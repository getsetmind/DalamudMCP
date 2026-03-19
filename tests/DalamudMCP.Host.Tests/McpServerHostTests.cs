using System.Text.Json;
using DalamudMCP.Domain.Registry;

namespace DalamudMCP.Host.Tests;

public sealed class McpServerHostTests
{
    [Fact]
    public async Task InitializeAsync_ReturnsServerMetadataAndCapabilities()
    {
        var host = CreateHost();

        var result = await host.InitializeAsync(
            new McpInitializeRequest(
                McpProtocolVersion.Current,
                new McpClientCapabilities(
                    ToolsListChanged: false,
                    ResourcesSubscribe: false,
                    ResourcesListChanged: false),
                new McpClientInfo("codex-cli", "1.0.0", "Codex CLI")),
            TestContext.Current.CancellationToken);

        Assert.Equal(McpProtocolVersion.Current, result.ProtocolVersion);
        Assert.NotNull(result.Capabilities.Tools);
        Assert.NotNull(result.Capabilities.Resources);
        Assert.False(result.Capabilities.Tools!.ListChanged);
        Assert.False(result.Capabilities.Resources!.Subscribe);
        Assert.False(result.Capabilities.Resources!.ListChanged);
        Assert.Equal("dalamudmcp-host", result.ServerInfo.Name);
        Assert.Equal("DalamudMCP Host", result.ServerInfo.Title);
        Assert.Contains("FFXIV observation primitives", result.Instructions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InitializeAsync_RejectsUnsupportedProtocolVersion()
    {
        var host = CreateHost();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.InitializeAsync(
                new McpInitializeRequest(
                    "2024-11-05",
                    new McpClientCapabilities(false, false, false),
                    new McpClientInfo("codex-cli", "1.0.0")),
                TestContext.Current.CancellationToken));

        Assert.Contains("Unsupported MCP protocol version", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListToolsAsync_ReturnsSchemasAndPagination()
    {
        var host = CreateHost();

        var result = await host.ListToolsAsync(cursor: null, pageSize: 20, TestContext.Current.CancellationToken);

        Assert.Contains(result.Tools, static tool => tool.Name == "get_addon_tree");
        Assert.Contains(result.Tools, static tool => tool.Name == "get_nearby_interactables");
        Assert.Contains(result.Tools, static tool => tool.Name == "target_object");
        Assert.Contains(result.Tools, static tool => tool.Name == "teleport_to_aetheryte");
        Assert.Null(result.NextCursor);

        var addonTreeTool = result.Tools.Single(static tool => tool.Name == "get_addon_tree");
        Assert.True(addonTreeTool.InputSchema.TryGetProperty("properties", out var properties));
        Assert.True(properties.TryGetProperty("addonName", out var addonNameSchema));
        Assert.Equal("string", addonNameSchema.GetProperty("type").GetString());
        Assert.Equal(
            JsonValueKind.Array,
            addonTreeTool.OutputSchema?.GetProperty("required").ValueKind);
        Assert.True(addonTreeTool.Annotations.ReadOnlyHint);
        Assert.True(addonTreeTool.Annotations.IdempotentHint);
        Assert.False(addonTreeTool.Annotations.DestructiveHint);
        Assert.False(addonTreeTool.Annotations.OpenWorldHint);

        var targetTool = result.Tools.Single(static tool => tool.Name == "target_object");
        Assert.False(targetTool.Annotations.ReadOnlyHint);
        Assert.False(targetTool.Annotations.IdempotentHint);
        Assert.True(targetTool.Annotations.DestructiveHint);
        Assert.True(targetTool.Annotations.OpenWorldHint);
    }

    [Fact]
    public async Task ListResourcesAsync_ReturnsOnlyConcreteResources()
    {
        var host = CreateHost();

        var result = await host.ListResourcesAsync(cursor: null, pageSize: 10, TestContext.Current.CancellationToken);

        Assert.Equal(5, result.Resources.Count);
        Assert.All(result.Resources, static resource => Assert.DoesNotContain('{', resource.Uri));
        Assert.All(result.Resources, static resource => Assert.Equal(McpContentTypes.ApplicationJson, resource.MimeType));
        Assert.Contains(result.Resources, static resource => resource.Uri == "ffxiv://session/status");
        Assert.Contains(result.Resources, static resource => resource.Uri == "ffxiv://ui/addons");
        Assert.Contains(result.Resources, static resource => resource.Uri == "ffxiv://player/context");
        Assert.Null(result.NextCursor);
    }

    [Fact]
    public async Task ListResourceTemplatesAsync_ReturnsOnlyTemplatedResources()
    {
        var host = CreateHost();

        var result = await host.ListResourceTemplatesAsync(cursor: null, pageSize: 10, TestContext.Current.CancellationToken);

        Assert.Equal(2, result.ResourceTemplates.Count);
        Assert.All(result.ResourceTemplates, static resource => Assert.Contains('{', resource.UriTemplate));
        Assert.All(result.ResourceTemplates, static resource => Assert.Equal(McpContentTypes.ApplicationJson, resource.MimeType));
        Assert.Contains(result.ResourceTemplates, static resource => resource.UriTemplate == "ffxiv://ui/addon/{addonName}/tree");
        Assert.Null(result.NextCursor);
    }

    [Fact]
    public async Task ListToolsAsync_RejectsInvalidCursor()
    {
        var host = CreateHost();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.ListToolsAsync("not-a-number", 2, TestContext.Current.CancellationToken));
    }

    private static McpServerHost CreateHost() =>
        new(static (_, _) => Task.FromResult(
            new DalamudMCP.Contracts.Bridge.BridgeResponseEnvelope(
                DalamudMCP.Contracts.Bridge.ContractVersion.Current,
                "req-test",
                DalamudMCP.Infrastructure.Bridge.BridgeResponseTypes.Error,
                Success: false,
                ErrorCode: "not_implemented",
                ErrorMessage: "Not used in this test.",
                Payload: null)));
}
