using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace AssetLibrary.Desktop.Library;

public sealed class LibraryScanner
{
    private static readonly HashSet<string> ModelExtensions = new(
        [".c4d", ".fbx", ".obj"],
        StringComparer.OrdinalIgnoreCase);

    private static readonly string[] CoverExtensions =
        [".png", ".jpg", ".jpeg", ".webp", ".bmp"];

    public LibraryCatalog Scan(
        string rootPath,
        CancellationToken cancellationToken = default,
        IProgress<LibraryScanProgress>? progress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        var root = Path.GetFullPath(rootPath);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException("选择的素材文件夹不存在或当前无法访问。");
        }

        var stopwatch = Stopwatch.StartNew();
        var assets = new List<LibraryAsset>();
        var errors = 0;
        var directories = 0;
        var pending = new Stack<DirectoryInfo>();
        pending.Push(new DirectoryInfo(root));

        progress?.Report(new LibraryScanProgress(0, 0, 0, "."));

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pending.Pop();
            directories++;

            try
            {
                var files = directory.GetFiles();
                var folderModels = files.Count(file => ModelExtensions.Contains(file.Extension));
                var images = files.Where(file => CoverExtensions.Contains(file.Extension, StringComparer.OrdinalIgnoreCase))
                    .ToDictionary(file => file.Name, file => file.FullName, StringComparer.OrdinalIgnoreCase);
                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!ModelExtensions.Contains(file.Extension))
                    {
                        continue;
                    }

                    var fullPath = Path.GetFullPath(file.FullName);
                    var relativePath = Path.GetRelativePath(root, fullPath);
                    var relativeFolder = Path.GetDirectoryName(relativePath);
                    var coverPath = FindCover(file, images, folderModels == 1);
                    assets.Add(new LibraryAsset(
                        CreateAssetId(fullPath),
                        Path.GetFileNameWithoutExtension(file.Name),
                        file.Name,
                        file.Extension.TrimStart('.').ToUpperInvariant(),
                        relativePath,
                        string.IsNullOrWhiteSpace(relativeFolder) ? "." : relativeFolder,
                        file.Length,
                        file.LastWriteTimeUtc,
                        coverPath is not null,
                        fullPath,
                        coverPath,
                        CreateAssetId(root),
                        new DirectoryInfo(root).Name));
                }
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
            {
                errors++;
            }

            try
            {
                foreach (var child in directory.EnumerateDirectories())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if ((child.Attributes & FileAttributes.ReparsePoint) == 0)
                    {
                        pending.Push(child);
                    }
                }
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
            {
                errors++;
            }

            progress?.Report(new LibraryScanProgress(
                directories,
                assets.Count,
                errors,
                Path.GetRelativePath(root, directory.FullName)));
        }

        stopwatch.Stop();
        assets.Sort((left, right) =>
            StringComparer.OrdinalIgnoreCase.Compare(left.RelativePath, right.RelativePath));

        return new LibraryCatalog(
            root,
            new DirectoryInfo(root).Name,
            DateTimeOffset.UtcNow,
            stopwatch.ElapsedMilliseconds,
            errors,
            assets);
    }

    private static string? FindCover(FileInfo model, IReadOnlyDictionary<string, string> images, bool onlyModel)
    {
        var directory = model.DirectoryName;
        if (directory is null)
        {
            return null;
        }

        var baseName = Path.GetFileNameWithoutExtension(model.Name);
        var names = new List<string> { baseName };
        names.AddRange(new[] { "_preview", "-preview", ".preview", "_cover", "-cover", "_thumb", "-thumb", "_thumbnail", "_render", "_预览", "_封面" }.Select(suffix => baseName + suffix));
        // Generic/folder-named previews are only unambiguous in a one-model folder.
        if (onlyModel) names.AddRange([model.Directory!.Name, "preview", "cover", "thumbnail", "预览", "封面"]);
        foreach (var name in names.Distinct(StringComparer.OrdinalIgnoreCase))
        foreach (var extension in CoverExtensions)
        {
            if (images.TryGetValue(name + extension, out var candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return null;
    }

    public static string CreateAssetId(string fullPath)
    {
        var normalized = Path.GetFullPath(fullPath).TrimEnd(Path.DirectorySeparatorChar).ToUpperInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
    }
}
