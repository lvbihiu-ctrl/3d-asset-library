namespace AssetLibrary.Desktop.Library;

// Explicit project-cover folders only. Never treats a texture/random sibling image as a model render.
internal static class ProjectPreviewFinder
{
    private static readonly string[] FolderNames = ["00_封面Cover", "封面Cover", "封面", "Cover", "Covers", "Preview", "Previews", "预览"];
    private static readonly HashSet<string> Extensions = new([".jpg", ".jpeg", ".png", ".webp", ".bmp"], StringComparer.OrdinalIgnoreCase);

    public static string? Find(string modelPath, string root, Dictionary<string, string?> cache)
    {
        var boundary = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var prefix = boundary.EndsWith(Path.DirectorySeparatorChar) ? boundary : boundary + Path.DirectorySeparatorChar;
        for (var current = Path.GetDirectoryName(modelPath); current is not null; current = Path.GetDirectoryName(current))
        {
            if (!string.Equals(current, boundary, StringComparison.OrdinalIgnoreCase)
                && !current.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) break;
            if (!cache.TryGetValue(current, out var preview))
            {
                preview = FindLocal(current);
                cache[current] = preview;
            }
            if (preview is not null) return preview;
            if (string.Equals(current, boundary, StringComparison.OrdinalIgnoreCase)) break;
        }
        return null;
    }

    private static string? FindLocal(string directory)
    {
        foreach (var name in FolderNames)
        {
            var folder = Path.Combine(directory, name);
            if (!Directory.Exists(folder)) continue;
            try
            {
                if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0) continue;
                var images = Directory.EnumerateFiles(folder).Where(path => Extensions.Contains(Path.GetExtension(path))).Take(100).ToArray();
                var named = images.Where(path => new[] { "manual_cover", "cover", "preview", "封面" }.Contains(Path.GetFileNameWithoutExtension(path), StringComparer.OrdinalIgnoreCase))
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
                if (named is not null) return named;
                if (images.Length == 1) return images[0];
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        }
        return null;
    }
}
