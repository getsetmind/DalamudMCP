using DalamudMCP.Domain.Policy;
using DalamudMCP.Host.Tools;
using DalamudMCP.Plugin;

namespace DalamudMCP.Host.Tests;

public sealed class McpToolHandlersTests
{
    [Fact]
    public async Task AddonTreeHandler_RequiresAddonName()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        await using var root = PluginCompositionRoot.CreateDefault(workingDirectory, $"DalamudMCP.ToolHandlers.{Guid.NewGuid():N}");
        await root.SettingsRepository.SaveAsync(
            ExposurePolicy.Default.EnableTool("get_addon_tree").EnableAddon("Inventory"),
            cancellationToken);
        await root.StartAsync(cancellationToken);

        var handler = new AddonTreeToolHandler(new PluginBridgeClient(root.Options.PipeName));

        await Assert.ThrowsAsync<ArgumentException>(() => handler.InvokeAsync(new { }, cancellationToken));
    }

    [Fact]
    public async Task PlayerContextHandler_ReturnsBridgePayload()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        await using var root = PluginCompositionRoot.CreateDefault(workingDirectory, $"DalamudMCP.ToolHandlers.{Guid.NewGuid():N}");
        await root.SettingsRepository.SaveAsync(
            ExposurePolicy.Default.EnableTool("get_player_context"),
            cancellationToken);
        await root.StartAsync(cancellationToken);

        var handler = new PlayerContextToolHandler(new PluginBridgeClient(root.Options.PipeName));
        var result = await handler.InvokeAsync(null, cancellationToken);
        var typed = Assert.IsType<DalamudMCP.Contracts.Bridge.QueryResponse<DalamudMCP.Contracts.Bridge.Responses.PlayerContextContract>>(result);

        Assert.False(typed.Available);
        Assert.Equal("player_not_ready", typed.Reason);
    }

    [Fact]
    public async Task SessionStatusHandler_ReturnsBridgePayload()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        await using var root = PluginCompositionRoot.CreateDefault(workingDirectory, $"DalamudMCP.ToolHandlers.{Guid.NewGuid():N}");
        await root.SettingsRepository.SaveAsync(
            ExposurePolicy.Default.EnableTool("get_session_status"),
            cancellationToken);
        await root.StartAsync(cancellationToken);

        var handler = new SessionStatusToolHandler(new PluginBridgeClient(root.Options.PipeName));
        var result = await handler.InvokeAsync(null, cancellationToken);
        var typed = Assert.IsType<DalamudMCP.Contracts.Bridge.QueryResponse<DalamudMCP.Contracts.Bridge.Responses.SessionStateContract>>(result);

        Assert.True(typed.Available);
        Assert.Equal(root.Options.PipeName, typed.Data?.PipeName);
        Assert.True(typed.Data?.IsBridgeServerRunning);
    }
}
