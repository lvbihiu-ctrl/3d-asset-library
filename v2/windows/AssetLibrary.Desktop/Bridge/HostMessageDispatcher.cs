using System.Diagnostics;
using System.Text.Json;
using AssetLibrary.Desktop.Library;

namespace AssetLibrary.Desktop.Bridge;

public sealed class HostMessageDispatcher
{
    public const int MaxMessageCharacters = 32 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly Func<HostInfo> _hostInfo;
    private readonly Func<Task<ImportLibraryResult>>? _importLibrary;
    private readonly Func<string, bool>? _locateAsset;
    private readonly Func<LibraryCatalog?>? _getLibrary;
    private readonly Func<string, Task<LibraryAsset>>? _setCoverFromClipboard;
    private readonly Func<Task<ImageSearchResult>>? _searchSimilarClipboard;
    private readonly Func<string, Task<bool>>? _beginAssetDrag;
    private readonly Func<Task<ImportLibraryResult>>? _rescanLibrary;

    public HostMessageDispatcher(
        Func<HostInfo> hostInfo,
        Func<Task<ImportLibraryResult>>? importLibrary = null,
        Func<string, bool>? locateAsset = null,
        Func<LibraryCatalog?>? getLibrary = null,
        Func<string, Task<LibraryAsset>>? setCoverFromClipboard = null,
        Func<Task<ImageSearchResult>>? searchSimilarClipboard = null,
        Func<string, Task<bool>>? beginAssetDrag = null,
        Func<Task<ImportLibraryResult>>? rescanLibrary = null)
    {
        _hostInfo = hostInfo ?? throw new ArgumentNullException(nameof(hostInfo));
        _importLibrary = importLibrary;
        _locateAsset = locateAsset;
        _getLibrary = getLibrary;
        _setCoverFromClipboard = setCoverFromClipboard;
        _searchSimilarClipboard = searchSimilarClipboard;
        _beginAssetDrag = beginAssetDrag;
        _rescanLibrary = rescanLibrary;
    }

    public string Dispatch(string? rawMessage) =>
        DispatchAsync(rawMessage).GetAwaiter().GetResult();

    public async Task<string> DispatchAsync(string? rawMessage)
    {
        if (rawMessage is null || rawMessage.Length > MaxMessageCharacters)
        {
            return Error(null, "message_too_large", "页面消息超过允许大小。");
        }

        try
        {
            using var document = JsonDocument.Parse(rawMessage, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 8,
            });

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Error(null, "invalid_message", "页面消息必须是 JSON 对象。");
            }

            var root = document.RootElement;
            var id = ReadString(root, "id");
            if (!IsValidId(id))
            {
                return Error(null, "invalid_id", "消息 id 格式无效。");
            }

            var action = ReadString(root, "action");
            if (!root.TryGetProperty("payload", out var payload)
                || payload.ValueKind != JsonValueKind.Object)
            {
                return Error(id, "invalid_payload", "页面动作必须包含对象 payload。");
            }

            return action switch
            {
                "getHostInfo" => DispatchHostInfo(id!, payload),
                "importFolder" => await DispatchImportAsync(id!, payload, _importLibrary),
                "rescanLibrary" => await DispatchImportAsync(id!, payload, _rescanLibrary),
                "locateAsset" => DispatchLocate(id!, payload),
                "beginAssetDrag" => await DispatchDragAsync(id!, payload),
                "getLibrary" => DispatchGetLibrary(id!, payload),
                "setCoverFromClipboard" => await DispatchClipboardAsync(id!, payload, true),
                "searchSimilarClipboard" => await DispatchClipboardAsync(id!, payload, false),
                _ => Error(id, "action_not_allowed", "该页面动作未被 Windows 宿主允许。"),
            };
        }
        catch (JsonException)
        {
            return Error(null, "invalid_message", "页面消息不是有效 JSON。");
        }
    }

    private string DispatchHostInfo(string id, JsonElement payload)
    {
        if (payload.EnumerateObject().Any())
        {
            return Error(id, "invalid_payload", "getHostInfo 不接受任何输入字段。");
        }

        try
        {
            var info = _hostInfo();
            var capabilities = new List<string> { "getHostInfo" };
            if (_importLibrary is not null)
            {
                capabilities.Add("importFolder");
            }

            if (_locateAsset is not null)
            {
                capabilities.Add("locateAsset");
            }
            if (_getLibrary is not null) capabilities.Add("getLibrary");
            if (_setCoverFromClipboard is not null) capabilities.Add("setCoverFromClipboard");
            if (_searchSimilarClipboard is not null) capabilities.Add("searchSimilarClipboard");
            if (_beginAssetDrag is not null) capabilities.Add("beginAssetDrag");
            if (_rescanLibrary is not null) capabilities.Add("rescanLibrary");

            return Success(id, new
            {
                info.ProductVersion,
                info.Framework,
                info.Architecture,
                capabilities,
            });
        }
        catch (Exception exception)
        {
            Trace.TraceError($"Native host action failed: {exception}");
            return Error(id, "native_error", "Windows host could not complete the request.");
        }
    }

    private async Task<string> DispatchImportAsync(string id, JsonElement payload, Func<Task<ImportLibraryResult>>? operation)
    {
        if (operation is null)
        {
            return Error(id, "action_not_allowed", "真实素材扫描尚未启用。");
        }

        if (payload.EnumerateObject().Any())
        {
            return Error(id, "invalid_payload", "importFolder 不接受网页路径或其他输入字段。");
        }

        try
        {
            return Success(id, await operation());
        }
        catch (Exception exception)
        {
            Trace.TraceError($"Native library scan failed: {exception}");
            return Error(id, "scan_failed", "素材文件夹扫描失败，请确认磁盘已连接且文件夹可读取。");
        }
    }

    private string DispatchLocate(string id, JsonElement payload)
    {
        if (_locateAsset is null)
        {
            return Error(id, "action_not_allowed", "原文件定位尚未启用。");
        }

        var properties = payload.EnumerateObject().ToArray();
        if (properties.Length != 1
            || properties[0].Name != "assetId"
            || properties[0].Value.ValueKind != JsonValueKind.String)
        {
            return Error(id, "invalid_payload", "locateAsset 只接受一个素材 ID，不接受文件路径。");
        }

        var assetId = properties[0].Value.GetString();
        if (!IsValidAssetId(assetId))
        {
            return Error(id, "invalid_asset", "素材 ID 无效或已过期。");
        }

        try
        {
            return _locateAsset(assetId!)
                ? Success(id, new { located = true })
                : Error(id, "asset_unavailable", "原文件不存在、磁盘未连接，或素材 ID 已过期。");
        }
        catch (Exception exception)
        {
            Trace.TraceError($"Native locate failed: {exception}");
            return Error(id, "locate_failed", "Windows 资源管理器无法定位该素材。");
        }
    }

    private async Task<string> DispatchDragAsync(string id, JsonElement payload)
    {
        if (_beginAssetDrag is null) return Error(id, "action_not_allowed", "原生文件拖拽尚未接入。");
        var properties = payload.EnumerateObject().ToArray();
        var assetId = ReadString(payload, "assetId");
        if (properties.Length != 1 || properties[0].Name != "assetId" || !IsValidAssetId(assetId))
            return Error(id, "invalid_payload", "拖拽只接受有效素材 ID，不接受网页路径或封面网址。");
        try
        {
            // A successful OLE drop is not proof that the receiving application opened the model.
            return Success(id, new { dropped = await _beginAssetDrag(assetId!) });
        }
        catch (InvalidOperationException exception)
        {
            return Error(id, "drag_unavailable", exception.Message);
        }
        catch (Exception exception)
        {
            Trace.TraceError($"Native file drag failed: {exception}");
            return Error(id, "drag_failed", "文件拖出失败，请确认原模型可读取且接收软件没有阻塞对话框。");
        }
    }

    private string DispatchGetLibrary(string id, JsonElement payload)
    {
        if (_getLibrary is null) return Error(id, "action_not_allowed", "本地索引尚未启用。");
        if (payload.EnumerateObject().Any()) return Error(id, "invalid_payload", "本地索引不接受网页路径。");
        return Success(id, new { catalog = _getLibrary() });
    }

    private async Task<string> DispatchClipboardAsync(string id, JsonElement payload, bool setCover)
    {
        if ((setCover && _setCoverFromClipboard is null) || (!setCover && _searchSimilarClipboard is null))
            return Error(id, "action_not_allowed", "剪贴板功能尚未接入。");
        var properties = payload.EnumerateObject().ToArray();
        var assetId = ReadString(payload, "assetId");
        if (setCover ? properties.Length != 1 || properties[0].Name != "assetId" || !IsValidAssetId(assetId) : properties.Length != 0)
            return Error(id, "invalid_payload", "封面操作只接受有效素材 ID；以图搜图不接受额外字段。");
        try
        {
            if (setCover)
                return Success(id, new { asset = await _setCoverFromClipboard!(assetId!), revision = Guid.NewGuid().ToString("N") });
            return Success(id, await _searchSimilarClipboard!());
        }
        catch (InvalidOperationException exception)
        {
            return Error(id, "clipboard_unavailable", exception.Message);
        }
        catch (Exception exception)
        {
            Trace.TraceError($"Clipboard feature failed: {exception}");
            return Error(id, "clipboard_failed", "操作未完成，请检查剪贴板是否包含图片、封面是否可读取，以及 D 盘剩余空间。");
        }
    }

    private static string? ReadString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
    }

    private static bool IsValidId(string? id)
    {
        return id is { Length: >= 1 and <= 64 }
            && id.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or ':' or '-');
    }

    private static bool IsValidAssetId(string? assetId)
    {
        return assetId is { Length: 64 }
            && assetId.All(character => char.IsAsciiHexDigit(character));
    }

    private static string Success(string id, object result)
    {
        return JsonSerializer.Serialize(new { id, ok = true, result }, JsonOptions);
    }

    private static string Error(string? id, string code, string message)
    {
        return JsonSerializer.Serialize(new
        {
            id,
            ok = false,
            error = new { code, message },
        }, JsonOptions);
    }
}
