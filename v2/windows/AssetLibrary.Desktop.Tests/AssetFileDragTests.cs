using System.Text.Json;
using System.Windows.Forms;
using AssetLibrary.Desktop.Bridge;
using AssetLibrary.Desktop.Library;

namespace AssetLibrary.Desktop.Tests;

public sealed class AssetFileDragTests
{
    [Fact]
    public void PayloadUsesModelFileDropNotCoverOrBrowserUrl()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "drag-test-data", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var model = Path.Combine(root, "中文 模型.obj");
            File.WriteAllText(model, "v 0 0 0");
            File.WriteAllText(Path.Combine(root, "中文 模型.png"), "synthetic cover marker");
            var catalog = new LibraryScanner().Scan(root);
            var session = new LibrarySession();
            session.Replace(catalog);
            var asset = Assert.Single(catalog.Assets);
            var payload = AssetFileDrag.CreatePayload(session, asset.Id);
            Assert.Equal(new[] { model }, payload.GetFileDropList()!.Cast<string>());
            Assert.Contains(DataFormats.FileDrop, payload.GetFormats());
            Assert.False(payload.GetDataPresent(DataFormats.Text, false));
            Assert.False(payload.GetDataPresent(DataFormats.Bitmap, false));
            Assert.Throws<InvalidOperationException>(() => AssetFileDrag.CreatePayload(session, new string('b', 64)));
            File.Delete(model);
            Assert.Throws<InvalidOperationException>(() => AssetFileDrag.CreatePayload(session, asset.Id));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void BridgeAcceptsOnlyAssetIdAndReportsCancellationWithoutClaimingImport()
    {
        var calls = new List<string>();
        var dispatcher = new HostMessageDispatcher(
            () => new HostInfo("test", ".NET 10", "X64"),
            beginAssetDrag: id => { calls.Add(id); return Task.FromResult(false); });
        var assetId = new string('a', 64);
        string Request(object payload) => JsonSerializer.Serialize(new { id = "drag-1", action = "beginAssetDrag", payload });
        foreach (var payload in new object[] { new { path = "D:/model.c4d" }, new { assetId, url = "https://cover" }, new { assetId = "bad" } })
        {
            using var rejected = JsonDocument.Parse(dispatcher.Dispatch(Request(payload)));
            Assert.False(rejected.RootElement.GetProperty("ok").GetBoolean());
        }
        Assert.Empty(calls);
        using var accepted = JsonDocument.Parse(dispatcher.Dispatch(Request(new { assetId })));
        Assert.True(accepted.RootElement.GetProperty("ok").GetBoolean());
        Assert.False(accepted.RootElement.GetProperty("result").GetProperty("dropped").GetBoolean());
        Assert.Equal(new[] { assetId }, calls);
    }
}
