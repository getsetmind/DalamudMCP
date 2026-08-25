using System.Text.Json;

namespace DalamudMCP.Protocol.Tests;

public sealed class ProtocolJsonSchemaBuilderTests
{
    [Fact]
    public void Create_builds_closed_camel_case_object_schema()
    {
        using JsonDocument document = JsonDocument.Parse(ProtocolJsonSchemaBuilder.Create(typeof(TestResult)));
        JsonElement root = document.RootElement;

        Assert.Equal("object", root.GetProperty("type").GetString());
        Assert.False(root.GetProperty("additionalProperties").GetBoolean());
        JsonElement properties = root.GetProperty("properties");
        Assert.Equal("boolean", properties.GetProperty("success").GetProperty("type").GetString());
        Assert.Equal("array", properties.GetProperty("items").GetProperty("type").GetString());
        Assert.Equal("string", properties.GetProperty("mode").GetProperty("type").GetString());
        Assert.Equal(
            ["one", "two"],
            properties.GetProperty("mode").GetProperty("enum").EnumerateArray().Select(static value => value.GetString()));
    }

    [Fact]
    public void Create_stops_recursive_cycles()
    {
        using JsonDocument document = JsonDocument.Parse(ProtocolJsonSchemaBuilder.Create(typeof(RecursiveResult)));

        JsonElement next = document.RootElement.GetProperty("properties").GetProperty("next");
        Assert.Equal("object", next.GetProperty("type").GetString());
    }

    private sealed record TestResult(bool Success, TestMode Mode, string[] Items);

    private sealed class RecursiveResult
    {
        public RecursiveResult? Next { get; init; }
    }

    private enum TestMode
    {
        One = 0,
        Two = 1,
    }
}
