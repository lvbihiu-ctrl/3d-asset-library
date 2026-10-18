using System.Drawing;
using System.Security.Cryptography;
using System.Text.Json;
using AssetLibrary.Desktop.Library;

namespace AssetLibrary.Desktop.Tests;

public sealed class LocalLibraryStoreTests
{
    private readonly string _root = Path.Combine(AppContext.BaseDirectory, "store-test-data", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Cover_SurvivesNewStoreAndRescan_WithoutChangingOriginals()
    {
        var models = Directory.CreateDirectory(Path.Combine(_root, "models")).FullName;
        var data = Path.Combine(_root, "data");
        var model = Path.Combine(models, "chair.c4d");
        File.WriteAllText(model, "original C4D fixture");
        var sourceHash = SHA256.HashData(File.ReadAllBytes(model));
        var store = new LocalLibraryStore(data);
        var catalog = store.SaveCatalog(new LibraryScanner().Scan(models));
        using var cover = Gradient(false);
        var saved = store.SaveCover(catalog.Assets[0], cover);
        Assert.True(File.Exists(saved.CoverPath));
        var fresh = new LocalLibraryStore(data);
        Assert.True(Assert.Single(fresh.Load()!.Assets).HasCover);
        Assert.Equal(saved.CoverPath, Assert.Single(fresh.SaveCatalog(new LibraryScanner().Scan(models)).Assets).CoverPath);
        Assert.Equal(sourceHash, SHA256.HashData(File.ReadAllBytes(model)));
        Assert.Single(Directory.GetFiles(models));
    }

    [Fact]
    public void LegacyImport_CopiesBothCoversAndIdMap_AndLeavesLegacyDataUntouched()
    {
        var models = Directory.CreateDirectory(Path.Combine(_root, "models")).FullName;
        var legacy = Directory.CreateDirectory(Path.Combine(_root, "legacy")).FullName;
        Directory.CreateDirectory(Path.Combine(legacy, "covers"));
        var full = Path.Combine(models, "chair.max");
        File.WriteAllText(full, "original fixture");
        using (var image = Gradient(false)) image.Save(Path.Combine(legacy, "covers", "old.jpg"), System.Drawing.Imaging.ImageFormat.Jpeg);
        File.WriteAllText(Path.Combine(legacy, "covers.json"), "{\"old-id\":\"old.jpg\"}");
        File.WriteAllText(Path.Combine(legacy, "ui-categories.v1.json"), "{\"custom\":\"retained\"}");
        File.WriteAllText(Path.Combine(legacy, "catalog.json"), JsonSerializer.Serialize(new
        {
            summary = new { root = models },
            assets = new[] { new { id = "old-id", name = "chair", full, bytes = 16, preview = "" } },
        }));
        var hashes = Directory.GetFiles(legacy, "*", SearchOption.AllDirectories).ToDictionary(path => path, path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
        var data = Path.Combine(_root, "data");
        var store = new LocalLibraryStore(data);
        var catalog = store.ImportLegacy(legacy)!;
        var asset = Assert.Single(catalog.Assets);
        Assert.Equal("MAX", asset.Extension);
        Assert.Equal(LibraryScanner.CreateAssetId(full), asset.Id);
        Assert.True(asset.HasCover);
        Assert.StartsWith(data, asset.CoverPath!);
        Assert.True(File.Exists(Path.Combine(data, "legacy-id-map.json")));
        Assert.Equal("{\"custom\":\"retained\"}", File.ReadAllText(Path.Combine(data, "legacy-backup", "ui-categories.v1.json")));
        Assert.Equal(catalog.Assets[0].Id, Assert.Single(new LocalLibraryStore(data).Load()!.Assets).Id);
        foreach (var (file, hash) in hashes) Assert.Equal(hash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))));
    }

    [Fact]
    public void Similarity_ExactMatchingCoverRanksFirst_AndInvalidCoverIsSkipped()
    {
        var models = Directory.CreateDirectory(Path.Combine(_root, "models")).FullName;
        File.WriteAllText(Path.Combine(models, "a.obj"), "a");
        File.WriteAllText(Path.Combine(models, "b.obj"), "b");
        File.WriteAllText(Path.Combine(models, "c.obj"), "c");
        using var first = Gradient(false);
        using var second = Gradient(true);
        first.Save(Path.Combine(models, "a.png"), System.Drawing.Imaging.ImageFormat.Png);
        second.Save(Path.Combine(models, "b.png"), System.Drawing.Imaging.ImageFormat.Png);
        File.WriteAllText(Path.Combine(models, "c.png"), "not an image");
        var assets = new LibraryScanner().Scan(models).Assets;
        var search = new ImageSimilarity();
        var result = search.Search(first, assets);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(2, result.Matches.Count);
        Assert.Equal(assets[0].Id, result.Matches[0].AssetId);
        Assert.Equal(0, result.Matches[0].Distance);
        Assert.True(result.Matches[1].Distance > 0);
        Assert.Equal(assets[1].Id, search.Search(second, assets).Matches[0].AssetId);
    }

    [Fact]
    public void LegacyImport_RejectsAssetOutsideRecordedRoot()
    {
        var legacy = Directory.CreateDirectory(Path.Combine(_root, "legacy")).FullName;
        var models = Directory.CreateDirectory(Path.Combine(_root, "models")).FullName;
        File.WriteAllText(Path.Combine(legacy, "catalog.json"), JsonSerializer.Serialize(new
        {
            summary = new { root = models }, assets = new[] { new { id = "bad", full = Path.Combine(_root, "outside.c4d") } },
        }));
        Assert.Throws<InvalidDataException>(() => new LocalLibraryStore(Path.Combine(_root, "data")).ImportLegacy(legacy));
    }

    private static Bitmap Gradient(bool reverse)
    {
        var bitmap = new Bitmap(90, 80);
        for (var y = 0; y < 80; y++) for (var x = 0; x < 90; x++)
        {
            var value = reverse ? x * 255 / 90 : 255 - x * 255 / 90;
            bitmap.SetPixel(x, y, Color.FromArgb(value, value, value));
        }
        return bitmap;
    }
}
