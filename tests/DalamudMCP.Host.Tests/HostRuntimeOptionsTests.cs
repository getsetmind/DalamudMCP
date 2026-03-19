namespace DalamudMCP.Host.Tests;

public sealed class HostRuntimeOptionsTests
{
    [Fact]
    public void TryParse_ReturnsOptions_WhenPipeNameIsProvided()
    {
        var success = HostRuntimeOptions.TryParse(
            ["--pipe-name", "DalamudMCP.TestPipe", "--page-size", "25"],
            out var options,
            out var errorMessage);

        Assert.True(success);
        Assert.NotNull(options);
        Assert.Equal(HostTransportKind.Stdio, options.Transport);
        Assert.Equal("DalamudMCP.TestPipe", options.PipeName);
        Assert.Equal(25, options.PageSize);
        Assert.Null(errorMessage);
    }

    [Fact]
    public void TryParse_ReturnsHttpOptions_WhenRequested()
    {
        var success = HostRuntimeOptions.TryParse(
            ["--pipe-name", "DalamudMCP.TestPipe", "--transport", "http", "--http-port", "39123", "--mcp-path", "rpc"],
            out var options,
            out var errorMessage);

        Assert.True(success);
        Assert.NotNull(options);
        Assert.Equal(HostTransportKind.Http, options!.Transport);
        Assert.Equal("DalamudMCP.TestPipe", options.PipeName);
        Assert.Equal(39123, options.HttpPort);
        Assert.Equal("/rpc", options.McpPath);
        Assert.Null(errorMessage);
    }

    [Fact]
    public void TryParse_ReturnsError_WhenPipeNameIsMissing()
    {
        var success = HostRuntimeOptions.TryParse([], out var options, out var errorMessage);

        Assert.False(success);
        Assert.Null(options);
        Assert.Equal("The --pipe-name argument is required.", errorMessage);
    }

    [Fact]
    public void TryParse_ReturnsError_WhenPageSizeIsInvalid()
    {
        var success = HostRuntimeOptions.TryParse(
            ["--pipe-name", "DalamudMCP.TestPipe", "--page-size", "0"],
            out var options,
            out var errorMessage);

        Assert.False(success);
        Assert.Null(options);
        Assert.Equal("--page-size must be a positive integer.", errorMessage);
    }
}
