using System.Text.Json;
using DalamudMCP.Contracts.Bridge;
using DalamudMCP.Contracts.Bridge.Requests;
using DalamudMCP.Contracts.Bridge.Responses;

namespace DalamudMCP.Contracts.Tests.Bridge;

public sealed class BridgeSerializationTests
{
    [Fact]
    public void QueryResponse_SerializesAndDeserializes()
    {
        var payload = new QueryResponse<PlayerContextContract>(
            Available: true,
            Reason: null,
            ContractVersion: ContractVersion.Current,
            CapturedAt: new DateTimeOffset(2026, 3, 20, 0, 0, 0, TimeSpan.Zero),
            SnapshotAgeMs: 100,
            Data: new PlayerContextContract(
                "Alice",
                "Tonberry",
                "Tonberry",
                24,
                "White Mage",
                100,
                1,
                "Limsa",
                2,
                "Lower Decks",
                new PositionContract(1, 2, 3, "coarse"),
                false,
                false,
                false,
                false,
                false,
                false,
                "city",
                "idle",
                "Lv100 White Mage"));

        var json = JsonSerializer.Serialize(payload, BridgeJson.Options);
        var roundTrip = JsonSerializer.Deserialize<QueryResponse<PlayerContextContract>>(json, BridgeJson.Options);

        Assert.NotNull(roundTrip);
        Assert.True(roundTrip.Available);
        Assert.Equal("Alice", roundTrip.Data?.CharacterName);
    }

    [Fact]
    public void BridgeRequestEnvelope_SerializesEmptyRequestPayload()
    {
        var envelope = new BridgeRequestEnvelope(
            ContractVersion.Current,
            "GetPlayerContext",
            "req-1",
            new EmptyRequest());

        var json = JsonSerializer.Serialize(envelope, BridgeJson.Options);

        Assert.Contains("GetPlayerContext", json, StringComparison.Ordinal);
        Assert.Contains("req-1", json, StringComparison.Ordinal);
    }

    [Fact]
    public void CapabilityStateResponse_RetainsEnabledCollections()
    {
        var response = new CapabilityStateResponse(
            ContractVersion.Current,
            ["get_player_context"],
            ["ffxiv://player/context"],
            ["Inventory"],
            ObservationProfileEnabled: true,
            ActionProfileEnabled: false);

        var json = JsonSerializer.Serialize(response, BridgeJson.Options);
        var roundTrip = JsonSerializer.Deserialize<CapabilityStateResponse>(json, BridgeJson.Options);

        Assert.NotNull(roundTrip);
        Assert.Contains("get_player_context", roundTrip.EnabledTools);
        Assert.Contains("Inventory", roundTrip.EnabledAddons);
    }

    [Fact]
    public void QueryResponse_SessionStateContract_RoundTrips()
    {
        var payload = new QueryResponse<SessionStateContract>(
            Available: true,
            Reason: null,
            ContractVersion: ContractVersion.Current,
            CapturedAt: new DateTimeOffset(2026, 3, 20, 0, 0, 0, TimeSpan.Zero),
            SnapshotAgeMs: 50,
            Data: new SessionStateContract(
                "DalamudMCP.123",
                true,
                1,
                2,
                [
                    new SessionComponentContract("player_context", true, "ready"),
                    new SessionComponentContract("addon_tree", false, "not_attached"),
                ],
                "1/2 readers ready; bridge server running."));

        var json = JsonSerializer.Serialize(payload, BridgeJson.Options);
        var roundTrip = JsonSerializer.Deserialize<QueryResponse<SessionStateContract>>(json, BridgeJson.Options);

        Assert.NotNull(roundTrip);
        Assert.True(roundTrip.Available);
        Assert.Equal("DalamudMCP.123", roundTrip.Data?.PipeName);
        Assert.Equal(2, roundTrip.Data?.Components.Count);
    }

    [Fact]
    public void BridgeResponseEnvelope_RoundTripsQueryPayload()
    {
        var envelope = new BridgeResponseEnvelope(
            ContractVersion.Current,
            "req-7",
            "query",
            Success: true,
            ErrorCode: null,
            ErrorMessage: null,
            Payload: new QueryResponse<DutyContextContract>(
                Available: true,
                Reason: null,
                ContractVersion: ContractVersion.Current,
                CapturedAt: new DateTimeOffset(2026, 3, 20, 0, 0, 0, TimeSpan.Zero),
                SnapshotAgeMs: 42,
                Data: new DutyContextContract(777, "Praetorium", "Dungeon", true, false, "In duty")));

        var json = JsonSerializer.Serialize(envelope, BridgeJson.Options);
        var roundTrip = JsonSerializer.Deserialize<BridgeResponseEnvelope>(json, BridgeJson.Options);
        var payload = BridgeJson.DeserializePayload<QueryResponse<DutyContextContract>>(roundTrip?.Payload);

        Assert.NotNull(roundTrip);
        Assert.True(roundTrip.Success);
        Assert.Equal("req-7", roundTrip.RequestId);
        Assert.NotNull(payload);
        Assert.Equal("Praetorium", payload.Data?.DutyName);
    }

    [Fact]
    public void AddonRequest_RoundTripsThroughEnvelopePayload()
    {
        var envelope = new BridgeRequestEnvelope(
            ContractVersion.Current,
            "GetAddonTree",
            "req-addon",
            new AddonRequest("Inventory"));

        var json = JsonSerializer.Serialize(envelope, BridgeJson.Options);
        var roundTrip = JsonSerializer.Deserialize<BridgeRequestEnvelope>(json, BridgeJson.Options);
        var payload = BridgeJson.DeserializePayload<AddonRequest>(roundTrip?.Payload);

        Assert.NotNull(roundTrip);
        Assert.Equal(ContractVersion.Current, roundTrip.ContractVersion);
        Assert.NotNull(payload);
        Assert.Equal("Inventory", payload.AddonName);
    }
}
