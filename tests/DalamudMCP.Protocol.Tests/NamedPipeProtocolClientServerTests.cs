using DalamudMCP.Plugin.Operations;

namespace DalamudMCP.Protocol.Tests;

public sealed class NamedPipeProtocolClientServerTests
{
    [Fact]
    public async Task Client_and_server_round_trip_player_context_snapshot()
    {
        string pipeName = "DalamudMCP.ProtocolTest." + Guid.NewGuid().ToString("N");
        await using NamedPipeProtocolServer server = new(
            pipeName,
            static (request, _) =>
            {
                Assert.Equal("player.context", request.RequestType);
                Assert.Equal(ProtocolContract.DefaultRequestId, request.RequestId);
                Assert.Null(request.Payload);
                Assert.Equal(ProtocolPayloadFormat.None, request.PayloadFormat);
                Assert.Equal(ProtocolPayloadFormat.MemoryPack, request.PreferredResponseFormat);
                return ValueTask.FromResult(ProtocolContract.CreateSuccessResponse(
                    request.RequestId,
                    new PlayerContextSnapshot(
                        "Test Adventurer",
                        "ExampleWorld",
                        "Dancer",
                        100,
                        "Sample Plaza",
                        new PlayerPosition(1.2, 3.4, 5.6)),
                    typeof(PlayerContextSnapshot),
                    preferredPayloadFormat: request.PreferredResponseFormat));
            });
        await server.StartAsync(TestContext.Current.CancellationToken);

        NamedPipeProtocolClient client = new(pipeName);
        PlayerContextSnapshot snapshot = await client.InvokeAsync<PlayerContextOperation.Request, PlayerContextSnapshot>(
            new PlayerContextOperation.Request(),
            TestContext.Current.CancellationToken);

        Assert.Equal("Test Adventurer", snapshot.CharacterName);
        Assert.Equal("ExampleWorld", snapshot.HomeWorld);
        Assert.Equal("Dancer", snapshot.JobName);
        Assert.Equal(100, snapshot.JobLevel);
    }
}
