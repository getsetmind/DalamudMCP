using DalamudMCP.Domain.Capabilities;
using DalamudMCP.Domain.Policy;
using DalamudMCP.Domain.Registry;
using DalamudMCP.Plugin;

namespace DalamudMCP.Host.Tests;

public sealed class McpResourceReaderTests
{
    [Fact]
    public async Task ReadAsync_PlayerContextUri_ReturnsBridgePayload()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        await using var root = PluginCompositionRoot.CreateDefault(workingDirectory, $"DalamudMCP.ResourceReaderTests.{Guid.NewGuid():N}");
        await root.SettingsRepository.SaveAsync(
            ExposurePolicy.Default.EnableResource("ffxiv://player/context"),
            cancellationToken);
        await root.StartAsync(cancellationToken);

        var reader = new McpResourceReader(
            new PluginBridgeClient(root.Options.PipeName),
            McpResourceRegistry.CreateDefault());
        var result = await reader.ReadAsync("ffxiv://player/context", cancellationToken);
        var typed = Assert.IsType<DalamudMCP.Contracts.Bridge.QueryResponse<DalamudMCP.Contracts.Bridge.Responses.PlayerContextContract>>(result);

        Assert.False(typed.Available);
        Assert.Equal("player_not_ready", typed.Reason);
    }

    [Fact]
    public async Task ReadAsync_DisabledResource_IsRejectedBeforeBridgeCall()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        await using var root = PluginCompositionRoot.CreateDefault(workingDirectory, $"DalamudMCP.ResourceReaderTests.{Guid.NewGuid():N}");
        await root.SettingsRepository.SaveAsync(ExposurePolicy.Default, cancellationToken);
        await root.StartAsync(cancellationToken);

        var reader = new McpResourceReader(
            new PluginBridgeClient(root.Options.PipeName),
            McpResourceRegistry.CreateDefault());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => reader.ReadAsync("ffxiv://player/context", cancellationToken));

        Assert.Contains("not enabled", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadAsync_InvalidScheme_IsRejectedBeforeProviderSelection()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        await using var root = PluginCompositionRoot.CreateDefault(workingDirectory, $"DalamudMCP.ResourceReaderTests.{Guid.NewGuid():N}");
        await root.SettingsRepository.SaveAsync(
            ExposurePolicy.Default.EnableResource("ffxiv://player/context"),
            cancellationToken);
        await root.StartAsync(cancellationToken);

        var reader = new McpResourceReader(
            new PluginBridgeClient(root.Options.PipeName),
            McpResourceRegistry.CreateDefault());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => reader.ReadAsync("https://example.com/not-ffxiv", cancellationToken));

        Assert.Contains("Unsupported resource uri", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadAsync_AddonResource_IsRejectedWhenAddonIsNotEnabled()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        await using var root = PluginCompositionRoot.CreateDefault(workingDirectory, $"DalamudMCP.ResourceReaderTests.{Guid.NewGuid():N}");
        await root.SettingsRepository.SaveAsync(
            ExposurePolicy.Default.EnableResource("ffxiv://ui/addon/{addonName}/tree"),
            cancellationToken);
        await root.StartAsync(cancellationToken);

        var reader = new McpResourceReader(
            new PluginBridgeClient(root.Options.PipeName),
            McpResourceRegistry.CreateDefault());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => reader.ReadAsync("ffxiv://ui/addon/Inventory/tree", cancellationToken));

        Assert.Contains("Addon access", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadAsync_DeniedResource_IsRejectedAsDenied()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        await using var root = PluginCompositionRoot.CreateDefault(workingDirectory, $"DalamudMCP.ResourceReaderTests.{Guid.NewGuid():N}");
        await root.StartAsync(cancellationToken);

        var deniedCapability = new CapabilityDefinition(
            new CapabilityId("system.denied"),
            "Denied",
            "Denied capability.",
            CapabilityCategory.System,
            SensitivityLevel.Blocked,
            ProfileType.Observation,
            defaultEnabled: false,
            requiresConsent: false,
            denied: true,
            supportsTool: false,
            supportsResource: true,
            version: "1.0.0");
        var registry = new McpResourceRegistry(
            new CapabilityRegistry(
                [deniedCapability],
                [],
                [new ResourceBinding(deniedCapability.Id, "ffxiv://blocked/resource", "application/json", "Provider", false)],
                []));
        var reader = new McpResourceReader(
            new PluginBridgeClient(root.Options.PipeName),
            registry);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => reader.ReadAsync("ffxiv://blocked/resource", cancellationToken));

        Assert.Contains("denied", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReadAsync_DeniedAddon_IsRejectedEvenWhenPolicyContainsAddon()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        await using var root = PluginCompositionRoot.CreateDefault(workingDirectory, $"DalamudMCP.ResourceReaderTests.{Guid.NewGuid():N}");
        await root.SettingsRepository.SaveAsync(
            new ExposurePolicy(
                enabledTools: [],
                enabledResources: ["ffxiv://ui/addon/{addonName}/tree"],
                enabledAddons: ["BlockedAddon"],
                observationProfileEnabled: true,
                actionProfileEnabled: false),
            cancellationToken);
        await root.StartAsync(cancellationToken);

        var capability = new CapabilityDefinition(
            new CapabilityId("ui.addonTree"),
            "Addon Tree",
            "Read addon tree.",
            CapabilityCategory.Ui,
            SensitivityLevel.Low,
            ProfileType.Observation,
            defaultEnabled: false,
            requiresConsent: false,
            denied: false,
            supportsTool: false,
            supportsResource: true,
            version: "1.0.0");
        var registry = new McpResourceRegistry(
            new CapabilityRegistry(
                [capability],
                [],
                [new ResourceBinding(capability.Id, "ffxiv://ui/addon/{addonName}/tree", "application/json", "Provider", false)],
                [
                    new AddonMetadata(
                        "BlockedAddon",
                        "Blocked Addon",
                        CapabilityCategory.Ui,
                        SensitivityLevel.Blocked,
                        DefaultEnabled: false,
                        Denied: true,
                        Recommended: false,
                        Notes: "Denied addon.",
                        IntrospectionModes: ["tree"],
                        ProfileAvailability: [ProfileType.Observation]),
                ]));
        var reader = new McpResourceReader(
            new PluginBridgeClient(root.Options.PipeName),
            registry);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => reader.ReadAsync("ffxiv://ui/addon/BlockedAddon/tree", cancellationToken));

        Assert.Contains("Addon access", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadAsync_DeniedResource_RecordsAuditEvent()
    {
        var events = new List<(string EventType, string Summary)>();
        var deniedCapability = new CapabilityDefinition(
            new CapabilityId("system.denied"),
            "Denied",
            "Denied capability.",
            CapabilityCategory.System,
            SensitivityLevel.Blocked,
            ProfileType.Observation,
            defaultEnabled: false,
            requiresConsent: false,
            denied: true,
            supportsTool: false,
            supportsResource: true,
            version: "1.0.0");
        var registry = new McpResourceRegistry(
            new CapabilityRegistry(
                [deniedCapability],
                [],
                [new ResourceBinding(deniedCapability.Id, "ffxiv://blocked/resource", "application/json", "Provider", false)],
                []));
        var reader = new McpResourceReader(
            providers: [],
            resourceRegistry: registry,
            capabilityStateProvider: null,
            auditRecorder: (eventType, summary, _) =>
            {
                events.Add((eventType, summary));
                return Task.CompletedTask;
            });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => reader.ReadAsync("ffxiv://blocked/resource", TestContext.Current.CancellationToken));

        var auditEvent = Assert.Single(events);
        Assert.Equal("resource.request_denied", auditEvent.EventType);
        Assert.Contains("ffxiv://blocked/resource", auditEvent.Summary, StringComparison.Ordinal);
    }
}
