using MemoryPack;

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
                    new SamplePlayerContextSnapshot(
                        "Test Adventurer",
                        "ExampleWorld",
                        "Dancer",
                        100,
                        "Sample Plaza",
                        new SamplePlayerPosition(1.2, 3.4, 5.6)),
                    typeof(SamplePlayerContextSnapshot),
                    preferredPayloadFormat: request.PreferredResponseFormat));
            });
        await server.StartAsync(TestContext.Current.CancellationToken);

        NamedPipeProtocolClient client = new(pipeName);
        SamplePlayerContextSnapshot snapshot = await client.InvokeAsync<SamplePlayerContextRequest, SamplePlayerContextSnapshot>(
            new SamplePlayerContextRequest(),
            TestContext.Current.CancellationToken);

        Assert.Equal("Test Adventurer", snapshot.CharacterName);
        Assert.Equal("ExampleWorld", snapshot.HomeWorld);
        Assert.Equal("Dancer", snapshot.JobName);
        Assert.Equal(100, snapshot.JobLevel);
    }
}

[ProtocolOperation("player.context")]
[MemoryPackable]
public sealed partial record SamplePlayerContextRequest;

[MemoryPackable]
public sealed partial record SamplePlayerContextSnapshot(
    string CharacterName,
    string HomeWorld,
    string JobName,
    int JobLevel,
    string PlaceName,
    SamplePlayerPosition Position);

[MemoryPackable]
public readonly partial record struct SamplePlayerPosition(
    double X,
    double Y,
    double Z);
