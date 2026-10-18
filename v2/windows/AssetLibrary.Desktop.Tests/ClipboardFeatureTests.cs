using System.Text.Json;
using AssetLibrary.Desktop.Bridge;
using AssetLibrary.Desktop.Library;

namespace AssetLibrary.Desktop.Tests;

public sealed class ClipboardFeatureTests
{
    [Fact]
    public void DesktopHost_HasRealClipboardHandlersInsteadOfDisabledPlaceholders()
    {
        // The constructor must expose handlers, not merely advertise fake capabilities in HTML.
        var parameters = typeof(HostMessageDispatcher).GetConstructors().Single().GetParameters();
        Assert.Contains(parameters, parameter => parameter.Name == "setCoverFromClipboard");
        Assert.Contains(parameters, parameter => parameter.Name == "searchSimilarClipboard");
    }

    [Fact]
    public async Task ClipboardBridge_RejectsPaths_AndReportsMissingImageWithoutSuccess()
    {
        var calls = 0;
        var dispatcher = new HostMessageDispatcher(() => new HostInfo("test", ".NET", "X64"),
            setCoverFromClipboard: _ => { calls++; throw new InvalidOperationException("剪贴板中没有图片"); },
            searchSimilarClipboard: () => Task.FromResult(new ImageSearchResult([], 0)));
        var id = new string('a', 64);
        foreach (var request in new[]
        {
            "{\"id\":\"x\",\"action\":\"searchSimilarClipboard\",\"payload\":{\"path\":\"D:/private.png\"}}",
            $"{{\"id\":\"x\",\"action\":\"setCoverFromClipboard\",\"payload\":{{\"assetId\":\"{id}\",\"path\":\"D:/private.png\"}}}}",
        })
        {
            using var response = JsonDocument.Parse(await dispatcher.DispatchAsync(request));
            Assert.False(response.RootElement.GetProperty("ok").GetBoolean());
        }
        Assert.Equal(0, calls);
        using var missingImage = JsonDocument.Parse(await dispatcher.DispatchAsync(
            $"{{\"id\":\"x\",\"action\":\"setCoverFromClipboard\",\"payload\":{{\"assetId\":\"{id}\"}}}}"));
        Assert.False(missingImage.RootElement.GetProperty("ok").GetBoolean());
        Assert.Contains("没有图片", missingImage.RootElement.GetProperty("error").GetProperty("message").GetString());
        Assert.Equal(1, calls);
    }
}
