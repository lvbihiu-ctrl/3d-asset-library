namespace AssetLibrary.Desktop.Library;

public sealed record LibraryCatalog(
    string Root,
    string RootName,
    DateTimeOffset ScannedAtUtc,
    long DurationMilliseconds,
    int ErrorCount,
    IReadOnlyList<LibraryAsset> Assets)
{
    public bool RootAvailable => Directory.Exists(Root);
}

public sealed record ImportLibraryResult(bool Cancelled, LibraryCatalog? Catalog);

public sealed record LibraryScanProgress(
    int DirectoryCount,
    int AssetCount,
    int ErrorCount,
    string CurrentFolder);
