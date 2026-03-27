using DalamudMCP.Framework;
using DalamudMCP.Protocol;

namespace DalamudMCP.Plugin.Operations.Tests;

public sealed class TeleportToAetheryteOperationTests
{
    [Fact]
    public void TeleportToAetheryteOperation_CarriesCliAndMcpMetadata_OnTheOperationClass()
    {
        Type operationType = typeof(TeleportToAetheryteOperation);

        OperationAttribute? operation = operationType.GetCustomAttribute<OperationAttribute>();
        CliCommandAttribute? cli = operationType.GetCustomAttribute<CliCommandAttribute>();
        McpToolAttribute? mcp = operationType.GetCustomAttribute<McpToolAttribute>();

        Assert.NotNull(operation);
        Assert.NotNull(cli);
        Assert.NotNull(mcp);
        Assert.Equal("teleport.to.aetheryte", operation.OperationId);
        Assert.Equal(["teleport", "to", "aetheryte"], cli.PathSegments);
        Assert.Equal("teleport_to_aetheryte", mcp.Name);
    }

    [Fact]
    public void TeleportToAetheryteOperation_RequestCarriesProtocolIdentity()
    {
        ProtocolOperationAttribute? protocol = typeof(TeleportToAetheryteOperation.Request)
            .GetCustomAttribute<ProtocolOperationAttribute>();

        Assert.NotNull(protocol);
        Assert.Equal("teleport.to.aetheryte", protocol.OperationId);
    }

    [Fact]
    public async Task ExecuteAsync_UsesInjectedExecutor_AndContextCancellation()
    {
        TeleportToAetheryteResult expected = new("limsa", true, null, 8u, "Limsa Lominsa", "Lower La Noscea", "Teleport started to Limsa Lominsa.");
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        CancellationToken observedCancellationToken = default;
        TeleportToAetheryteOperation operation = new(
            (request, cancellation) =>
            {
                observedCancellationToken = cancellation;
                Assert.Equal("limsa", request.Query);
                return ValueTask.FromResult(expected);
            });

        TeleportToAetheryteResult actual = await operation.ExecuteAsync(
            new TeleportToAetheryteOperation.Request { Query = "limsa" },
            OperationContext.ForCli("teleport.to.aetheryte", cancellationToken: cancellationToken));

        Assert.Equal(expected, actual);
        Assert.Equal(cancellationToken, observedCancellationToken);
    }

    [Fact]
    public void TryStartLifestreamAethernetTeleport_returns_null_when_client_is_unavailable()
    {
        TeleportToAetheryteResult? result = TeleportToAetheryteOperation.TryStartLifestreamAethernetTeleport(
            new FakeLifestreamClient(isAvailable: false, isBusy: false, startResult: false),
            "グリダニア：旧市街");

        Assert.Null(result);
    }

    [Fact]
    public void TryStartLifestreamAethernetTeleport_returns_success_summary_when_client_starts_travel()
    {
        TeleportToAetheryteResult? result = TeleportToAetheryteOperation.TryStartLifestreamAethernetTeleport(
            new FakeLifestreamClient(isAvailable: true, isBusy: false, startResult: true),
            "グリダニア：旧市街");

        Assert.NotNull(result);
        Assert.True(result!.Succeeded);
        Assert.Equal("グリダニア：旧市街", result.AetheryteName);
        Assert.Equal("Lifestream started local aethernet travel to グリダニア：旧市街.", result.SummaryText);
    }

    [Fact]
    public void TryStartLifestreamAethernetTeleport_returns_busy_failure_when_plugin_reports_busy()
    {
        TeleportToAetheryteResult? result = TeleportToAetheryteOperation.TryStartLifestreamAethernetTeleport(
            new FakeLifestreamClient(isAvailable: true, isBusy: true, startResult: true),
            "グリダニア：旧市街");

        Assert.NotNull(result);
        Assert.False(result!.Succeeded);
        Assert.Equal("lifestream_busy", result.Reason);
        Assert.Equal(
            "Lifestream is busy and could not start local aethernet travel to グリダニア：旧市街.",
            result.SummaryText);
    }

    private sealed class FakeLifestreamClient(bool isAvailable, bool isBusy, bool startResult)
        : TeleportToAetheryteOperation.ILifestreamAethernetClient
    {
        public bool IsAvailable { get; } = isAvailable;

        public bool IsBusy { get; } = isBusy;

        public bool StartAethernetTeleport(string destination)
        {
            Assert.Equal("グリダニア：旧市街", destination);
            return startResult;
        }
    }
}
