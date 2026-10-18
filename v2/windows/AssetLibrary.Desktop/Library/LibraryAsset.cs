using System.Text.Json.Serialization;

namespace AssetLibrary.Desktop.Library;

public sealed record LibraryAsset(
    string Id,
    string Name,
    string FileName,
    string Extension,
    string RelativePath,
    string Folder,
    long SizeBytes,
    DateTimeOffset ModifiedUtc,
    bool HasCover,
    [property: JsonIgnore] string FullPath,
    [property: JsonIgnore] string? CoverPath,
    string SourceRootId = "",
    string SourceRootName = "",
    IReadOnlyList<string>? SourceRootIds = null);
