using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using AssetLibrary.Desktop.Bridge;
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

    public MainWindow(HostOptions options)
    {
        _options = options;
        _dispatcher = new HostMessageDispatcher(CreateHostInfo);
        Text = "3D 素材管理器 V2";
        ClientSize = new Size(1016, 689);
        MinimumSize = new Size(820, 600);
        StartPosition = FormStartPosition.CenterScreen;
        Controls.Add(_webView);
        Shown += OnShown;
    }

    public int ExitCode { get; private set; }

    private async void OnShown(object? sender, EventArgs eventArgs)
    {
        Shown -= OnShown;
        try
        {
            await InitializeWebViewAsync();
            if (_options.UiInteractionTest)
            {
                await WebViewInteractionSmoke.VerifyAsync(_webView);
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

        var userDataDirectory = _options.UserDataDirectory
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "3DAssetLibrary",
                "V2Preview",
                "WebView2");
        Directory.CreateDirectory(userDataDirectory);

        var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataDirectory);
        await _webView.EnsureCoreWebView2Async(environment);
        var core = _webView.CoreWebView2;
        core.Settings.IsWebMessageEnabled = true;
        core.Settings.AreHostObjectsAllowed = false;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
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

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
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

        _webView.CoreWebView2.PostWebMessageAsJson(_dispatcher.Dispatch(rawMessage));
    }

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
                    "Boolean(document.querySelector('#app[data-ui-contract=\"issue-32\"]') && document.querySelector('#search') && document.querySelector('#special-nav') && document.querySelector('#inspector') && document.querySelectorAll('#assets article.asset').length >= 15)");
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
