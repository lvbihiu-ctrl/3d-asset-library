namespace AssetLibrary.Desktop.Library;

public sealed class LibrarySession
{
    private readonly object _gate = new();
    private Dictionary<string, LibraryAsset> _assets = new(StringComparer.Ordinal);
    public LibraryCatalog? Catalog { get; private set; }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _assets.Count;
            }
        }
    }

    public void Replace(LibraryCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        lock (_gate)
        {
            Catalog = catalog;
            _assets = catalog.Assets.ToDictionary(asset => asset.Id, StringComparer.Ordinal);
        }
    }

    public bool TryGetAsset(string assetId, out LibraryAsset? asset)
    {
        lock (_gate)
        {
            return _assets.TryGetValue(assetId, out asset);
        }
    }

    public bool TryGetCover(string assetId, out string? coverPath)
    {
        coverPath = null;
        if (!TryGetAsset(assetId, out var asset) || asset?.CoverPath is null)
        {
            return false;
        }

        coverPath = asset.CoverPath;
        return File.Exists(coverPath);
    }
}
