using DalamudMCP.Protocol;
using Manifold;

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

    [Fact]
    public void Bitmap_conversion_returns_valid_png_image_content()
    {
        string path = Path.Combine(Path.GetTempPath(), $"DalamudMCP-{Guid.NewGuid():N}.bmp");
        try
        {
            byte[] bitmap = new byte[58];
            bitmap[0] = (byte)'B';
            bitmap[1] = (byte)'M';
            BitConverter.GetBytes(58).CopyTo(bitmap, 2);
            BitConverter.GetBytes(54).CopyTo(bitmap, 10);
            BitConverter.GetBytes(40).CopyTo(bitmap, 14);
            BitConverter.GetBytes(1).CopyTo(bitmap, 18);
            BitConverter.GetBytes(1).CopyTo(bitmap, 22);
            BitConverter.GetBytes((ushort)1).CopyTo(bitmap, 26);
            BitConverter.GetBytes((ushort)32).CopyTo(bitmap, 28);
            bitmap[54] = 0x33;
            bitmap[55] = 0x22;
            bitmap[56] = 0x11;
            bitmap[57] = 0xFF;
            File.WriteAllBytes(path, bitmap);

            byte[] png = GameScreenshotOperation.WindowBitmapCaptureHelper.ConvertBitmapFileToPng(
                path,
                1,
                1,
                out int width,
                out int height);

            Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, png[..8]);
            Assert.Equal(1, width);
            Assert.Equal(1, height);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
