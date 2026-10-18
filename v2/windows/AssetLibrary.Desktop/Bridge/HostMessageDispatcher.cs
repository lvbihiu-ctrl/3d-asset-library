using System.Diagnostics;
using System.Text.Json;

namespace AssetLibrary.Desktop.Bridge;

public sealed class HostMessageDispatcher
{
    public const int MaxMessageCharacters = 32 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly Func<HostInfo> _hostInfo;

    public HostMessageDispatcher(Func<HostInfo> hostInfo)
    {
        _hostInfo = hostInfo ?? throw new ArgumentNullException(nameof(hostInfo));
    }

    public string Dispatch(string? rawMessage)
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
            if (!string.Equals(action, "getHostInfo", StringComparison.Ordinal))
            {
                return Error(id, "action_not_allowed", "该页面动作未被 Windows 宿主允许。");
            }

            if (!root.TryGetProperty("payload", out var payload)
                || payload.ValueKind != JsonValueKind.Object
                || payload.EnumerateObject().Any())
            {
                return Error(id, "invalid_payload", "getHostInfo 不接受任何输入字段。");
            }

            HostInfo info;
            try
            {
                info = _hostInfo();
            }
            catch (Exception exception)
            {
                Trace.TraceError($"Native host action failed: {exception}");
                return Error(id, "native_error", "Windows host could not complete the request.");
            }
            return JsonSerializer.Serialize(new
            {
                id,
                ok = true,
                result = new
                {
                    info.ProductVersion,
                    info.Framework,
                    info.Architecture,
                    capabilities = new[] { "getHostInfo" },
                },
            }, JsonOptions);
        }
        catch (JsonException)
        {
            return Error(null, "invalid_message", "页面消息不是有效 JSON。");
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
