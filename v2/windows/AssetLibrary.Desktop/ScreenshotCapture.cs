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
            window.ClientSize = size;
            await Task.Delay(400);
            var outputPath = Path.Combine(outputDirectory, $"webview2-{size.Width}x{size.Height}.png");
            await using var stream = File.Create(outputPath);
            await webView.CoreWebView2.CapturePreviewAsync(
                CoreWebView2CapturePreviewImageFormat.Png,
                stream);
        }
    }
}
