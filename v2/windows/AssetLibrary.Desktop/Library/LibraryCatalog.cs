namespace AssetLibrary.Desktop.Library;

public sealed record LibraryCatalog(
    string Root,
    string RootName,
    DateTimeOffset ScannedAtUtc,
    long DurationMilliseconds,
    int ErrorCount,
    IReadOnlyList<LibraryAsset> Assets,
    IReadOnlyList<LibraryRoot>? Roots = null)
{
    public IReadOnlyList<LibraryRoot> SourceRoots => Roots is { Count: > 0 } ? Roots : [new(Root, RootName)];
    public bool RootAvailable => SourceRoots.All(root => root.Available);
}

public sealed record LibraryRoot(string Path, string Name)
{
    public string Id => LibraryScanner.CreateAssetId(Path);
    public bool Available => Directory.Exists(Path);
}

public sealed record ImportLibraryResult(bool Cancelled, LibraryCatalog? Catalog, int AddedCount = 0, int NewCoverCount = 0);

public sealed record LibraryScanProgress(
    int DirectoryCount,
    int AssetCount,
    int ErrorCount,
    string CurrentFolder);
