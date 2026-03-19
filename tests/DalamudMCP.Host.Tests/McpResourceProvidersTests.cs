using DalamudMCP.Domain.Policy;
using DalamudMCP.Host.Resources;
using DalamudMCP.Plugin;

namespace DalamudMCP.Host.Tests;

public sealed class McpResourceProvidersTests
{
    [Fact]
    public void AddonTreeProvider_MatchesTemplatedUri()
    {
        var provider = new AddonTreeResourceProvider(new PluginBridgeClient(new DalamudMCP.Infrastructure.Bridge.NamedPipeBridgeClient("unused")));

        Assert.True(provider.CanHandle("ffxiv://ui/addon/Inventory/tree"));
        Assert.False(provider.CanHandle("ffxiv://ui/addon/Inventory/strings"));
    }

    [Fact]
    public async Task PlayerContextProvider_ReturnsBridgePayload()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        await using var root = PluginCompositionRoot.CreateDefault(workingDirectory, $"DalamudMCP.ResourceProviders.{Guid.NewGuid():N}");
        await root.SettingsRepository.SaveAsync(
            ExposurePolicy.Default.EnableResource("ffxiv://player/context"),
            cancellationToken);
        await root.StartAsync(cancellationToken);

        var provider = new PlayerContextResourceProvider(new PluginBridgeClient(root.Options.PipeName));
        var result = await provider.ReadAsync("ffxiv://player/context", cancellationToken);
        var typed = Assert.IsType<DalamudMCP.Contracts.Bridge.QueryResponse<DalamudMCP.Contracts.Bridge.Responses.PlayerContextContract>>(result);

        Assert.False(typed.Available);
        Assert.Equal("player_not_ready", typed.Reason);
    }

    [Fact]
    public async Task SessionStatusProvider_ReturnsBridgePayload()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        await using var root = PluginCompositionRoot.CreateDefault(workingDirectory, $"DalamudMCP.ResourceProviders.{Guid.NewGuid():N}");
        await root.SettingsRepository.SaveAsync(
            ExposurePolicy.Default.EnableResource("ffxiv://session/status"),
            cancellationToken);
        await root.StartAsync(cancellationToken);

        var provider = new SessionStatusResourceProvider(new PluginBridgeClient(root.Options.PipeName));
        var result = await provider.ReadAsync("ffxiv://session/status", cancellationToken);
        var typed = Assert.IsType<DalamudMCP.Contracts.Bridge.QueryResponse<DalamudMCP.Contracts.Bridge.Responses.SessionStateContract>>(result);

        Assert.True(typed.Available);
        Assert.Equal(root.Options.PipeName, typed.Data?.PipeName);
        Assert.True(typed.Data?.IsBridgeServerRunning);
    }
}
