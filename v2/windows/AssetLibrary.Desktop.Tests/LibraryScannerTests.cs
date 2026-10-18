using System.Security.Cryptography;
using System.Text.Json;
using AssetLibrary.Desktop.Library;

namespace AssetLibrary.Desktop.Tests;

public sealed class LibraryScannerTests : IDisposable
{
    private readonly string _root = Path.Combine(
        AppContext.BaseDirectory,
        "scanner-test-data",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Scan_FindsSupportedModelsAndSameNameCoverWithoutChangingSourceFiles()
    {
        var nested = Directory.CreateDirectory(Path.Combine(_root, "家具", "椅子")).FullName;
        var model = Path.Combine(nested, "休闲椅.c4d");
        var cover = Path.Combine(nested, "休闲椅.png");
        var secondModel = Path.Combine(_root, "展示台.fbx");
        var ignored = Path.Combine(_root, "说明.txt");
        File.WriteAllBytes(model, [1, 2, 3, 4, 5]);
        File.WriteAllBytes(cover, [137, 80, 78, 71, 1, 2, 3]);
        File.WriteAllBytes(secondModel, [9, 8, 7]);
        File.WriteAllText(ignored, "not an asset");
        var before = SnapshotHashes(_root);

        var catalog = new LibraryScanner().Scan(_root);

        Assert.Equal(2, catalog.Assets.Count);
        var chair = Assert.Single(catalog.Assets, asset => asset.FileName == "休闲椅.c4d");
        Assert.Equal("C4D", chair.Extension);
        Assert.True(chair.HasCover);
        Assert.Equal(cover, chair.CoverPath);
        Assert.Equal(Path.Combine("家具", "椅子", "休闲椅.c4d"), chair.RelativePath);
        Assert.Matches("^[0-9a-f]{64}$", chair.Id);
        Assert.DoesNotContain(catalog.Assets, asset => asset.FileName == "说明.txt");
        Assert.Equal(before, SnapshotHashes(_root));
    }

    [Fact]
    public void Scan_ProducesStableIdsAndDoesNotSerializeTrustedAbsolutePaths()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllBytes(Path.Combine(_root, "产品.obj"), [4, 3, 2, 1]);
        var scanner = new LibraryScanner();

        var first = Assert.Single(scanner.Scan(_root).Assets);
        var second = Assert.Single(scanner.Scan(_root).Assets);
        var json = JsonSerializer.Serialize(first);

        Assert.Equal(first.Id, second.Id);
        Assert.DoesNotContain(first.FullPath, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("coverPath", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fullPath", json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(1000)]
    public void Scan_HandlesExpectedAlphaCatalogSizes(int count)
    {
        Directory.CreateDirectory(_root);
        for (var index = 0; index < count; index++)
        {
            File.WriteAllText(Path.Combine(_root, $"asset-{index:D4}.obj"), $"record {index}");
        }

        var catalog = new LibraryScanner().Scan(_root);

        Assert.Equal(count, catalog.Assets.Count);
        Assert.Equal(count, catalog.Assets.Select(asset => asset.Id).Distinct().Count());
    }

    [Fact]
    public void Scan_ReportsVisibleDirectoryAssetAndErrorCounts()
    {
        Directory.CreateDirectory(Path.Combine(_root, "一级", "二级"));
        File.WriteAllText(Path.Combine(_root, "一级", "asset.obj"), "record");
        var updates = new List<LibraryScanProgress>();

        var catalog = new LibraryScanner().Scan(
            _root,
            progress: new SynchronousProgress<LibraryScanProgress>(updates.Add));

        Assert.NotEmpty(updates);
        Assert.Equal(catalog.Assets.Count, updates[^1].AssetCount);
        Assert.Equal(catalog.ErrorCount, updates[^1].ErrorCount);
        Assert.True(updates[^1].DirectoryCount >= 3);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static Dictionary<string, string> SnapshotHashes(string root)
    {
        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                path => Path.GetRelativePath(root, path),
                path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))),
                StringComparer.OrdinalIgnoreCase);
    }

    private sealed class SynchronousProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
