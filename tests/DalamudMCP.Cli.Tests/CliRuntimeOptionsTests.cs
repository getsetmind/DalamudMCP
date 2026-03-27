namespace DalamudMCP.Cli.Tests;

[Collection(DiscoveryEnvironmentSerialGroup.Name)]
public sealed class CliRuntimeOptionsTests : IDisposable
{
    private readonly DiscoveryEnvironmentScope scope = new();

    [Fact]
    public void TryParse_recognizes_direct_cli_mode()
    {
        bool parsed = CliRuntimeOptions.TryParse(["session", "status", "--json"], out CliRuntimeOptions? options, out string? errorMessage);

        Assert.True(parsed);
        Assert.NotNull(options);
        Assert.Null(errorMessage);
        Assert.Equal(CliCommandMode.DirectCli, options!.Mode);
        Assert.Equal(["session", "status", "--json"], options.CommandArguments);
        Assert.Null(options.PipeName);
    }

    [Fact]
    public void TryParse_recognizes_serve_mcp_mode()
    {
        bool parsed = CliRuntimeOptions.TryParse(["serve", "mcp"], out CliRuntimeOptions? options, out string? errorMessage);

        Assert.True(parsed);
        Assert.NotNull(options);
        Assert.Null(errorMessage);
        Assert.Equal(CliCommandMode.ServeMcp, options!.Mode);
        Assert.Empty(options.CommandArguments);
        Assert.Null(options.PipeName);
    }

    [Fact]
    public void TryParse_extracts_pipe_for_direct_cli_mode()
    {
        bool parsed = CliRuntimeOptions.TryParse(["--pipe", "DalamudMCP.1234", "player", "context"], out CliRuntimeOptions? options, out string? errorMessage);

        Assert.True(parsed);
        Assert.NotNull(options);
        Assert.Null(errorMessage);
        Assert.Equal(CliCommandMode.DirectCli, options!.Mode);
        Assert.Equal(["player", "context"], options.CommandArguments);
        Assert.Equal("DalamudMCP.1234", options.PipeName);
    }

    [Fact]
    public void TryParse_extracts_pipe_for_serve_mcp_mode()
    {
        bool parsed = CliRuntimeOptions.TryParse(["serve", "mcp", "--pipe", "DalamudMCP.1234"], out CliRuntimeOptions? options, out string? errorMessage);

        Assert.True(parsed);
        Assert.NotNull(options);
        Assert.Null(errorMessage);
        Assert.Equal(CliCommandMode.ServeMcp, options!.Mode);
        Assert.Empty(options.CommandArguments);
        Assert.Equal("DalamudMCP.1234", options.PipeName);
    }

    [Fact]
    public void TryParse_recognizes_serve_http_mode_with_defaults()
    {
        bool parsed = CliRuntimeOptions.TryParse(["--pipe", "DalamudMCP.1234", "serve", "http"], out CliRuntimeOptions? options, out string? errorMessage);

        Assert.True(parsed);
        Assert.NotNull(options);
        Assert.Null(errorMessage);
        Assert.Equal(CliCommandMode.ServeHttp, options!.Mode);
        Assert.Equal("DalamudMCP.1234", options.PipeName);
        Assert.Equal(CliRuntimeOptions.DefaultHttpPort, options.HttpPort);
        Assert.Equal(CliRuntimeOptions.DefaultHttpPath, options.HttpPath);
    }

    [Fact]
    public void TryParse_extracts_http_server_options()
    {
        bool parsed = CliRuntimeOptions.TryParse(["serve", "http", "--port", "39555", "--path", "custom"], out CliRuntimeOptions? options, out string? errorMessage);

        Assert.True(parsed);
        Assert.NotNull(options);
        Assert.Null(errorMessage);
        Assert.Equal(CliCommandMode.ServeHttp, options!.Mode);
        Assert.Equal(39555, options.HttpPort);
        Assert.Equal("/custom", options.HttpPath);
    }

    [Fact]
    public void TryParse_rejects_extra_arguments_for_serve_mcp()
    {
        bool parsed = CliRuntimeOptions.TryParse(["serve", "mcp", "--json"], out CliRuntimeOptions? options, out string? errorMessage);

        Assert.False(parsed);
        Assert.Null(options);
        Assert.Contains("does not accept additional arguments", errorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void TryParse_rejects_pipe_without_value()
    {
        bool parsed = CliRuntimeOptions.TryParse(["player", "context", "--pipe"], out CliRuntimeOptions? options, out string? errorMessage);

        Assert.False(parsed);
        Assert.Null(options);
        Assert.Contains("--pipe option requires", errorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void TryParse_rejects_invalid_http_port()
    {
        bool parsed = CliRuntimeOptions.TryParse(["serve", "http", "--port", "99999"], out CliRuntimeOptions? options, out string? errorMessage);

        Assert.False(parsed);
        Assert.Null(options);
        Assert.Contains("--port option requires", errorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void TryResolvePipeName_uses_discovery_file_when_no_explicit_pipe_is_present()
    {
        scope.WriteDiscovery("DalamudMCP.12345.instance");
        bool parsed = CliRuntimeOptions.TryParse(["player", "context"], out CliRuntimeOptions? options, out string? errorMessage);

        Assert.True(parsed);
        Assert.NotNull(options);
        Assert.Null(errorMessage);

        bool resolved = options!.TryResolvePipeName(out string? pipeName, out errorMessage);

        Assert.True(resolved);
        Assert.Equal("DalamudMCP.12345.instance", pipeName);
        Assert.Null(errorMessage);
    }

    [Fact]
    public void TryResolvePipeName_prefers_explicit_pipe_over_environment_and_discovery()
    {
        scope.WriteDiscovery("DalamudMCP.discovery");
        Environment.SetEnvironmentVariable("DALAMUD_MCP_PIPE", "DalamudMCP.environment");
        bool parsed = CliRuntimeOptions.TryParse(["--pipe", "DalamudMCP.explicit", "player", "context"], out CliRuntimeOptions? options, out string? errorMessage);

        Assert.True(parsed);
        Assert.NotNull(options);
        Assert.Null(errorMessage);

        bool resolved = options!.TryResolvePipeName(out string? pipeName, out errorMessage);

        Assert.True(resolved);
        Assert.Equal("DalamudMCP.explicit", pipeName);
        Assert.Null(errorMessage);
    }

    public void Dispose()
    {
        scope.Dispose();
    }
}
