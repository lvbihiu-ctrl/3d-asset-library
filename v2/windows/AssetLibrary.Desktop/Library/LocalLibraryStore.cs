using System.Text.Json;

namespace AssetLibrary.Desktop.Library;

// Version-independent local cache only. Never writes to source assets or the legacy data directory.
public sealed class LocalLibraryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _root;
    private readonly string _covers;
    private Dictionary<string, string> _customCovers;

    public LocalLibraryStore(string directory)
    {
        _root = Path.GetFullPath(directory);
        _covers = Path.Combine(_root, "covers");
        Directory.CreateDirectory(_covers);
        var mapPath = Path.Combine(_root, "covers.json");
        _customCovers = File.Exists(mapPath)
            ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(mapPath))
                ?? throw new InvalidDataException("V2 封面记录无法读取；原文件未修改。")
            : new(StringComparer.Ordinal);
    }

    public LibraryCatalog? Load()
    {
        var file = Path.Combine(_root, "library.json");
        if (!File.Exists(file)) return null;
        var saved = JsonSerializer.Deserialize<SavedCatalog>(File.ReadAllText(file))
            ?? throw new InvalidDataException("V2 素材索引无法读取；请保留该文件以便恢复。");
        var roots = saved.Roots is { Length: > 0 } ? saved.Roots : [new LibraryRoot(saved.Root, saved.RootName)];
        var assets = saved.Assets.Select(entry =>
        {
            var full = Path.GetFullPath(entry.FullPath);
            var sourceRoot = roots.FirstOrDefault(root => root.Id == entry.Asset.SourceRootId && IsUnder(root.Path, full))
                ?? roots.FirstOrDefault(root => IsUnder(root.Path, full));
            if (sourceRoot is null || LibraryScanner.CreateAssetId(full) != entry.Asset.Id)
                throw new InvalidDataException("V2 索引包含不匹配的素材路径。");
            var cover = entry.CoverPath;
            if (cover is not null && !IsUnder(_covers, cover)
                && !string.Equals(Path.GetDirectoryName(full), Path.GetDirectoryName(Path.GetFullPath(cover)), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("V2 索引包含不匹配的封面路径。");
            return entry.Asset with { FullPath = full, CoverPath = cover, HasCover = cover is not null && File.Exists(cover), SourceRootId = sourceRoot.Id, SourceRootName = sourceRoot.Name };
        }).ToArray();
        return WithCustomCovers(new LibraryCatalog(saved.Root, saved.RootName, saved.ScannedAtUtc, 0, saved.ErrorCount, assets, roots));
    }

    public LibraryCatalog SaveCatalog(LibraryCatalog catalog)
    {
        var updated = WithCustomCovers(catalog);
        AtomicJson(Path.Combine(_root, "library.json"), new SavedCatalog(
            updated.Root, updated.RootName, updated.ScannedAtUtc, updated.ErrorCount,
            updated.Assets.Select(asset => new SavedAsset(asset, asset.FullPath, asset.CoverPath)).ToArray(), updated.SourceRoots.ToArray()));
        return updated;
    }

    public LibraryCatalog MergeScans(LibraryCatalog? previous, IReadOnlyList<LibraryCatalog> scans)
    {
        if (scans.Count == 0) return previous ?? throw new ArgumentException("没有可导入的素材文件夹。");
        var roots = (previous?.SourceRoots ?? []).Concat(scans.SelectMany(scan => scan.SourceRoots))
            .GroupBy(root => root.Id, StringComparer.Ordinal).Select(group => group.First()).ToArray();
        var assets = (previous?.Assets ?? []).ToDictionary(asset => asset.Id, StringComparer.Ordinal);
        foreach (var scan in scans)
        foreach (var incoming in scan.Assets)
        {
            var next = incoming;
            if (assets.TryGetValue(incoming.Id, out var existing))
            {
                // Keep prior ownership when parent and child folders overlap; one path, one record.
                var owner = roots.FirstOrDefault(root => root.Id == existing.SourceRootId)
                    ?? roots.First(root => IsUnder(root.Path, existing.FullPath));
                var relative = Path.GetRelativePath(owner.Path, incoming.FullPath);
                next = incoming with { Name = existing.Name, SourceRootId = owner.Id, SourceRootName = owner.Name,
                    RelativePath = relative, Folder = Path.GetDirectoryName(relative) is { Length: > 0 } folder ? folder : "." };
                if (existing.CoverPath is not null && File.Exists(existing.CoverPath))
                    next = next with { CoverPath = existing.CoverPath, HasCover = true };
            }
            assets[incoming.Id] = next;
        }
        return SaveCatalog(new LibraryCatalog(roots[0].Path, roots.Length == 1 ? roots[0].Name : $"{roots.Length} 个素材文件夹",
            DateTimeOffset.UtcNow, scans.Sum(scan => scan.DurationMilliseconds), scans.Sum(scan => scan.ErrorCount),
            assets.Values.OrderBy(asset => asset.SourceRootName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(asset => asset.RelativePath, StringComparer.OrdinalIgnoreCase).ToArray(), roots));
    }

    public LibraryAsset SaveCover(LibraryAsset asset, Image image)
    {
        if (image.Width > 16384 || image.Height > 16384 || (long)image.Width * image.Height > 80_000_000)
            throw new InvalidOperationException("剪贴板图片太大，请先缩小图片。");
        var name = asset.Id + "-" + Guid.NewGuid().ToString("N") + ".png";
        var path = Path.Combine(_covers, name);
        using (var copy = new Bitmap(image)) copy.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        var next = new Dictionary<string, string>(_customCovers, StringComparer.Ordinal) { [asset.Id] = name };
        AtomicJson(Path.Combine(_root, "covers.json"), next);
        _customCovers = next;
        return asset with { HasCover = true, CoverPath = path };
    }

    public LibraryCatalog? ImportLegacy(string legacyDirectory)
    {
        if (File.Exists(Path.Combine(_root, "library.json"))) return Load();
        var legacy = Path.GetFullPath(legacyDirectory);
        var catalogFile = Path.Combine(legacy, "catalog.json");
        if (!File.Exists(catalogFile)) return null;
        using var document = JsonDocument.Parse(File.ReadAllText(catalogFile));
        var source = document.RootElement;
        var root = Path.GetFullPath(source.GetProperty("summary").GetProperty("root").GetString()!);
        var coversFile = Path.Combine(legacy, "covers.json");
        var map = File.Exists(coversFile)
            ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(coversFile)) ?? new()
            : new Dictionary<string, string>();
        // Preserve the small legacy metadata files verbatim, including classifications, before import.
        var backup = Path.Combine(_root, "legacy-backup");
        Directory.CreateDirectory(backup);
        foreach (var name in new[] { "catalog.json", "covers.json", "ui-categories.v1.json" })
            if (File.Exists(Path.Combine(legacy, name))) File.Copy(Path.Combine(legacy, name), Path.Combine(backup, name), true);
        var assets = new List<LibraryAsset>();
        var mappings = new List<object>();
        foreach (var entry in source.GetProperty("assets").EnumerateArray())
        {
            var full = Path.GetFullPath(entry.GetProperty("full").GetString()!);
            if (!IsUnder(root, full)) throw new InvalidDataException("旧索引的素材路径不在已记录根目录内，已停止导入。");
            var id = LibraryScanner.CreateAssetId(full);
            var oldId = entry.GetProperty("id").GetString()!;
            string? cover = null;
            var custom = map.TryGetValue(oldId, out var coverName);
            if (custom && coverName is not null && Path.GetFileName(coverName) == coverName)
                cover = Path.Combine(legacy, "covers", coverName);
            else if (entry.TryGetProperty("preview", out var preview))
            {
                var value = preview.GetString() ?? "";
                var prefix = value.StartsWith("/thumb/", StringComparison.Ordinal) ? "thumbs"
                    : value.StartsWith("/cover/", StringComparison.Ordinal) ? "covers" : null;
                if (prefix is not null && Path.GetFileName(value) == value[(value.LastIndexOf('/') + 1)..])
                    cover = Path.Combine(legacy, prefix, Path.GetFileName(value));
            }
            if (cover is not null && File.Exists(cover))
            {
                // Import cover bytes, not a writable reference to the old application's cover cache.
                var name = id + "-legacy" + Path.GetExtension(cover);
                var target = Path.Combine(_covers, name);
                File.Copy(cover, target, true);
                cover = target;
                if (custom) _customCovers[id] = name;
            }
            else cover = null;
            var relative = Path.GetRelativePath(root, full);
            var info = new FileInfo(full);
            assets.Add(new LibraryAsset(id, entry.GetProperty("name").GetString() ?? info.Name,
                info.Name, info.Extension.TrimStart('.').ToUpperInvariant(), relative,
                Path.GetDirectoryName(relative) is { Length: > 0 } folder ? folder : ".",
                entry.TryGetProperty("bytes", out var bytes) ? bytes.GetInt64() : 0,
                info.Exists ? info.LastWriteTimeUtc : DateTimeOffset.MinValue,
                cover is not null, full, cover));
            mappings.Add(new { legacyId = oldId, v2Id = id });
        }
        if (assets.Select(asset => asset.Id).Distinct().Count() != assets.Count)
            throw new InvalidDataException("旧索引存在重复路径，已保留旧数据并停止自动导入。");
        AtomicJson(Path.Combine(_root, "legacy-id-map.json"), mappings);
        AtomicJson(Path.Combine(_root, "covers.json"), _customCovers);
        return SaveCatalog(new LibraryCatalog(root, new DirectoryInfo(root).Name, DateTimeOffset.UtcNow, 0, 0, assets));
    }

    private LibraryCatalog WithCustomCovers(LibraryCatalog catalog) => catalog with
    {
        Assets = catalog.Assets.Select(asset =>
        {
            asset = asset with { SourceRootIds = catalog.SourceRoots.Where(root => IsUnder(root.Path, asset.FullPath)).Select(root => root.Id).ToArray() };
            if (!_customCovers.TryGetValue(asset.Id, out var name) || Path.GetFileName(name) != name) return asset;
            var path = Path.Combine(_covers, name);
            return File.Exists(path) ? asset with { HasCover = true, CoverPath = path } : asset;
        }).ToArray(),
    };

    private static bool IsUnder(string root, string file) => Path.GetFullPath(file).StartsWith(
        Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static void AtomicJson<T>(string path, T value)
    {
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(value, JsonOptions));
        File.Move(temp, path, true);
    }

    private sealed record SavedAsset(LibraryAsset Asset, string FullPath, string? CoverPath);
    private sealed record SavedCatalog(string Root, string RootName, DateTimeOffset ScannedAtUtc, int ErrorCount, SavedAsset[] Assets, LibraryRoot[]? Roots = null);
}
