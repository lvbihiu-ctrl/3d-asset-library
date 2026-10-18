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
                foreach (var file in directory.EnumerateFiles())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!ModelExtensions.Contains(file.Extension))
                    {
                        continue;
                    }

                    var fullPath = Path.GetFullPath(file.FullName);
                    var relativePath = Path.GetRelativePath(root, fullPath);
                    var relativeFolder = Path.GetDirectoryName(relativePath);
                    var coverPath = FindCover(file);
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
                        coverPath));
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

    private static string? FindCover(FileInfo model)
    {
        var directory = model.DirectoryName;
        if (directory is null)
        {
            return null;
        }

        var baseName = Path.GetFileNameWithoutExtension(model.Name);
        foreach (var extension in CoverExtensions)
        {
            var candidate = Path.Combine(directory, baseName + extension);
            if (File.Exists(candidate))
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
