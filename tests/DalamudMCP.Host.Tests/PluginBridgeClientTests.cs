using DalamudMCP.Contracts.Bridge.Responses;
using DalamudMCP.Domain.Policy;
using DalamudMCP.Infrastructure.Bridge;
using DalamudMCP.Plugin;

namespace DalamudMCP.Host.Tests;

public sealed class PluginBridgeClientTests
{
    [Fact]
    public async Task GetPlayerContextAsync_ReturnsNotReadyPayloadFromPlugin()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        await using var root = PluginCompositionRoot.CreateDefault(workingDirectory, $"DalamudMCP.HostTests.{Guid.NewGuid():N}");
        await root.SettingsRepository.SaveAsync(
            ExposurePolicy.Default.EnableTool("get_player_context"),
            cancellationToken);
        await root.StartAsync(cancellationToken);

        var client = new PluginBridgeClient(root.Options.PipeName);
        var response = await client.GetPlayerContextAsync(cancellationToken);

        Assert.False(response.Available);
        Assert.Equal("player_not_ready", response.Reason);
        Assert.Null(response.Data);
    }

    [Fact]
    public async Task GetCapabilityStateAsync_ReturnsConfiguredPolicy()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        await using var root = PluginCompositionRoot.CreateDefault(workingDirectory, $"DalamudMCP.HostTests.{Guid.NewGuid():N}");
        await root.SettingsRepository.SaveAsync(
            new ExposurePolicy(
                enabledTools: ["get_player_context"],
                enabledResources: ["ffxiv://player/context"],
                enabledAddons: ["Inventory"],
                observationProfileEnabled: true,
                actionProfileEnabled: false),
            cancellationToken);
        await root.StartAsync(cancellationToken);

        var client = new PluginBridgeClient(root.Options.PipeName);
        var response = await client.GetCapabilityStateAsync(cancellationToken);

        Assert.Contains("get_player_context", response.EnabledTools);
        Assert.Contains("ffxiv://player/context", response.EnabledResources);
        Assert.Contains("Inventory", response.EnabledAddons);
    }

    [Fact]
    public async Task GetSessionStatusAsync_ReturnsPluginRuntimeState()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        await using var root = PluginCompositionRoot.CreateDefault(workingDirectory, $"DalamudMCP.HostTests.{Guid.NewGuid():N}");
        await root.SettingsRepository.SaveAsync(
            ExposurePolicy.Default.EnableTool("get_session_status"),
            cancellationToken);
        await root.StartAsync(cancellationToken);

        var client = new PluginBridgeClient(root.Options.PipeName);
        var response = await client.GetSessionStatusAsync(cancellationToken);

        Assert.True(response.Available);
        Assert.Equal(root.Options.PipeName, response.Data?.PipeName);
        Assert.True(response.Data?.IsBridgeServerRunning);
        Assert.NotEmpty(response.Data?.Components ?? []);
    }

    [Fact]
    public async Task McpServerHost_CreateForPipe_UsesBridgeClient()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        await using var root = PluginCompositionRoot.CreateDefault(workingDirectory, $"DalamudMCP.HostTests.{Guid.NewGuid():N}");
        await root.SettingsRepository.SaveAsync(
            ExposurePolicy.Default.EnableTool("get_player_context"),
            cancellationToken);
        await root.StartAsync(cancellationToken);

        var host = McpServerHost.CreateForPipe(root.Options.PipeName);
        var response = await host.HandleJsonAsync(
            "{\"contractVersion\":\"1.0\",\"requestType\":\"GetPlayerContext\",\"requestId\":\"req-host\",\"payload\":{}}",
            cancellationToken);
        var envelope = DalamudMCP.Contracts.Bridge.BridgeJson.Deserialize<DalamudMCP.Contracts.Bridge.BridgeResponseEnvelope>(response);
        var payload = DalamudMCP.Contracts.Bridge.BridgeJson.DeserializePayload<DalamudMCP.Contracts.Bridge.QueryResponse<PlayerContextContract>>(envelope?.Payload);

        Assert.NotNull(envelope);
        Assert.True(envelope.Success);
        Assert.NotNull(payload);
        Assert.False(payload.Available);
    }

    [Fact]
    public async Task McpServerHost_CreateForPipe_FiltersToolsAndResourcesByPluginSettings()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        await using var root = PluginCompositionRoot.CreateDefault(workingDirectory, $"DalamudMCP.HostTests.{Guid.NewGuid():N}");
        await root.SettingsRepository.SaveAsync(
            new ExposurePolicy(
                enabledTools: ["get_player_context"],
                enabledResources: ["ffxiv://player/context"],
                enabledAddons: [],
                observationProfileEnabled: true,
                actionProfileEnabled: false),
            cancellationToken);
        await root.StartAsync(cancellationToken);

        var host = McpServerHost.CreateForPipe(root.Options.PipeName);
        var tools = await host.ListToolsAsync(cursor: null, pageSize: 20, cancellationToken);
        var resources = await host.ListResourcesAsync(cursor: null, pageSize: 20, cancellationToken);
        var resourceTemplates = await host.ListResourceTemplatesAsync(cursor: null, pageSize: 20, cancellationToken);

        Assert.Single(tools.Tools);
        Assert.Equal("get_player_context", tools.Tools[0].Name);
        Assert.Single(resources.Resources);
        Assert.Equal("ffxiv://player/context", resources.Resources[0].Uri);
        Assert.Empty(resourceTemplates.ResourceTemplates);
    }

    [Fact]
    public async Task McpServerHost_CreateForPipe_ReflectsSettingsChangesWithoutRestart()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        await using var root = PluginCompositionRoot.CreateDefault(workingDirectory, $"DalamudMCP.HostTests.{Guid.NewGuid():N}");
        await root.SettingsRepository.SaveAsync(ExposurePolicy.Default, cancellationToken);
        await root.StartAsync(cancellationToken);

        var host = McpServerHost.CreateForPipe(root.Options.PipeName);
        var initialTools = await host.ListToolsAsync(cursor: null, pageSize: 20, cancellationToken);
        var initialResources = await host.ListResourcesAsync(cursor: null, pageSize: 20, cancellationToken);

        Assert.Empty(initialTools.Tools);
        Assert.Empty(initialResources.Resources);

        await root.SettingsRepository.SaveAsync(
            new ExposurePolicy(
                enabledTools: ["get_player_context"],
                enabledResources: ["ffxiv://player/context"],
                enabledAddons: [],
                observationProfileEnabled: true,
                actionProfileEnabled: false),
            cancellationToken);

        var updatedTools = await host.ListToolsAsync(cursor: null, pageSize: 20, cancellationToken);
        var updatedResources = await host.ListResourcesAsync(cursor: null, pageSize: 20, cancellationToken);

        Assert.Single(updatedTools.Tools);
        Assert.Equal("get_player_context", updatedTools.Tools[0].Name);
        Assert.Single(updatedResources.Resources);
        Assert.Equal("ffxiv://player/context", updatedResources.Resources[0].Uri);
    }

    [Fact]
    public async Task GetCapabilityStateAsync_ThrowsStandardError_WhenPipeIsUnavailable()
    {
        var client = new PluginBridgeClient(
            new NamedPipeBridgeClient(
                $"DalamudMCP.Missing.{Guid.NewGuid():N}",
                TimeSpan.FromMilliseconds(50)));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetCapabilityStateAsync(TestContext.Current.CancellationToken));

        Assert.Equal("Plugin bridge is unavailable.", exception.Message);
        Assert.NotNull(exception.InnerException);
    }

    [Fact]
    public async Task GetPlayerContextAsync_Throws_WhenBridgeResponseContractVersionIsIncompatible()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var pipeName = $"DalamudMCP.HostTests.{Guid.NewGuid():N}";
        await using var server = new NamedPipeBridgeServer(
            pipeName,
            static (request, _) => Task.FromResult(
                new DalamudMCP.Contracts.Bridge.BridgeResponseEnvelope(
                    "2.0.0",
                    request.RequestId,
                    DalamudMCP.Infrastructure.Bridge.BridgeResponseTypes.Query,
                    Success: true,
                    ErrorCode: null,
                    ErrorMessage: null,
                    Payload: new { })));
        await server.StartAsync(cancellationToken);

        var client = new PluginBridgeClient(new NamedPipeBridgeClient(pipeName));
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetPlayerContextAsync(cancellationToken));

        Assert.Contains("Unsupported contract version", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RecordAuditEventAsync_WritesToPluginAuditLog()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        await using var root = PluginCompositionRoot.CreateDefault(workingDirectory, $"DalamudMCP.HostTests.{Guid.NewGuid():N}");
        await root.StartAsync(cancellationToken);

        var client = new PluginBridgeClient(root.Options.PipeName);
        await client.RecordAuditEventAsync("tool.request_denied", "tool=blocked_tool", cancellationToken);

        var contents = await File.ReadAllTextAsync(root.Options.AuditLogFilePath, cancellationToken);
        Assert.Contains("tool.request_denied", contents, StringComparison.Ordinal);
        Assert.Contains("tool=blocked_tool", contents, StringComparison.Ordinal);
    }
}
