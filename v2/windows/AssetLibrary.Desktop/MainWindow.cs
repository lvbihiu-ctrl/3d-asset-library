using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using AssetLibrary.Desktop.Bridge;
using AssetLibrary.Desktop.Library;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace AssetLibrary.Desktop;

internal sealed class MainWindow : Form
{
    private static readonly TimeSpan NavigationTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan BridgeTimeout = TimeSpan.FromSeconds(10);

    private readonly HostOptions _options;
    private readonly WebView2 _webView = new() { Dock = DockStyle.Fill };
    private readonly HostMessageDispatcher _dispatcher;
    private readonly LibraryScanner _libraryScanner = new();
    private readonly LibrarySession _librarySession = new();
    private readonly LocalLibraryStore _store;
    private readonly ImageSimilarity _imageSearch = new();
    private readonly SemaphoreSlim _libraryWork = new(1, 1);
    private string? _lastAutomatedLocatedAssetId;
    private bool _dragging;
    private Queue<string[]>? _automatedFolderSelections;

    public MainWindow(HostOptions options)
    {
        _options = options;
        var dataDirectory = options.DataDirectory ?? (options.IsAutomated
            ? Path.Combine(options.UserDataDirectory ?? throw new ArgumentException("自动测试必须指定 --user-data-dir 或 --data-dir。"), "test-library")
            : @"D:\Codex\3D-Asset-Library-V2-Data");
        _store = new LocalLibraryStore(dataDirectory);
        var catalog = _store.Load();
        if (catalog is null && (options.LegacyDataDirectory is not null || !options.IsAutomated))
            catalog = _store.ImportLegacy(options.LegacyDataDirectory ?? @"D:\Codex\3D-Asset-Library-Ready\.local-data");
        if (catalog is not null) _librarySession.Replace(catalog);
        _dispatcher = new HostMessageDispatcher(
            CreateHostInfo,
            ImportLibraryAsync,
            LocateAsset,
            () => _librarySession.Catalog,
            SetCoverFromClipboardAsync,
            SearchSimilarClipboardAsync,
            BeginAssetDragAsync,
            RescanLibraryAsync);
        Text = "3D 素材管理器 V2";
        ClientSize = new Size(1016, 689);
        MinimumSize = new Size(820, 600);
        StartPosition = FormStartPosition.CenterScreen;
        Controls.Add(_webView);
        Shown += OnShown;
    }

    public int ExitCode { get; private set; }

    private async Task<bool> BeginAssetDragAsync(string assetId)
    {
        // Leave WebView2's message callback before entering OLE's nested message loop.
        await Task.Yield();
        if (IsDisposed || Disposing) return false;
        if (_dragging || _libraryWork.CurrentCount == 0)
            throw new InvalidOperationException("请等待当前素材操作结束后再拖出。");
        if ((MouseButtons & MouseButtons.Left) == 0)
            throw new InvalidOperationException("请按住素材卡片并拖向 C4D 窗口。");
        var payload = AssetFileDrag.CreatePayload(_librarySession, assetId);
        _dragging = true;
        try
        {
            // Windows supplies the physical Shift state to the receiving application's OLE handler.
            return _webView.DoDragDrop(payload, DragDropEffects.Copy) != DragDropEffects.None;
        }
        finally { _dragging = false; }
    }

    private async void OnShown(object? sender, EventArgs eventArgs)
    {
        Shown -= OnShown;
        try
        {
            await InitializeWebViewAsync();
            if (_options.LargeLibraryTest)
            {
                await WebViewInteractionSmoke.VerifyLargeLibraryAsync(_webView, _librarySession.Catalog!);
                if (_options.CaptureDirectory is not null)
                    await ScreenshotCapture.CaptureRequiredAsync(_webView, this, _options.CaptureDirectory);
                Close();
                return;
            }
            if (_options.MultiFolderTest)
            {
                _automatedFolderSelections = MultiFolderFeatureSmoke.Prepare(_options.LibraryTestRoot!);
                await MultiFolderFeatureSmoke.VerifyAsync(_webView, _librarySession, _store, _options.LibraryTestRoot!);
                if (_options.CaptureDirectory is not null)
                    await ScreenshotCapture.CaptureRequiredAsync(_webView, this, _options.CaptureDirectory);
                Close();
                return;
            }
            if (_options.CoverFeatureTest)
            {
                await ClipboardFeatureSmoke.VerifyAsync(_webView, _librarySession, _store, _options.LibraryTestRoot!);
                if (_options.CaptureDirectory is not null)
                    await ScreenshotCapture.CaptureRequiredAsync(_webView, this, _options.CaptureDirectory);
                Close();
                return;
            }
            if (_options.LibraryTestRoot is not null)
            {
                await WebViewInteractionSmoke.VerifyRealLibraryAsync(
                    _webView,
                    () => _librarySession.Count,
                    () => _lastAutomatedLocatedAssetId);
                Console.WriteLine("REAL LIBRARY PASS: 真实目录只读扫描、卡片、搜索和定位入口已在 WebView2 中验证。");
                if (_options.CaptureDirectory is not null)
                {
                    await ScreenshotCapture.CaptureRequiredAsync(
                        _webView,
                        this,
                        _options.CaptureDirectory);
                }

                Close();
                return;
            }

            if (_options.UiInteractionTest)
            {
                await WebViewInteractionSmoke.VerifyAsync(_webView);
                await WebViewInteractionSmoke.VerifyCardZoomAsync(_webView);
                Console.WriteLine("UI INTERACTION PASS: 搜索、分类、主题、布局、卡片选择和滚动均已在 WebView2 中验证。");
                Close();
                return;
            }

            if (_options.CaptureDirectory is not null)
            {
                await ScreenshotCapture.CaptureRequiredAsync(
                    _webView,
                    this,
                    _options.CaptureDirectory);
                Console.WriteLine($"截图已写入：{_options.CaptureDirectory}");
                Close();
                return;
            }

            if (_options.SmokeTest)
            {
                Console.WriteLine("SMOKE PASS: WebView2 已加载受信任本地页面，消息桥已连接。");
                Close();
            }
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private async Task InitializeWebViewAsync()
    {
        var runtimeVersion = CoreWebView2Environment.GetAvailableBrowserVersionString();
        if (string.IsNullOrWhiteSpace(runtimeVersion))
        {
            throw new InvalidOperationException("未找到 Microsoft Edge WebView2 Runtime。请先安装 Evergreen Runtime。");
        }

        var webRoot = Path.Combine(AppContext.BaseDirectory, "Web");
        var indexPath = Path.Combine(webRoot, "index.html");
        var bridgePath = Path.Combine(webRoot, "host-bridge.js");
        var catalogPath = Path.Combine(webRoot, "synthetic-catalog.js");
        foreach (var requiredFile in new[] { indexPath, bridgePath, catalogPath })
        {
            if (!File.Exists(requiredFile))
            {
                throw new FileNotFoundException("发布目录缺少受信任页面资源。", requiredFile);
            }
        }

        var userDataDirectory = _options.UserDataDirectory ?? @"D:\Codex\3D-Asset-Library-V2-Data\WebView2";
        Directory.CreateDirectory(userDataDirectory);

        var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataDirectory);
        await _webView.EnsureCoreWebView2Async(environment);
        var core = _webView.CoreWebView2;
        core.Settings.IsWebMessageEnabled = true;
        core.Settings.AreHostObjectsAllowed = false;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        // Ctrl+wheel belongs to the material wall, not browser-wide page zoom.
        core.Settings.IsZoomControlEnabled = false;
        core.Settings.AreDefaultScriptDialogsEnabled = false;
        core.SetVirtualHostNameToFolderMapping(
            TrustedContentPolicy.VirtualHostName,
            webRoot,
            CoreWebView2HostResourceAccessKind.DenyCors);

        core.NavigationStarting += (_, args) =>
        {
            if (!TrustedContentPolicy.IsTrustedMessageSource(args.Uri))
            {
                args.Cancel = true;
            }
        };
        core.NewWindowRequested += (_, args) => args.Handled = true;
        core.DownloadStarting += (_, args) => args.Cancel = true;
        core.PermissionRequested += (_, args) => args.State = CoreWebView2PermissionState.Deny;
        core.WebMessageReceived += OnWebMessageReceived;
        core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += (_, args) =>
        {
            if (TryServeCover(core, args))
            {
                return;
            }

            if (!TrustedContentPolicy.IsTrustedResource(args.Request.Uri))
            {
                args.Response = core.Environment.CreateWebResourceResponse(
                    Stream.Null,
                    403,
                    "Blocked",
                    "Content-Type: text/plain; charset=utf-8");
            }
        };

        await core.AddScriptToExecuteOnDocumentCreatedAsync(await File.ReadAllTextAsync(catalogPath));
        await core.AddScriptToExecuteOnDocumentCreatedAsync(await File.ReadAllTextAsync(bridgePath));

        var navigation = new TaskCompletionSource<CoreWebView2NavigationCompletedEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        void NavigationCompleted(object? _, CoreWebView2NavigationCompletedEventArgs args) =>
            navigation.TrySetResult(args);
        core.NavigationCompleted += NavigationCompleted;
        try
        {
            _webView.Source = new Uri(TrustedContentPolicy.StartPage);
            var result = await navigation.Task.WaitAsync(NavigationTimeout);
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException($"本地页面加载失败：{result.WebErrorStatus}");
            }
        }
        finally
        {
            core.NavigationCompleted -= NavigationCompleted;
        }

        await WaitForBridgeAsync();
        Debug.WriteLine($"WebView2 Runtime: {runtimeVersion}");
    }

    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        if (!TrustedContentPolicy.IsTrustedMessageSource(args.Source))
        {
            Trace.TraceWarning("忽略来自非受信页面的 WebView2 消息。");
            return;
        }

        string rawMessage;
        try
        {
            rawMessage = args.WebMessageAsJson;
        }
        catch (Exception exception)
        {
            Trace.TraceWarning($"读取页面消息失败：{exception.Message}");
            return;
        }

        var response = await _dispatcher.DispatchAsync(rawMessage);
        if (!_webView.IsDisposed && _webView.CoreWebView2 is not null)
        {
            _webView.CoreWebView2.PostWebMessageAsJson(response);
        }
    }

    private async Task<ImportLibraryResult> ImportLibraryAsync()
    {
        await _libraryWork.WaitAsync();
        try { return await ImportLibraryCoreAsync(); }
        finally { _libraryWork.Release(); }
    }

    private async Task<ImportLibraryResult> ImportLibraryCoreAsync()
    {
        var progress = new Progress<LibraryScanProgress>(ReportScanProgress);
        if (_automatedFolderSelections is not null)
            return await ScanAndMergeAsync(_automatedFolderSelections.Dequeue(), progress);
        if (_options.LibraryTestRoot is not null)
        {
            return await ScanAndMergeAsync([_options.LibraryTestRoot], progress);
        }

        using var dialog = new FolderBrowserDialog
        {
            Description = "添加素材文件夹（可按 Ctrl 多选；保留已收录素材）",
            Multiselect = true,
            ShowNewFolderButton = false,
            UseDescriptionForTitle = true,
        };

        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.SelectedPaths.Length == 0)
        {
            return new ImportLibraryResult(true, null);
        }

        return await ScanAndMergeAsync(dialog.SelectedPaths, progress);
    }

    private async Task<ImportLibraryResult> ScanAndMergeAsync(IEnumerable<string> paths, IProgress<LibraryScanProgress> progress)
    {
        var scans = new List<LibraryCatalog>();
        foreach (var path in paths.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase))
            scans.Add(await Task.Run(() => _libraryScanner.Scan(path, progress: progress)));
        var previous = _librarySession.Catalog;
        var catalog = _store.MergeScans(previous, scans);
        var coveredIds = (previous?.Assets ?? []).Where(asset => asset.HasCover).Select(asset => asset.Id).ToHashSet(StringComparer.Ordinal);
        var newCovers = catalog.Assets.Count(asset => asset.HasCover && !coveredIds.Contains(asset.Id));
        _librarySession.Replace(catalog);
        return new ImportLibraryResult(false, catalog, catalog.Assets.Count - (previous?.Assets.Count ?? 0), newCovers);
    }

    private async Task<ImportLibraryResult> RescanLibraryAsync()
    {
        await _libraryWork.WaitAsync();
        try
        {
            var previous = _librarySession.Catalog ?? throw new InvalidOperationException("请先添加素材文件夹。");
            var available = previous.SourceRoots.Where(root => root.Available).Select(root => root.Path).ToArray();
            if (available.Length == 0) throw new DirectoryNotFoundException("素材磁盘未连接，已保留所有索引和封面。");
            return await ScanAndMergeAsync(available, new Progress<LibraryScanProgress>(ReportScanProgress));
        }
        finally { _libraryWork.Release(); }
    }

    private async Task<LibraryAsset> SetCoverFromClipboardAsync(string assetId)
    {
        await _libraryWork.WaitAsync();
        try
        {
            if (!_librarySession.TryGetAsset(assetId, out var asset) || asset is null)
                throw new InvalidOperationException("请先选择一条真实素材。");
            using var image = await GetClipboardImageAsync();
            var updated = _store.SaveCover(asset, image);
            var catalog = _librarySession.Catalog!;
            _librarySession.Replace(catalog with { Assets = catalog.Assets.Select(item => item.Id == assetId ? updated : item).ToArray() });
            // The atomic cover map is authoritative, even if a later catalog save cannot complete.
            return updated;
        }
        finally { _libraryWork.Release(); }
    }

    private async Task<ImageSearchResult> SearchSimilarClipboardAsync()
    {
        await _libraryWork.WaitAsync();
        try
        {
            if (_librarySession.Catalog is null) throw new InvalidOperationException("请先导入真实素材文件夹。");
            using var image = await GetClipboardImageAsync();
            var assets = _librarySession.Catalog.Assets;
            return await Task.Run(() => _imageSearch.Search(image, assets));
        }
        finally { _libraryWork.Release(); }
    }

    private static async Task<Image> GetClipboardImageAsync()
    {
        var occupied = false;
        for (var attempt = 0; attempt < 4; attempt++)
        {
            try
            {
                if (Clipboard.ContainsImage() && Clipboard.GetImage() is { } image) return image;
                occupied = false;
            }
            catch (ExternalException) { occupied = true; }
            // Clipboard owners and history tools can briefly hold the clipboard after a copy.
            // Yield on the UI context (STA); never move Clipboard APIs to Task.Run.
            if (attempt < 3) await Task.Delay(75);
        }
        throw new InvalidOperationException(occupied ? "剪贴板正被其他程序占用，请稍后重试。"
            : "剪贴板中没有图片。请先复制图片或截图，再执行此操作。");
    }

    private void ReportScanProgress(LibraryScanProgress progress)
    {
        if (_webView.IsDisposed || _webView.CoreWebView2 is null)
        {
            return;
        }

        var message = JsonSerializer.Serialize(new
        {
            eventName = "scanProgress",
            detail = new
            {
                progress.DirectoryCount,
                progress.AssetCount,
                progress.ErrorCount,
                progress.CurrentFolder,
            },
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        _webView.CoreWebView2.PostWebMessageAsJson(message);
    }

    private bool LocateAsset(string assetId)
    {
        if (_options.LibraryTestRoot is null)
        {
            return ExplorerAssetLocator.Locate(_librarySession, assetId);
        }

        if (!_librarySession.TryGetAsset(assetId, out var asset)
            || asset is null
            || !File.Exists(asset.FullPath))
        {
            return false;
        }

        _lastAutomatedLocatedAssetId = assetId;
        return true;
    }

    private bool TryServeCover(CoreWebView2 core, CoreWebView2WebResourceRequestedEventArgs args)
    {
        if (!TrustedContentPolicy.IsTrustedCoverResource(args.Request.Uri))
        {
            return false;
        }

        var assetId = new Uri(args.Request.Uri).AbsolutePath[1..];
        if (!_librarySession.TryGetCover(assetId, out var coverPath) || coverPath is null)
        {
            args.Response = core.Environment.CreateWebResourceResponse(
                Stream.Null,
                404,
                "Not Found",
                "Content-Type: text/plain; charset=utf-8");
            return true;
        }

        try
        {
            var stream = File.OpenRead(coverPath);
            args.Response = core.Environment.CreateWebResourceResponse(
                stream,
                200,
                "OK",
                $"Content-Type: {CoverContentType(coverPath)}\r\nCache-Control: no-store");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            args.Response = core.Environment.CreateWebResourceResponse(
                Stream.Null,
                404,
                "Not Found",
                "Content-Type: text/plain; charset=utf-8");
        }

        return true;
    }

    private static string CoverContentType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".webp" => "image/webp",
        ".bmp" => "image/bmp",
        _ => "application/octet-stream",
    };

    private async Task WaitForBridgeAsync()
    {
        var deadline = DateTime.UtcNow + BridgeTimeout;
        while (DateTime.UtcNow < deadline)
        {
            var rawState = await _webView.CoreWebView2.ExecuteScriptAsync(
                "document.documentElement.dataset.nativeHost || 'waiting'");
            var state = JsonSerializer.Deserialize<string>(rawState);
            if (state == "connected")
            {
                // The host handshake alone does not prove the V2 UI rendered.
                // Require actual masonry cards, search and sidebar before smoke-test success.
                var screenReady = await _webView.CoreWebView2.ExecuteScriptAsync(
                    "Boolean(document.querySelector('#app[data-ui-contract=\"issue-32\"]') && document.querySelector('#search') && document.querySelector('#special-nav') && document.querySelector('#inspector') && document.documentElement.dataset.libraryReady === 'true')");
                if (screenReady == "true")
                {
                    return;
                }
            }

            if (state == "error")
            {
                throw new InvalidOperationException("本地页面已加载，但消息桥握手失败。");
            }

            await Task.Delay(100).ConfigureAwait(true);
        }

        throw new TimeoutException("等待 WebView2 消息桥连接超时。");
    }

    private void Fail(Exception exception)
    {
        ExitCode = 1;
        var message = $"V2 Windows 客户端启动失败。\n\n{exception.Message}";
        Console.Error.WriteLine(message);
        if (!_options.IsAutomated)
        {
            MessageBox.Show(message, "3D 素材管理器", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        Close();
    }

    private static HostInfo CreateHostInfo()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";
        return new HostInfo(
            version,
            RuntimeInformation.FrameworkDescription,
            RuntimeInformation.ProcessArchitecture.ToString());
    }
}
