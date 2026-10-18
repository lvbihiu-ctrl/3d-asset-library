namespace AssetLibrary.Desktop.Bridge;

public static class TrustedContentPolicy
{
    public const string VirtualHostName = "assetlibrary.local";
    public const string StartPage = $"https://{VirtualHostName}/index.html";

    public static bool IsTrustedMessageSource(string? source)
    {
        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return IsMappedOrigin(uri)
            && uri.AbsolutePath.Equals("/index.html", StringComparison.Ordinal)
            && uri.Fragment.Length == 0;
    }

    public static bool IsTrustedResource(string? resourceUrl)
    {
        if (!Uri.TryCreate(resourceUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme.Equals("data", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IsMappedOrigin(uri);
    }

    private static bool IsMappedOrigin(Uri uri) =>
        uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
        && uri.Host.Equals(VirtualHostName, StringComparison.OrdinalIgnoreCase)
        && uri.Port == 443
        && uri.UserInfo.Length == 0;
}
