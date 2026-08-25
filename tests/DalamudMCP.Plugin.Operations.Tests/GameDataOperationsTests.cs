using System.Text.Json;
using System.Text.Json.Nodes;
using DalamudMCP.Plugin.Services;
using DalamudMCP.Protocol;
using Lumina.Data;
using Manifold;

namespace DalamudMCP.Plugin.Operations.Tests;

public sealed class GameDataOperationsTests
{
    [Theory]
    [InlineData(typeof(GameDataListSheetsOperation), typeof(GameDataListSheetsOperation.Request), "game-data.list-sheets", "game_data_list_sheets")]
    [InlineData(typeof(GameDataDescribeSheetOperation), typeof(GameDataDescribeSheetOperation.Request), "game-data.describe-sheet", "game_data_describe_sheet")]
    [InlineData(typeof(GameDataGetRowOperation), typeof(GameDataGetRowOperation.Request), "game-data.get-row", "game_data_get_row")]
    [InlineData(typeof(GameDataSearchOperation), typeof(GameDataSearchOperation.Request), "game-data.search", "game_data_search")]
    public void Operations_carry_mcp_and_protocol_metadata(
        Type operationType,
        Type requestType,
        string operationId,
        string toolName)
    {
        Assert.Equal(operationId, operationType.GetCustomAttribute<OperationAttribute>()?.OperationId);
        Assert.Equal(toolName, operationType.GetCustomAttribute<McpToolAttribute>()?.Name);
        Assert.Equal(operationId, requestType.GetCustomAttribute<ProtocolOperationAttribute>()?.OperationId);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("en", Language.English)]
    [InlineData("ja", Language.Japanese)]
    [InlineData("zh-cn", Language.ChineseSimplified)]
    public void TryParseLanguage_accepts_supported_aliases(string? value, Language? expected)
    {
        Assert.True(GameDataSheetService.TryParseLanguage(value, out Language? actual));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TryDecodeCursor_rejects_invalid_values()
    {
        Assert.False(GameDataSheetService.TryDecodeCursor("not-base64", out _));
    }

    [Fact]
    public void ToJsonNode_stops_self_referential_enumerables()
    {
        List<object> values = [];
        values.Add(values);

        JsonNode? result = GameDataSheetService.ToJsonNode(values);

        JsonArray array = Assert.IsType<JsonArray>(result);
        Assert.Equal("<cycle>", array[0]?.GetValue<string>());
    }

    [Fact]
    public void Where_predicate_parses_and_compares_without_executing_code()
    {
        Assert.True(GameDataSheetService.TryParsePredicate(
            "{\"field\":\"Level\",\"operator\":\"gte\",\"value\":90}",
            out GameDataPredicate? predicate,
            out string? error), error);
        using JsonDocument document = JsonDocument.Parse("{\"Level\":100,\"Name\":\"Example\"}");

        Assert.True(GameDataSheetService.TryMatchesPredicate(
            document.RootElement,
            predicate!,
            out bool matches,
            out error), error);
        Assert.True(matches);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"field\":\"Level\",\"operator\":\"execute\",\"value\":1}")]
    [InlineData("not-json")]
    public void Where_predicate_rejects_unsupported_shapes(string json)
    {
        Assert.False(GameDataSheetService.TryParsePredicate(json, out _, out string? error));
        Assert.NotNull(error);
    }
}
