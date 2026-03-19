using DalamudMCP.Contracts.Bridge.Requests;
using DalamudMCP.Domain.Capabilities;
using DalamudMCP.Domain.Policy;
using DalamudMCP.Domain.Registry;
using DalamudMCP.Plugin;

namespace DalamudMCP.Host.Tests;

public sealed class McpToolInvokerTests
{
    [Fact]
    public async Task InvokeAsync_PlayerContextTool_ReturnsBridgePayload()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        await using var root = PluginCompositionRoot.CreateDefault(workingDirectory, $"DalamudMCP.ToolInvokerTests.{Guid.NewGuid():N}");
        await root.SettingsRepository.SaveAsync(
            ExposurePolicy.Default.EnableTool("get_player_context"),
            cancellationToken);
        await root.StartAsync(cancellationToken);

        var invoker = new McpToolInvoker(
            new PluginBridgeClient(root.Options.PipeName),
            McpToolRegistry.CreateDefault());
        var result = await invoker.InvokeAsync("get_player_context", null, cancellationToken);
        var typed = Assert.IsType<DalamudMCP.Contracts.Bridge.QueryResponse<DalamudMCP.Contracts.Bridge.Responses.PlayerContextContract>>(result);

        Assert.False(typed.Available);
        Assert.Equal("player_not_ready", typed.Reason);
    }

    [Fact]
    public async Task InvokeAsync_AddonTreeTool_RequiresAddonArgument()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        await using var root = PluginCompositionRoot.CreateDefault(workingDirectory, $"DalamudMCP.ToolInvokerTests.{Guid.NewGuid():N}");
        await root.SettingsRepository.SaveAsync(
            ExposurePolicy.Default.EnableTool("get_addon_tree").EnableAddon("Inventory"),
            cancellationToken);
        await root.StartAsync(cancellationToken);

        var invoker = new McpToolInvoker(
            new PluginBridgeClient(root.Options.PipeName),
            McpToolRegistry.CreateDefault());
        var result = await invoker.InvokeAsync("get_addon_tree", new AddonRequest("Inventory"), cancellationToken);
        var typed = Assert.IsType<DalamudMCP.Contracts.Bridge.QueryResponse<DalamudMCP.Contracts.Bridge.Responses.AddonTreeContract>>(result);

        Assert.False(typed.Available);
        Assert.Equal("addon_not_ready", typed.Reason);
    }

    [Fact]
    public async Task InvokeAsync_DisabledTool_IsRejectedBeforeBridgeCall()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        await using var root = PluginCompositionRoot.CreateDefault(workingDirectory, $"DalamudMCP.ToolInvokerTests.{Guid.NewGuid():N}");
        await root.SettingsRepository.SaveAsync(ExposurePolicy.Default, cancellationToken);
        await root.StartAsync(cancellationToken);

        var invoker = new McpToolInvoker(
            new PluginBridgeClient(root.Options.PipeName),
            McpToolRegistry.CreateDefault());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => invoker.InvokeAsync("get_player_context", null, cancellationToken));

        Assert.Contains("not enabled", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAsync_AddonTool_IsRejectedWhenAddonIsNotEnabled()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        await using var root = PluginCompositionRoot.CreateDefault(workingDirectory, $"DalamudMCP.ToolInvokerTests.{Guid.NewGuid():N}");
        await root.SettingsRepository.SaveAsync(
            ExposurePolicy.Default.EnableTool("get_addon_tree"),
            cancellationToken);
        await root.StartAsync(cancellationToken);

        var invoker = new McpToolInvoker(
            new PluginBridgeClient(root.Options.PipeName),
            McpToolRegistry.CreateDefault());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => invoker.InvokeAsync("get_addon_tree", new AddonRequest("Inventory"), cancellationToken));

        Assert.Contains("Addon access", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAsync_DeniedTool_IsRejectedAsDenied()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        await using var root = PluginCompositionRoot.CreateDefault(workingDirectory, $"DalamudMCP.ToolInvokerTests.{Guid.NewGuid():N}");
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
            supportsTool: true,
            supportsResource: false,
            version: "1.0.0");
        var registry = new McpToolRegistry(
            new CapabilityRegistry(
                [deniedCapability],
                [new ToolBinding(deniedCapability.Id, "blocked_tool", "in", "out", "Handler", false)],
                [],
                []));
        var invoker = new McpToolInvoker(
            new PluginBridgeClient(root.Options.PipeName),
            registry);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => invoker.InvokeAsync("blocked_tool", null, cancellationToken));

        Assert.Contains("denied", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvokeAsync_DeniedAddon_IsRejectedEvenWhenPolicyContainsAddon()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        await using var root = PluginCompositionRoot.CreateDefault(workingDirectory, $"DalamudMCP.ToolInvokerTests.{Guid.NewGuid():N}");
        await root.SettingsRepository.SaveAsync(
            new ExposurePolicy(
                enabledTools: ["get_addon_tree"],
                enabledResources: [],
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
            supportsTool: true,
            supportsResource: false,
            version: "1.0.0");
        var registry = new McpToolRegistry(
            new CapabilityRegistry(
                [capability],
                [new ToolBinding(capability.Id, "get_addon_tree", "in", "out", "Handler", false)],
                [],
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
        var invoker = new McpToolInvoker(
            new PluginBridgeClient(root.Options.PipeName),
            registry);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => invoker.InvokeAsync("get_addon_tree", new AddonRequest("BlockedAddon"), cancellationToken));

        Assert.Contains("Addon access", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAsync_DeniedTool_RecordsAuditEvent()
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
            supportsTool: true,
            supportsResource: false,
            version: "1.0.0");
        var registry = new McpToolRegistry(
            new CapabilityRegistry(
                [deniedCapability],
                [new ToolBinding(deniedCapability.Id, "blocked_tool", "in", "out", "Handler", false)],
                [],
                []));
        var invoker = new McpToolInvoker(
            handlers: [],
            toolRegistry: registry,
            capabilityStateProvider: null,
            auditRecorder: (eventType, summary, _) =>
            {
                events.Add((eventType, summary));
                return Task.CompletedTask;
            });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => invoker.InvokeAsync("blocked_tool", null, TestContext.Current.CancellationToken));

        var auditEvent = Assert.Single(events);
        Assert.Equal("tool.request_denied", auditEvent.EventType);
        Assert.Contains("blocked_tool", auditEvent.Summary, StringComparison.Ordinal);
    }
}
