using System.Runtime.Versioning;
using DalamudMCP.Contracts.Bridge;
using DalamudMCP.Contracts.Bridge.Requests;
using DalamudMCP.Contracts.Bridge.Responses;
using DalamudMCP.Domain.Policy;
using DalamudMCP.Infrastructure.Bridge;

namespace DalamudMCP.Plugin.Tests;

[SupportedOSPlatform("windows")]
public sealed class PluginCompositionRootTests
{
    [Fact]
    public async Task StartAsync_StartsPipeServerAndHandlesRequests()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        await using var root = PluginCompositionRoot.CreateDefault(workingDirectory, $"DalamudMCP.PluginTests.{Guid.NewGuid():N}");
        await root.SettingsRepository.SaveAsync(
            ExposurePolicy.Default.EnableTool("get_player_context"),
            cancellationToken);
        await root.StartAsync(cancellationToken);

        var client = new NamedPipeBridgeClient(root.Options.PipeName);
        var response = await client.SendAsync(
            new BridgeRequestEnvelope(
                ContractVersion.Current,
                BridgeRequestTypes.GetPlayerContext,
                "req-plugin",
                new EmptyRequest()),
            cancellationToken);

        var payload = BridgeJson.DeserializePayload<QueryResponse<PlayerContextContract>>(response.Payload);

        Assert.True(response.Success);
        Assert.NotNull(payload);
        Assert.False(payload.Available);
        Assert.Equal("player_not_ready", payload.Reason);
    }

    [Fact]
    public async Task StartAsync_ReturnsSessionStatusPayload()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        await using var root = PluginCompositionRoot.CreateDefault(workingDirectory, $"DalamudMCP.PluginTests.{Guid.NewGuid():N}");
        await root.SettingsRepository.SaveAsync(
            ExposurePolicy.Default
                .EnableTool("get_session_status")
                .EnableResource("ffxiv://session/status"),
            cancellationToken);
        await root.StartAsync(cancellationToken);

        var client = new NamedPipeBridgeClient(root.Options.PipeName);
        var response = await client.SendAsync(
            new BridgeRequestEnvelope(
                ContractVersion.Current,
                BridgeRequestTypes.GetSessionStatus,
                "req-session",
                new EmptyRequest()),
            cancellationToken);

        var payload = BridgeJson.DeserializePayload<QueryResponse<SessionStateContract>>(response.Payload);

        Assert.True(response.Success);
        Assert.NotNull(payload);
        Assert.True(payload.Available);
        Assert.Equal(root.Options.PipeName, payload.Data?.PipeName);
        Assert.True(payload.Data?.IsBridgeServerRunning);
        Assert.NotEmpty(payload.Data?.Components ?? []);
    }

    [Fact]
    public async Task StartAsync_RecordsAuditEventsThroughBridgeRequest()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        await using var root = PluginCompositionRoot.CreateDefault(workingDirectory, $"DalamudMCP.PluginTests.{Guid.NewGuid():N}");
        await root.StartAsync(cancellationToken);

        var client = new NamedPipeBridgeClient(root.Options.PipeName);
        var response = await client.SendAsync(
            new BridgeRequestEnvelope(
                ContractVersion.Current,
                BridgeRequestTypes.RecordAuditEvent,
                "req-audit",
                new AuditEventRequest("resource.request_denied", "resource=ffxiv://blocked/resource")),
            cancellationToken);

        var contents = await File.ReadAllTextAsync(root.Options.AuditLogFilePath, cancellationToken);

        Assert.True(response.Success);
        Assert.Contains("resource.request_denied", contents, StringComparison.Ordinal);
        Assert.Contains("ffxiv://blocked/resource", contents, StringComparison.Ordinal);
    }

}
