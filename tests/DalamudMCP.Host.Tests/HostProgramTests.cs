using DalamudMCP.Domain.Policy;
using DalamudMCP.Plugin;

namespace DalamudMCP.Host.Tests;

public sealed class HostProgramTests
{
    [Fact]
    public async Task RunAsync_ReturnsUsage_WhenArgumentsAreInvalid()
    {
        using var input = new StringReader(string.Empty);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await HostProgram.RunAsync([], input, output, error, TestContext.Current.CancellationToken);

        Assert.Equal(1, exitCode);
        Assert.Contains("--pipe-name", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_UsesPipeConfiguredHost()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        await using var root = PluginCompositionRoot.CreateDefault(workingDirectory, $"DalamudMCP.HostProgramTests.{Guid.NewGuid():N}");
        await root.SettingsRepository.SaveAsync(
            new ExposurePolicy(
                enabledTools: ["get_player_context"],
                enabledResources: ["ffxiv://player/context"],
                enabledAddons: [],
                observationProfileEnabled: true,
                actionProfileEnabled: false),
            cancellationToken);
        await root.StartAsync(cancellationToken);

        using var input = new StringReader(
            """
            {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","clientInfo":{"name":"codex-cli","version":"1.0.0"}}}
            {"jsonrpc":"2.0","id":2,"method":"resources/read","params":{"uri":"ffxiv://player/context"}}
            """);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await HostProgram.RunAsync(
            ["--pipe-name", root.Options.PipeName],
            input,
            output,
            error,
            cancellationToken);

        var lines = output.ToString()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Assert.Equal(0, exitCode);
        Assert.Empty(error.ToString());
        Assert.Equal(2, lines.Length);
        Assert.Contains("player_not_ready", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_UsesPipeConfiguredHost_ForSessionStatus()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        await using var root = PluginCompositionRoot.CreateDefault(workingDirectory, $"DalamudMCP.HostProgramTests.{Guid.NewGuid():N}");
        await root.SettingsRepository.SaveAsync(
            new ExposurePolicy(
                enabledTools: ["get_session_status"],
                enabledResources: ["ffxiv://session/status"],
                enabledAddons: [],
                observationProfileEnabled: true,
                actionProfileEnabled: false),
            cancellationToken);
        await root.StartAsync(cancellationToken);

        using var input = new StringReader(
            """
            {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","clientInfo":{"name":"codex-cli","version":"1.0.0"}}}
            {"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"get_session_status"}}
            """);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await HostProgram.RunAsync(
            ["--pipe-name", root.Options.PipeName],
            input,
            output,
            error,
            cancellationToken);

        var lines = output.ToString()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Assert.Equal(0, exitCode);
        Assert.Empty(error.ToString());
        Assert.Equal(2, lines.Length);
        Assert.Contains(root.Options.PipeName, lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_WritesJsonRpcError_WhenPipeIsUnavailable()
    {
        using var input = new StringReader(
            """
            {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","clientInfo":{"name":"codex-cli","version":"1.0.0"}}}
            {"jsonrpc":"2.0","id":2,"method":"tools/list"}
            """);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await HostProgram.RunAsync(
            ["--pipe-name", $"DalamudMCP.Missing.{Guid.NewGuid():N}"],
            input,
            output,
            error,
            TestContext.Current.CancellationToken);

        var lines = output.ToString()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Assert.Equal(0, exitCode);
        Assert.Empty(error.ToString());
        Assert.Equal(2, lines.Length);
        Assert.Contains("Plugin bridge is unavailable.", lines[1], StringComparison.Ordinal);
    }
}
