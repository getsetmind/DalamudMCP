using System.IO.Pipes;
using DalamudMCP.Contracts.Bridge;
using DalamudMCP.Contracts.Bridge.Requests;
using DalamudMCP.Infrastructure.Bridge;

namespace DalamudMCP.Infrastructure.Tests.Bridge;

public sealed class NamedPipeBridgeTransportTests
{
    [Fact]
    public async Task ClientAndServer_RoundTripEnvelope()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var pipeName = $"DalamudMCP.TransportTests.{Guid.NewGuid():N}";
        await using var server = new NamedPipeBridgeServer(
            pipeName,
            static (request, _) => Task.FromResult(
                new BridgeResponseEnvelope(
                    ContractVersion.Current,
                    request.RequestId,
                    BridgeResponseTypes.Query,
                    Success: true,
                    ErrorCode: null,
                    ErrorMessage: null,
                    Payload: new { echoed = request.RequestType })));
        await server.StartAsync(cancellationToken);
        var client = new NamedPipeBridgeClient(pipeName);

        var response = await client.SendAsync(
            new BridgeRequestEnvelope(
                ContractVersion.Current,
                BridgeRequestTypes.GetPlayerContext,
                "req-transport",
                new EmptyRequest()),
            cancellationToken);

        Assert.True(response.Success);
        Assert.Equal("req-transport", response.RequestId);
        Assert.Equal(BridgeResponseTypes.Query, response.ResponseType);
    }

    [Fact]
    public async Task Client_ThrowsTimeout_WhenPipeIsUnavailable()
    {
        var client = new NamedPipeBridgeClient(
            $"DalamudMCP.TransportTests.Missing.{Guid.NewGuid():N}",
            TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAsync<TimeoutException>(() =>
            client.SendAsync(
                new BridgeRequestEnvelope(
                    ContractVersion.Current,
                    BridgeRequestTypes.GetPlayerContext,
                    "req-timeout",
                    new EmptyRequest()),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Client_RejectsIncompatibleResponseVersion()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var pipeName = $"DalamudMCP.TransportTests.{Guid.NewGuid():N}";
        await using var server = new NamedPipeBridgeServer(
            pipeName,
            static (request, _) => Task.FromResult(
                new BridgeResponseEnvelope(
                    "2.0.0",
                    request.RequestId,
                    BridgeResponseTypes.Query,
                    Success: true,
                    ErrorCode: null,
                    ErrorMessage: null,
                    Payload: new { echoed = request.RequestType })));
        await server.StartAsync(cancellationToken);
        var client = new NamedPipeBridgeClient(pipeName);

        var response = await client.SendAsync(
            new BridgeRequestEnvelope(
                ContractVersion.Current,
                BridgeRequestTypes.GetPlayerContext,
                "req-version",
                new EmptyRequest()),
            cancellationToken);

        Assert.False(response.Success);
        Assert.Equal("invalid_contract_version", response.ErrorCode);
    }

    [Fact]
    public async Task Server_RejectsIncompatibleRequestVersion()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var pipeName = $"DalamudMCP.TransportTests.{Guid.NewGuid():N}";
        await using var server = new NamedPipeBridgeServer(
            pipeName,
            static (_, _) => Task.FromResult(
                new BridgeResponseEnvelope(
                    ContractVersion.Current,
                    "ignored",
                    BridgeResponseTypes.Query,
                    Success: true,
                    ErrorCode: null,
                    ErrorMessage: null,
                    Payload: new { })));
        await server.StartAsync(cancellationToken);
        var client = new NamedPipeBridgeClient(pipeName);

        var response = await client.SendAsync(
            new BridgeRequestEnvelope(
                "2.0.0",
                BridgeRequestTypes.GetPlayerContext,
                "req-version",
                new EmptyRequest()),
            cancellationToken);

        Assert.False(response.Success);
        Assert.Equal("invalid_contract_version", response.ErrorCode);
    }

    [Fact]
    public async Task Server_AllowsSecondClient_WhenFirstClientStalls()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var pipeName = $"DalamudMCP.TransportTests.{Guid.NewGuid():N}";
        await using var server = new NamedPipeBridgeServer(
            pipeName,
            static (request, _) => Task.FromResult(
                new BridgeResponseEnvelope(
                    ContractVersion.Current,
                    request.RequestId,
                    BridgeResponseTypes.Query,
                    Success: true,
                    ErrorCode: null,
                    ErrorMessage: null,
                    Payload: new { echoed = request.RequestType })),
            TimeSpan.FromSeconds(1));
        await server.StartAsync(cancellationToken);

        await using var stalledClient = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await stalledClient.ConnectAsync(cancellationToken);

        var secondClient = new NamedPipeBridgeClient(pipeName, TimeSpan.FromSeconds(1));
        var response = await secondClient.SendAsync(
            new BridgeRequestEnvelope(
                ContractVersion.Current,
                BridgeRequestTypes.GetPlayerContext,
                "req-stall",
                new EmptyRequest()),
            cancellationToken);

        Assert.True(response.Success);
        Assert.Equal("req-stall", response.RequestId);
    }
}
