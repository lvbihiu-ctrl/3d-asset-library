using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace AssetLibrary.Desktop;

internal static class ScreenshotCapture
{
    public static async Task CaptureRequiredAsync(
        WebView2 webView,
        Form window,
        string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        foreach (var size in new[] { new Size(1016, 689), new Size(1920, 1080) })
        {
            var outputPath = Path.Combine(outputDirectory, $"webview2-{size.Width}x{size.Height}.png");
            await CapturePngAsync(webView, window, size, outputPath);
        }

        var darkModeEnabled = await webView.CoreWebView2.ExecuteScriptAsync(
            "(() => { const button = document.querySelector('#theme'); "
            + "if (!document.body.classList.contains('dark')) button.click(); "
            + "return document.body.classList.contains('dark') "
            + "&& button.getAttribute('aria-pressed') === 'true'; })()");
        if (darkModeEnabled != "true")
        {
            throw new InvalidDataException("Could not enable dark mode for screenshot evidence.");
        }

        var darkSize = new Size(1016, 689);
        await CapturePngAsync(
            webView,
            window,
            darkSize,
            Path.Combine(outputDirectory, "webview2-dark-1016x689.png"));
    }

    private static async Task CapturePngAsync(
        WebView2 webView,
        Form window,
        Size size,
        string outputPath)
    {
        window.ClientSize = size;
        await Task.Delay(400);
        await using (var stream = File.Create(outputPath))
        {
            await webView.CoreWebView2.CapturePreviewAsync(
                CoreWebView2CapturePreviewImageFormat.Png,
                stream);
        }

        var actual = PngDimensions.Read(outputPath);
        if (actual.Width != size.Width || actual.Height != size.Height)
        {
            throw new InvalidDataException(
                $"Screenshot size mismatch. Requested {size.Width}x{size.Height}, "
                + $"captured {actual.Width}x{actual.Height}.");
        }

        Console.WriteLine($"SCREENSHOT PASS: {actual.Width}x{actual.Height} {outputPath}");
    }
}
