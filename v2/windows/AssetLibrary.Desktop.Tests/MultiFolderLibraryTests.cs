using System.Drawing;
using System.Security.Cryptography;
using AssetLibrary.Desktop.Library;

namespace AssetLibrary.Desktop.Tests;

public sealed class MultiFolderLibraryTests : IDisposable
{
    private readonly string _root = Path.Combine(AppContext.BaseDirectory, "multi-folder-tests", Guid.NewGuid().ToString("N"));
    private string Folder(string name) => Directory.CreateDirectory(Path.Combine(_root, name)).FullName;
    private static void Image(string path) { using var image = new Bitmap(32, 24); image.Save(path, System.Drawing.Imaging.ImageFormat.Png); }

    [Fact]
    public void AppendRepeatOverlapAndRestartKeepOneRecordPerPathAndAllRoots()
    {
        var a = Folder("A"); var b = Folder("B");
        var child = Folder(Path.Combine("A", "child"));
        File.WriteAllText(Path.Combine(a, "a.c4d"), "original a");
        File.WriteAllText(Path.Combine(child, "child.obj"), "original child");
        File.WriteAllText(Path.Combine(b, "a.c4d"), "different path, same name");
        Image(Path.Combine(a, "a.png")); Image(Path.Combine(b, "a_preview.PNG"));
        var scanner = new LibraryScanner();
        var store = new LocalLibraryStore(Folder("data"));
        var first = store.MergeScans(null, [scanner.Scan(a)]);
        var all = store.MergeScans(first, [scanner.Scan(b), scanner.Scan(a), scanner.Scan(child)]);
        Assert.Equal(3, all.Assets.Count);
        Assert.Equal(3, all.Assets.Select(asset => asset.Id).Distinct().Count());
        Assert.Equal(3, all.SourceRoots.Count);
        Assert.All(all.Assets.Where(asset => asset.FullPath.StartsWith(a, StringComparison.OrdinalIgnoreCase)),
            asset => Assert.Contains(LibraryScanner.CreateAssetId(a), asset.SourceRootIds!));
        Assert.Equal(first.Assets[0].SourceRootId, all.Assets.Single(asset => asset.Id == first.Assets[0].Id).SourceRootId);
        Assert.Equal(2, all.Assets.Count(asset => asset.HasCover));
        var restored = new LocalLibraryStore(Path.Combine(_root, "data")).Load()!;
        Assert.Equal(3, restored.SourceRoots.Count);
        Assert.Equal(all.Assets.Select(asset => asset.Id), restored.Assets.Select(asset => asset.Id));
        Directory.Move(b, b + "-offline");
        var offline = store.MergeScans(restored, [scanner.Scan(a)]);
        Assert.Equal(3, offline.Assets.Count);
        Assert.Single(offline.SourceRoots, root => !root.Available);
    }

    [Fact]
    public void ExistingAutomaticAndManualCoversSurviveNewMatchingImagesAndRescans()
    {
        var a = Folder("models");
        var file = Path.Combine(a, "chair.c4d"); File.WriteAllText(file, "original");
        var preview = Path.Combine(a, "chair_preview.png"); Image(preview);
        var scanner = new LibraryScanner(); var store = new LocalLibraryStore(Folder("data"));
        var initial = store.MergeScans(null, [scanner.Scan(a)]);
        Image(Path.Combine(a, "chair.png"));
        var hashes = Directory.GetFiles(a).ToDictionary(path => path, path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
        var rescan = store.MergeScans(initial, [scanner.Scan(a)]);
        Assert.Equal(preview, Assert.Single(rescan.Assets).CoverPath);
        using var manual = new Bitmap(40, 30);
        var custom = store.SaveCover(rescan.Assets[0], manual);
        var coverHash = SHA256.HashData(File.ReadAllBytes(custom.CoverPath!));
        var restored = new LocalLibraryStore(Path.Combine(_root, "data"));
        var final = restored.MergeScans(restored.Load(), [scanner.Scan(a)]);
        Assert.Equal(custom.CoverPath, Assert.Single(final.Assets).CoverPath);
        Assert.Equal(coverHash, SHA256.HashData(File.ReadAllBytes(custom.CoverPath!)));
        foreach (var (path, hash) in hashes) Assert.Equal(hash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
    }

    [Fact]
    public void CoverMatchingIsDeterministicAndDoesNotGuessTexturesInMultiModelFolders()
    {
        var a = Folder("one"); var b = Folder("many");
        File.WriteAllText(Path.Combine(a, "a.obj"), "one"); Image(Path.Combine(a, "cover.png"));
        File.WriteAllText(Path.Combine(b, "a.obj"), "a"); File.WriteAllText(Path.Combine(b, "b.obj"), "b");
        Image(Path.Combine(b, "cover.png")); Image(Path.Combine(b, "diffuse.png"));
        var scanner = new LibraryScanner();
        Assert.True(Assert.Single(scanner.Scan(a).Assets).HasCover);
        Assert.All(scanner.Scan(b).Assets, asset => Assert.False(asset.HasCover));
        Image(Path.Combine(b, "a_preview.PNG")); Image(Path.Combine(b, "a.png"));
        Assert.EndsWith("a.png", scanner.Scan(b).Assets.Single(asset => asset.Name == "a").CoverPath);
        Assert.False(scanner.Scan(b).Assets.Single(asset => asset.Name == "b").HasCover);
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
