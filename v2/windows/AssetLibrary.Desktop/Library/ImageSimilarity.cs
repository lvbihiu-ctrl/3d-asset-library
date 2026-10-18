using System.Drawing.Drawing2D;
using System.Numerics;
using System.Runtime.InteropServices;

namespace AssetLibrary.Desktop.Library;

public sealed record ImageMatch(string AssetId, int Distance);
public sealed record ImageSearchResult(IReadOnlyList<ImageMatch> Matches, int Skipped, string Algorithm = "dHash · 基础视觉相似度，非语义识别");

public sealed class ImageSimilarity
{
    private readonly Dictionary<string, (long Length, DateTime Modified, ulong Hash)> _cache = new(StringComparer.OrdinalIgnoreCase);

    // Matches the stable client's 9x8 dHash; not an AI/semantic search model.
    public static ulong Hash(Image image)
    {
        using var small = new Bitmap(9, 8);
        using (var graphics = Graphics.FromImage(small))
        {
            graphics.Clear(Color.White);
            graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
            graphics.DrawImage(image, 0, 0, 9, 8);
        }
        ulong result = 0;
        for (var y = 0; y < 8; y++)
            for (var x = 0; x < 8; x++)
            {
                var a = small.GetPixel(x, y);
                var b = small.GetPixel(x + 1, y);
                result = (result << 1) | (a.R * 299 + a.G * 587 + a.B * 114 > b.R * 299 + b.G * 587 + b.B * 114 ? 1UL : 0UL);
            }
        return result;
    }

    public ImageSearchResult Search(Image query, IReadOnlyList<LibraryAsset> assets)
    {
        var target = Hash(query);
        var matches = new List<ImageMatch>();
        var skipped = 0;
        foreach (var asset in assets)
        {
            try
            {
                if (asset.CoverPath is null || !File.Exists(asset.CoverPath)) { skipped++; continue; }
                var file = new FileInfo(asset.CoverPath);
                if (!_cache.TryGetValue(file.FullName, out var cached)
                    || cached.Length != file.Length || cached.Modified != file.LastWriteTimeUtc)
                {
                    using var image = Image.FromFile(file.FullName);
                    cached = (file.Length, file.LastWriteTimeUtc, Hash(image));
                    _cache[file.FullName] = cached;
                }
                matches.Add(new ImageMatch(asset.Id, BitOperations.PopCount(cached.Hash ^ target)));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or OutOfMemoryException or ExternalException)
            {
                skipped++;
            }
        }
        return new ImageSearchResult(matches.OrderBy(match => match.Distance).ThenBy(match => match.AssetId, StringComparer.Ordinal).ToArray(), skipped);
    }
}
