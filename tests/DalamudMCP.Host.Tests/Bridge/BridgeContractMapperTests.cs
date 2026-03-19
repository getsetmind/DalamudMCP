using DalamudMCP.Application.Common;
using DalamudMCP.Domain.Policy;
using DalamudMCP.Domain.Snapshots;
using DalamudMCP.Infrastructure.Bridge;

namespace DalamudMCP.Host.Tests.Bridge;

public sealed class BridgeContractMapperTests
{
    [Fact]
    public void ToResponse_MapsSuccessfulPlayerContextQuery()
    {
        var snapshot = new PlayerContextSnapshot(
            new DateTimeOffset(2026, 3, 20, 1, 0, 0, TimeSpan.Zero),
            "Alice",
            "Tonberry",
            "Tonberry",
            24,
            "White Mage",
            100,
            128,
            "Limsa Lominsa",
            256,
            "Lower Decks",
            new PositionSnapshot(10, 20, 30, "coarse"),
            false,
            false,
            false,
            false,
            false,
            false,
            "city",
            "idle",
            "Lv100 White Mage");
        var result = QueryResults.Success(snapshot, snapshot.CapturedAt, 250);

        var response = BridgeContractMapper.ToResponse(result);

        Assert.True(response.Available);
        Assert.Equal(250, response.SnapshotAgeMs);
        Assert.Equal("Alice", response.Data?.CharacterName);
        Assert.Equal("coarse", response.Data?.Position?.Precision);
    }

    [Fact]
    public void ToResponse_MapsCapabilityStateCollectionsInSortedOrder()
    {
        var policy = new ExposurePolicy(
            enabledTools: ["z_tool", "a_tool"],
            enabledResources: ["ffxiv://z", "ffxiv://a"],
            enabledAddons: ["Zoo", "Alpha"],
            observationProfileEnabled: true,
            actionProfileEnabled: false);

        var response = BridgeContractMapper.ToResponse(policy);

        Assert.Equal(["a_tool", "z_tool"], response.EnabledTools);
        Assert.Equal(["ffxiv://a", "ffxiv://z"], response.EnabledResources);
        Assert.Equal(["Alpha", "Zoo"], response.EnabledAddons);
    }
}
