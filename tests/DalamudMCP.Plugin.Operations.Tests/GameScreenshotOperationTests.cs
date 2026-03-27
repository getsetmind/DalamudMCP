using DalamudMCP.Framework;
using DalamudMCP.Protocol;

namespace DalamudMCP.Plugin.Operations.Tests;

public sealed class GameScreenshotOperationTests
{
    [Fact]
    public void GameScreenshotOperation_CarriesCliAndMcpMetadata_OnTheOperationClass()
    {
        Type operationType = typeof(GameScreenshotOperation);

        OperationAttribute? operation = operationType.GetCustomAttribute<OperationAttribute>();
        CliCommandAttribute? cli = operationType.GetCustomAttribute<CliCommandAttribute>();
        McpToolAttribute? mcp = operationType.GetCustomAttribute<McpToolAttribute>();

        Assert.NotNull(operation);
        Assert.NotNull(cli);
        Assert.NotNull(mcp);
        Assert.Equal("game.screenshot", operation.OperationId);
        Assert.Equal(["game", "screenshot"], cli.PathSegments);
        Assert.Equal("capture_game_screenshot", mcp.Name);
    }

    [Fact]
    public void GameScreenshotOperation_RequestCarriesProtocolIdentity()
    {
        ProtocolOperationAttribute? protocol = typeof(GameScreenshotOperation.Request)
            .GetCustomAttribute<ProtocolOperationAttribute>();

        Assert.NotNull(protocol);
        Assert.Equal("game.screenshot", protocol.OperationId);
    }

    [Fact]
    public async Task ExecuteAsync_UsesInjectedExecutor_AndContextCancellation()
    {
        GameScreenshotSnapshot expected = new(
            DateTimeOffset.UtcNow,
            "client",
            @"C:\temp\ffxiv-client.bmp",
            1920,
            1080,
            4096,
            "Captured client screenshot.");
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        CancellationToken observedCancellationToken = default;
        GameScreenshotOperation operation = new(
            (request, cancellation) =>
            {
                observedCancellationToken = cancellation;
                Assert.Equal("client", request.CaptureArea);
                return ValueTask.FromResult(expected);
            });

        GameScreenshotSnapshot actual = await operation.ExecuteAsync(
            new GameScreenshotOperation.Request { CaptureArea = "client" },
            OperationContext.ForCli("game.screenshot", cancellationToken: cancellationToken));

        Assert.Equal(expected, actual);
        Assert.Equal(cancellationToken, observedCancellationToken);
    }
}
