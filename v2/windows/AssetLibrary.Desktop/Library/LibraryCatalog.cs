namespace AssetLibrary.Desktop.Library;

public sealed record LibraryCatalog(
    string Root,
    string RootName,
    DateTimeOffset ScannedAtUtc,
    long DurationMilliseconds,
    int ErrorCount,
    IReadOnlyList<LibraryAsset> Assets);

public sealed record ImportLibraryResult(bool Cancelled, LibraryCatalog? Catalog);

