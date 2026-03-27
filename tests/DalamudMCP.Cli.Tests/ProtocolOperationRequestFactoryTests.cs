using System.Text.Json;
using DalamudMCP.Protocol;

namespace DalamudMCP.Cli.Tests;

public sealed class ProtocolOperationRequestFactoryTests
{
    [Fact]
    public void CreateFromCli_reuses_empty_payload_for_parameterless_operations()
    {
        ProtocolOperationDescriptor operation = new(
            "player.context",
            ProtocolOperationVisibility.Both,
            []);

        _ = ProtocolOperationRequestFactory.CreateFromCli(operation, EmptyCliOptions, EmptyCliArguments);

        long before = GC.GetAllocatedBytesForCurrentThread();
        ProtocolRequestPayload payload = ProtocolOperationRequestFactory.CreateFromCli(operation, EmptyCliOptions, EmptyCliArguments);
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, after - before);
        Assert.Equal(ProtocolRequestPayload.None, payload);
    }

    [Fact]
    public void CreateFromMcp_reuses_empty_payload_for_parameterless_operations()
    {
        ProtocolOperationDescriptor operation = new(
            "player.context",
            ProtocolOperationVisibility.Both,
            []);

        _ = ProtocolOperationRequestFactory.CreateFromMcp(operation, null);

        long before = GC.GetAllocatedBytesForCurrentThread();
        ProtocolRequestPayload payload = ProtocolOperationRequestFactory.CreateFromMcp(operation, null);
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, after - before);
        Assert.Equal(ProtocolRequestPayload.None, payload);
    }

    [Fact]
    public void CreateFromCli_serializes_json_without_intermediate_dictionary()
    {
        ProtocolOperationDescriptor operation = new(
            "session.status",
            ProtocolOperationVisibility.Both,
            [
                new ProtocolParameterDescriptor(
                    "pipeName",
                    ProtocolValueKind.Text,
                    ProtocolParameterSource.Option,
                    true,
                    CliName: "pipe")
            ]);

        ProtocolRequestPayload payload = ProtocolOperationRequestFactory.CreateFromCli(
            operation,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["pipe"] = "DalamudMCP.1234"
            },
            EmptyCliArguments);

        Assert.Equal(ProtocolPayloadFormat.Json, payload.Format);
        JsonElement json = JsonSerializer.Deserialize<JsonElement>(payload.Payload!, ProtocolContract.JsonOptions);
        Assert.Equal("DalamudMCP.1234", json.GetProperty("pipeName").GetString());
    }

    [Fact]
    public void CreateFromCli_uses_request_property_name_when_external_option_name_differs()
    {
        ProtocolOperationDescriptor operation = new(
            "target.object",
            ProtocolOperationVisibility.Both,
            [
                new ProtocolParameterDescriptor(
                    "GameObjectId",
                    ProtocolValueKind.Text,
                    ProtocolParameterSource.Option,
                    true,
                    CliName: "game-object-id")
            ]);

        ProtocolRequestPayload payload = ProtocolOperationRequestFactory.CreateFromCli(
            operation,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["game-object-id"] = "0x100762511"
            },
            EmptyCliArguments);

        JsonElement json = JsonSerializer.Deserialize<JsonElement>(payload.Payload!, ProtocolContract.JsonOptions);
        Assert.Equal("0x100762511", json.GetProperty("GameObjectId").GetString());
    }

    [Fact]
    public void CreateFromMcp_uses_request_property_name_when_external_argument_name_differs()
    {
        ProtocolOperationDescriptor operation = new(
            "target.object",
            ProtocolOperationVisibility.Both,
            [
                new ProtocolParameterDescriptor(
                    "GameObjectId",
                    ProtocolValueKind.Text,
                    ProtocolParameterSource.Option,
                    true,
                    McpName: "game-object-id")
            ]);

        using JsonDocument document = JsonDocument.Parse("{\"game-object-id\":\"0x100762511\"}");
        Dictionary<string, JsonElement> arguments = new(StringComparer.Ordinal)
        {
            ["game-object-id"] = document.RootElement.GetProperty("game-object-id").Clone()
        };

        ProtocolRequestPayload payload = ProtocolOperationRequestFactory.CreateFromMcp(operation, arguments);

        JsonElement json = JsonSerializer.Deserialize<JsonElement>(payload.Payload!, ProtocolContract.JsonOptions);
        Assert.Equal("0x100762511", json.GetProperty("GameObjectId").GetString());
    }

    private static readonly IReadOnlyDictionary<string, string> EmptyCliOptions =
        new Dictionary<string, string>(0, StringComparer.Ordinal);

    private static readonly IReadOnlyList<string> EmptyCliArguments = [];
}
