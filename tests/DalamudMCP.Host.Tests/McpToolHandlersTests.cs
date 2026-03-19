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

    [Fact]
    public async Task TargetObjectHandler_RequiresGameObjectId()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        await using var root = PluginCompositionRoot.CreateDefault(workingDirectory, $"DalamudMCP.ToolHandlers.{Guid.NewGuid():N}");
        await root.SettingsRepository.SaveAsync(
            new ExposurePolicy(
                enabledTools: ["target_object"],
                actionProfileEnabled: true),
            cancellationToken);
        await root.StartAsync(cancellationToken);

        var handler = new TargetObjectToolHandler(new PluginBridgeClient(root.Options.PipeName));

        await Assert.ThrowsAsync<ArgumentException>(() => handler.InvokeAsync(new { }, cancellationToken));
    }

    [Fact]
    public async Task TeleportToAetheryteHandler_RequiresQuery()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        await using var root = PluginCompositionRoot.CreateDefault(workingDirectory, $"DalamudMCP.ToolHandlers.{Guid.NewGuid():N}");
        await root.SettingsRepository.SaveAsync(
            new ExposurePolicy(
                enabledTools: ["teleport_to_aetheryte"],
                actionProfileEnabled: true),
            cancellationToken);
        await root.StartAsync(cancellationToken);

        var handler = new TeleportToAetheryteToolHandler(new PluginBridgeClient(root.Options.PipeName));

        await Assert.ThrowsAsync<ArgumentException>(() => handler.InvokeAsync(new { }, cancellationToken));
    }

    [Fact]
    public async Task AddonCallbackValuesHandler_RequiresValues()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        await using var root = PluginCompositionRoot.CreateDefault(workingDirectory, $"DalamudMCP.ToolHandlers.{Guid.NewGuid():N}");
        await root.SettingsRepository.SaveAsync(
            new ExposurePolicy(
                enabledTools: ["send_addon_callback_values"],
                enabledAddons: ["TelepotTown"],
                actionProfileEnabled: true),
            cancellationToken);
        await root.StartAsync(cancellationToken);

        var handler = new AddonCallbackValuesToolHandler(new PluginBridgeClient(root.Options.PipeName));

        await Assert.ThrowsAsync<ArgumentException>(() => handler.InvokeAsync(new { addonName = "TelepotTown" }, cancellationToken));
    }
}
