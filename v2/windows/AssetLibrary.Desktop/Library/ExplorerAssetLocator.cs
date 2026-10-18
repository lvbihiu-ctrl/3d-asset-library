using System.Diagnostics;

namespace AssetLibrary.Desktop.Library;

public static class ExplorerAssetLocator
{
    public static bool Locate(LibrarySession session, string assetId)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!session.TryGetAsset(assetId, out var asset) || asset is null)
        {
            return false;
        }

        // RelativePath is intentionally not accepted from the page. MainWindow supplies
        // the trusted absolute path from its current native catalog instead.
        return StartExplorer(asset.FullPath);
    }

    public static bool StartExplorer(string trustedFullPath)
    {
        if (!Path.IsPathFullyQualified(trustedFullPath) || !File.Exists(trustedFullPath))
        {
            return false;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{trustedFullPath.Replace("\"", string.Empty, StringComparison.Ordinal)}\"",
            UseShellExecute = true,
        });
        return true;
    }
}
