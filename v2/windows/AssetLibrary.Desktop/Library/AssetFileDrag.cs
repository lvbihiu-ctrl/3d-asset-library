using System.Collections.Specialized;

namespace AssetLibrary.Desktop.Library;

public static class AssetFileDrag
{
    public static DataObject CreatePayload(LibrarySession session, string assetId)
    {
        if (!session.TryGetAsset(assetId, out var asset) || asset is null || !File.Exists(asset.FullPath))
            throw new InvalidOperationException("原模型不存在或磁盘未连接，请重新扫描后再拖出。");
        var files = new StringCollection { Path.GetFullPath(asset.FullPath) };
        var payload = new DataObject();
        payload.SetFileDropList(files);
        return payload;
    }
}
