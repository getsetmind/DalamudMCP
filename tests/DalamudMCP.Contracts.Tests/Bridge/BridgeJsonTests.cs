using System.Text.Json;
using DalamudMCP.Contracts.Bridge;
using DalamudMCP.Contracts.Bridge.Requests;

namespace DalamudMCP.Contracts.Tests.Bridge;

public sealed class BridgeJsonTests
{
    [Fact]
    public void DeserializePayload_ReadsJsonElementPayload()
    {
        var payload = JsonSerializer.SerializeToElement(new AddonRequest("Inventory"), BridgeJson.Options);

        var request = BridgeJson.DeserializePayload<AddonRequest>(payload);

        Assert.NotNull(request);
        Assert.Equal("Inventory", request.AddonName);
    }

    [Fact]
    public void SerializeAndDeserialize_RoundTripsEnvelope()
    {
        var envelope = new BridgeRequestEnvelope(
            ContractVersion.Current,
            "GetAddonStrings",
            "req-42",
            new AddonRequest("Inventory"));

        var json = BridgeJson.Serialize(envelope);
        var roundTrip = BridgeJson.Deserialize<BridgeRequestEnvelope>(json);

        Assert.NotNull(roundTrip);
        Assert.Equal("req-42", roundTrip.RequestId);
    }

    [Theory]
    [InlineData("1.0.0", true)]
    [InlineData("1.4.2", true)]
    [InlineData("2.0.0", false)]
    [InlineData("foo", false)]
    [InlineData("", false)]
    public void IsCompatible_UsesMajorVersionCompatibility(string version, bool expected)
    {
        Assert.Equal(expected, ContractVersion.IsCompatible(version));
    }
}
