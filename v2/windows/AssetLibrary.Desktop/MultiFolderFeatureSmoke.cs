using System.Security.Cryptography;
using AssetLibrary.Desktop.Library;
using Microsoft.Web.WebView2.WinForms;

namespace AssetLibrary.Desktop;

internal static class MultiFolderFeatureSmoke
{
    public static Queue<string[]> Prepare(string root)
    {
        if (string.IsNullOrWhiteSpace(root) || !Path.GetFullPath(root).StartsWith(@"D:\Codex\", StringComparison.OrdinalIgnoreCase)
            || !new DirectoryInfo(root).Name.StartsWith("multi-fixtures-", StringComparison.Ordinal))
            throw new InvalidOperationException("Multi-folder smoke requires a dedicated D:\\Codex fixture folder.");
        Directory.CreateDirectory(root);
        if (Directory.EnumerateFileSystemEntries(root).Any()) throw new InvalidOperationException("Multi-folder fixture must be empty.");
        var a = Directory.CreateDirectory(Path.Combine(root, "Folder A")).FullName;
        var b = Directory.CreateDirectory(Path.Combine(root, "Folder B")).FullName;
        File.WriteAllText(Path.Combine(a, "alpha.c4d"), "Synthetic marker, not an actual C4D scene.");
        File.WriteAllText(Path.Combine(b, "beta.obj"), "# Synthetic OBJ\nv 0 0 0\n");
        using (var first = new Bitmap(160, 100))
        {
            using var graphics = Graphics.FromImage(first); graphics.Clear(Color.SteelBlue);
            first.Save(Path.Combine(a, "alpha_preview.PNG"), System.Drawing.Imaging.ImageFormat.Png);
        }
        using (var second = new Bitmap(100, 160))
        {
            using var graphics = Graphics.FromImage(second); graphics.Clear(Color.Coral);
            second.Save(Path.Combine(b, "cover.png"), System.Drawing.Imaging.ImageFormat.Png);
        }
        return new Queue<string[]>([new[] { a }, new[] { a, b }, new[] { a }, new[] { root }]);
    }

    public static async Task VerifyAsync(WebView2 view, LibrarySession session, LocalLibraryStore store, string root)
    {
        var originals = Directory.GetFiles(root, "*", SearchOption.AllDirectories).ToDictionary(path => path, Hash);
        for (var index = 0; index < 4; index++)
        {
            await view.CoreWebView2.ExecuteScriptAsync("document.querySelector('#importFolder').click(); true");
            await WaitAsync(view, $"!document.querySelector('#importFolder').disabled && document.querySelectorAll('article.asset').length === {(index == 0 ? 1 : 2)}");
            if (session.Count != (index == 0 ? 1 : 2)) throw new InvalidDataException("Import replaced or duplicated an existing model.");
        }
        if (session.Catalog!.SourceRoots.Count != 3 || session.Catalog.Assets.Any(asset => !asset.HasCover))
            throw new InvalidDataException("Source folders or automatic previews were not retained.");
        await WaitAsync(view, "document.querySelectorAll('[data-filter^=\"source:\"]').length === 3 && [...document.querySelectorAll('article.asset img')].every(img=>img.complete && img.naturalWidth>0)");
        var originalAsset = session.Catalog.Assets.First(asset => asset.Name == "alpha");
        using var manual = new Bitmap(140, 90);
        using (var graphics = Graphics.FromImage(manual)) graphics.Clear(Color.MediumSeaGreen);
        var custom = store.SaveCover(originalAsset, manual);
        var customHash = Hash(custom.CoverPath!);
        session.Replace(session.Catalog with { Assets = session.Catalog.Assets.Select(asset => asset.Id == custom.Id ? custom : asset).ToArray() });
        await view.CoreWebView2.ExecuteScriptAsync("document.querySelector('#rescanLibrary').click(); true");
        await WaitAsync(view, "!document.querySelector('#rescanLibrary').disabled && document.querySelectorAll('article.asset').length===2");
        var restored = store.Load()!;
        if (restored.Assets.Count != 2 || restored.SourceRoots.Count != 3 || restored.Assets.Single(asset => asset.Id == custom.Id).CoverPath != custom.CoverPath || Hash(custom.CoverPath!) != customHash)
            throw new InvalidDataException("Repeat scan did not preserve the manual cover and stored roots.");
        var b = restored.SourceRoots.Single(folder => folder.Name == "Folder B");
        var parent = restored.SourceRoots.Single(folder => folder.Path == root);
        await view.CoreWebView2.ExecuteScriptAsync($"document.querySelector('[data-filter=\"source:{parent.Id}\"]').click(); true");
        await WaitAsync(view, "document.querySelectorAll('article.asset').length===2");
        await view.CoreWebView2.ExecuteScriptAsync($"document.querySelector('[data-filter=\"source:{b.Id}\"]').click(); true");
        await WaitAsync(view, "document.querySelectorAll('article.asset').length===1 && document.querySelector('.aname').textContent==='beta'");
        await view.CoreWebView2.ExecuteScriptAsync("document.querySelector('#showAll').click(); document.querySelector('#search').value='alpha'; document.querySelector('#search').dispatchEvent(new Event('input',{bubbles:true})); true");
        await WaitAsync(view, "document.querySelectorAll('article.asset').length===1 && document.querySelector('.aname').textContent==='alpha'");
        await view.CoreWebView2.ExecuteScriptAsync("document.querySelector('#showAll').click(); true");
        foreach (var (path, hash) in originals) if (Hash(path) != hash) throw new InvalidDataException("Original source or preview changed.");
        await WaitAsync(view, "[...document.querySelectorAll('article.asset img')].every(img=>img.complete && img.naturalWidth>0)");
        Console.WriteLine("MULTI FOLDER PASS: real WebView2 imports A then A+B, repeats A, overlaps parent, rescans, filters roots/searches, preserves manual cover and original hashes; stored 2 records / 3 roots.");
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static async Task WaitAsync(WebView2 view, string condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            if (await view.CoreWebView2.ExecuteScriptAsync(condition) == "true") return;
            await Task.Delay(80);
        }
        throw new TimeoutException("Multi-folder UI condition failed: " + condition);
    }
}
