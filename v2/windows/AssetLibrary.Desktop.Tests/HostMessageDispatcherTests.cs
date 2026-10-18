using System.Text.Json;
using AssetLibrary.Desktop.Bridge;

namespace AssetLibrary.Desktop.Tests;

public sealed class HostMessageDispatcherTests
{
    private static readonly HostInfo TestHost = new(
        ProductVersion: "2.0.0-preview.14",
        Framework: ".NET 10",
        Architecture: "X64");

    private readonly HostMessageDispatcher _dispatcher = new(() => TestHost);

    [Fact]
    public void GetHostInfo_ReturnsOnlyTheAdvertisedCapability()
    {
        using var response = Dispatch("""
            {"id":"request-1","action":"getHostInfo","payload":{}}
            """);

        Assert.True(response.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("request-1", response.RootElement.GetProperty("id").GetString());
        var result = response.RootElement.GetProperty("result");
        Assert.Equal("2.0.0-preview.14", result.GetProperty("productVersion").GetString());
        Assert.Equal(["getHostInfo"], result.GetProperty("capabilities").EnumerateArray().Select(x => x.GetString()));
    }

    [Fact]
    public void UnknownAction_IsRejectedInsteadOfBeingExecuted()
    {
        using var response = Dispatch("""
            {"id":"request-2","action":"runCommand","payload":{"command":"calc.exe"}}
            """);

        Assert.False(response.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("action_not_allowed", ErrorCode(response));
    }

    [Theory]
    [InlineData("not json", "invalid_message")]
    [InlineData("{\"id\":\"bad id\",\"action\":\"getHostInfo\",\"payload\":{}}", "invalid_id")]
    [InlineData("{\"id\":\"request-3\",\"action\":\"getHostInfo\",\"payload\":{\"path\":\"D:\\\\private\"}}", "invalid_payload")]
    public void InvalidMessages_ReturnExplicitErrors(string request, string expectedCode)
    {
        using var response = Dispatch(request);

        Assert.False(response.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal(expectedCode, ErrorCode(response));
    }

    [Fact]
    public void OversizedMessage_IsRejectedBeforeParsing()
    {
        var responseText = _dispatcher.Dispatch(new string('x', HostMessageDispatcher.MaxMessageCharacters + 1));
        using var response = JsonDocument.Parse(responseText);

        Assert.False(response.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("message_too_large", ErrorCode(response));
    }

    [Fact]
    public void NativeHandlerFailure_ReturnsStableExplicitError()
    {
        var dispatcher = new HostMessageDispatcher(
            () => throw new InvalidOperationException("private implementation detail"));

        using var response = JsonDocument.Parse(dispatcher.Dispatch("""
            {"id":"request-4","action":"getHostInfo","payload":{}}
            """));

        Assert.False(response.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("request-4", response.RootElement.GetProperty("id").GetString());
        Assert.Equal("native_error", ErrorCode(response));
        Assert.DoesNotContain("private implementation detail", response.RootElement.GetRawText());
    }

    private JsonDocument Dispatch(string request) => JsonDocument.Parse(_dispatcher.Dispatch(request));

    private static string? ErrorCode(JsonDocument response) =>
        response.RootElement.GetProperty("error").GetProperty("code").GetString();
}
