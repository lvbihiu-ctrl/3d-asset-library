using AssetLibrary.Desktop.Bridge;

namespace AssetLibrary.Desktop.Tests;

public sealed class TrustedContentPolicyTests
{
    [Theory]
    [InlineData("https://assetlibrary.local/index.html", true)]
    [InlineData("https://assetlibrary.local/index.html?preview=1", true)]
    [InlineData("http://assetlibrary.local/index.html", false)]
    [InlineData("https://assetlibrary.local.evil.example/index.html", false)]
    [InlineData("https://example.com/index.html", false)]
    [InlineData("file:///D:/Codex/index.html", false)]
    [InlineData("not-a-uri", false)]
    public void OnlyTheMappedLocalDocumentMaySendHostMessages(string source, bool expected)
    {
        Assert.Equal(expected, TrustedContentPolicy.IsTrustedMessageSource(source));
    }

    [Theory]
    [InlineData("https://assetlibrary.local/index.html", true)]
    [InlineData("https://assetlibrary.local/styles/app.css", true)]
    [InlineData("https://assetlibrary.local.evil.example/app.js", false)]
    [InlineData("https://example.com/image.png", false)]
    [InlineData("http://assetlibrary.local/index.html", false)]
    [InlineData("data:image/svg+xml;charset=utf-8,%3Csvg%3E%3C/svg%3E", true)]
    [InlineData("file:///D:/private/model.c4d", false)]
    [InlineData("https://assetlibrary-covers.local/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", true)]
    [InlineData("https://assetlibrary-covers.local/model.c4d", false)]
    [InlineData("https://assetlibrary-covers.local:444/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", false)]
    [InlineData("https://user@assetlibrary-covers.local/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", false)]
    [InlineData("https://assetlibrary-covers.local/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa?path=D:/private", false)]
    [InlineData("http://assetlibrary-covers.local/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", false)]
    public void OnlyMappedLocalResourcesMayLoad(string resourceUrl, bool expected)
    {
        Assert.Equal(expected, TrustedContentPolicy.IsTrustedResource(resourceUrl));
    }
}
