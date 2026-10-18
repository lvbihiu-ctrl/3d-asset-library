using System.Security.Cryptography;
using System.Text.Json;
using AssetLibrary.Desktop.Library;
using Microsoft.Web.WebView2.WinForms;

namespace AssetLibrary.Desktop;

internal static class ClipboardFeatureSmoke
{
    public static async Task VerifyAsync(WebView2 view, LibrarySession session, LocalLibraryStore store, string fixtureRoot)
    {
        if (!Path.GetFullPath(fixtureRoot).StartsWith(@"D:\Codex\", StringComparison.OrdinalIgnoreCase)
            || !new DirectoryInfo(fixtureRoot).Name.StartsWith("clipboard-fixtures-", StringComparison.Ordinal))
            throw new InvalidOperationException("Clipboard test requires a dedicated D:\\Codex fixture directory.");
        if (session.Count == 2)
        {
            await WaitAsync(view, "document.querySelectorAll('#assets article.asset').length === 2 && !document.querySelector('#pasteCover').disabled && !document.querySelector('#findImage').disabled");
            if (session.Catalog!.Assets.Any(asset => !asset.HasCover || asset.CoverPath is null))
                throw new InvalidDataException("Restart did not restore covers.");
            await WaitForImagesAsync(view);
            Console.WriteLine("CLIPBOARD RESTORE PASS: EXE restored 2 records, cover bytes, enabled controls and decoded images.");
            return;
        }
        Directory.CreateDirectory(fixtureRoot);
        if (Directory.EnumerateFileSystemEntries(fixtureRoot).Any()) throw new InvalidOperationException("Fixture directory must be empty.");
        File.WriteAllText(Path.Combine(fixtureRoot, "clipboard-a.c4d"), "Synthetic test file, not a C4D scene.");
        File.WriteAllText(Path.Combine(fixtureRoot, "clipboard-b.obj"), "# Synthetic OBJ fixture\nv 0 0 0\n");
        var before = Directory.GetFiles(fixtureRoot).ToDictionary(path => path, path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
        var originalClipboard = SnapshotClipboard();
        try
        {
            await view.CoreWebView2.ExecuteScriptAsync("document.querySelector('#importFolder').click()");
            await WaitAsync(view, "document.querySelectorAll('#assets article.asset').length === 2 && !document.querySelector('#pasteCover').disabled");
            var ids = session.Catalog!.Assets.Select(asset => asset.Id).ToArray();
            using var first = Pattern(false);
            using var second = Pattern(true);
            Clipboard.SetImage(first);
            await view.CoreWebView2.ExecuteScriptAsync("document.querySelector('#pasteCover').click()");
            await WaitAsync(view, "document.documentElement.dataset.coverState === 'saved'");
            await WaitForImagesAsync(view);
            var firstCover = session.Catalog.Assets.Single(asset => asset.Id == ids[0]).CoverPath;
            if (firstCover is null || !File.Exists(firstCover)) throw new InvalidDataException("Cover button did not persist PNG.");

            await view.CoreWebView2.ExecuteScriptAsync($"document.querySelector('[data-id=\"{ids[1]}\"]').click(); document.querySelector('[data-id=\"{ids[1]}\"]').focus();");
            Clipboard.SetImage(second);
            await view.CoreWebView2.ExecuteScriptAsync("document.dispatchEvent(new KeyboardEvent('keydown', {key:'v',ctrlKey:true,bubbles:true}));");
            await WaitAsync(view, "document.documentElement.dataset.coverState === 'saved'");
            await WaitForImagesAsync(view);
            Clipboard.SetImage(first);
            await view.CoreWebView2.ExecuteScriptAsync("document.querySelector('#findImage').click()");
            await WaitAsync(view, "document.documentElement.dataset.imageSearchState === 'complete'");
            var topId = JsonSerializer.Deserialize<string>(await view.CoreWebView2.ExecuteScriptAsync("document.querySelector('#assets article.asset').dataset.id"));
            if (topId != ids[0]) throw new InvalidDataException("Image search did not rank the matching cover first.");
            await view.CoreWebView2.ExecuteScriptAsync("document.querySelector('#showAll').click()");
            var restored = store.Load();
            if (restored?.Assets.Count != 2 || restored.Assets.Any(asset => !asset.HasCover)) throw new InvalidDataException("Persisted index/cover map could not reopen.");
            var coverBytesBefore = restored.Assets.ToDictionary(asset => asset.Id, asset => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(asset.CoverPath!))));
            Clipboard.SetText("clipboard text fixture");
            await view.CoreWebView2.ExecuteScriptAsync("document.querySelector('#pasteCover').click()");
            await WaitAsync(view, "document.documentElement.dataset.coverState === 'error'");
            await view.CoreWebView2.ExecuteScriptAsync("document.querySelector('#findImage').click()");
            await WaitAsync(view, "document.documentElement.dataset.imageSearchState === 'error'");
            foreach (var asset in store.Load()!.Assets)
                if (coverBytesBefore[asset.Id] != Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(asset.CoverPath!))))
                    throw new InvalidDataException("Invalid clipboard changed an existing cover.");
            foreach (var (path, hash) in before)
                if (hash != Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))) throw new InvalidDataException("Source file changed.");
            await view.CoreWebView2.ExecuteScriptAsync("document.querySelector('#importFolder').focus(); document.querySelector('#mainContent').scrollTop=0; document.querySelector('#inspector').scrollTop=0;");
            await Task.Delay(3300); // Let the intentionally triggered clipboard error toast dismiss before evidence capture.
            Console.WriteLine("CLIPBOARD FEATURES PASS: real WebView2 button and Ctrl+V saved distinct images; matching cover ranked Top-1; text clipboard rejected without changing covers; source hashes unchanged.");
        }
        finally
        {
            if (originalClipboard.GetFormats(false).Length == 0) Clipboard.Clear();
            else Clipboard.SetDataObject(originalClipboard, true);
        }
    }

    private static DataObject SnapshotClipboard()
    {
        var snapshot = new DataObject();
        var original = Clipboard.GetDataObject();
        if (original is null) return snapshot;
        foreach (var format in original.GetFormats(false))
        {
            var value = original.GetData(format, false);
            if (value is Image image) value = new Bitmap(image);
            else if (value is MemoryStream stream) value = new MemoryStream(stream.ToArray());
            if (value is not null) snapshot.SetData(format, false, value);
        }
        return snapshot;
    }

    public static Bitmap Pattern(bool reverse)
    {
        var bitmap = new Bitmap(360, 240);
        for (var y = 0; y < bitmap.Height; y++)
            for (var x = 0; x < bitmap.Width; x++)
            {
                var value = reverse ? x * 255 / bitmap.Width : 255 - x * 255 / bitmap.Width;
                bitmap.SetPixel(x, y, Color.FromArgb(value, value, value));
            }
        return bitmap;
    }

    private static Task WaitForImagesAsync(WebView2 view) => WaitAsync(view,
        "[...document.querySelectorAll('img[src^=\"https://assetlibrary-covers.local/\"]')].every(img => img.complete && img.naturalWidth > 0)");

    private static async Task WaitAsync(WebView2 view, string expression)
    {
        var end = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < end)
        {
            if (await view.CoreWebView2.ExecuteScriptAsync(expression) == "true") return;
            await Task.Delay(60);
        }
        var detail = await view.CoreWebView2.ExecuteScriptAsync("JSON.stringify({cover:document.documentElement.dataset.coverState,search:document.documentElement.dataset.imageSearchState,toast:document.querySelector('#toast')?.textContent,pasteDisabled:document.querySelector('#pasteCover')?.disabled})");
        throw new InvalidDataException("Clipboard WebView2 verification timed out: " + expression + "; " + detail);
    }
}
