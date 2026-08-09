using DalamudMCP.Plugin.Services;
using DalamudMCP.Protocol;
using Manifold;
using MemoryPack;

namespace DalamudMCP.Plugin.Operations;

[Operation("game-data.list-sheets", Description = "Lists available Lumina Excel sheets with cursor pagination.", Summary = "Lists Excel sheets.")]
[ResultFormatter(typeof(GameDataListSheetsOperation.TextFormatter))]
[CliCommand("game-data", "list-sheets")]
[McpTool("game_data_list_sheets")]
public sealed partial class GameDataListSheetsOperation
    : IOperation<GameDataListSheetsOperation.Request, GameDataSheetListResult>
{
    private readonly GameDataSheetService service;

    public GameDataListSheetsOperation(GameDataSheetService service)
    {
        this.service = service ?? throw new ArgumentNullException(nameof(service));
    }

    public ValueTask<GameDataSheetListResult> ExecuteAsync(Request request, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        context.CancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(service.ListSheets(request.Query, request.Cursor, request.Limit));
    }

    [MemoryPackable]
    [ProtocolOperation("game-data.list-sheets")]
    public sealed partial class Request
    {
        [Option("query", Description = "Optional case-insensitive sheet name filter.", Required = false)]
        public string? Query { get; init; }

        [Option("cursor", Description = "Opaque cursor returned by a previous call.", Required = false)]
        public string? Cursor { get; init; }

        [Option("limit", Description = "Maximum number of sheets to return, from 1 through 100.", Required = false)]
        public int? Limit { get; init; }
    }

    public sealed class TextFormatter : IResultFormatter<GameDataSheetListResult>
    {
        public string? FormatText(GameDataSheetListResult result, OperationContext context) => result.SummaryText;
    }
}

[Operation("game-data.describe-sheet", Description = "Describes fields and row count for a typed or raw Lumina Excel sheet.", Summary = "Describes an Excel sheet.")]
[ResultFormatter(typeof(GameDataDescribeSheetOperation.TextFormatter))]
[CliCommand("game-data", "describe-sheet")]
[McpTool("game_data_describe_sheet")]
public sealed partial class GameDataDescribeSheetOperation
    : IOperation<GameDataDescribeSheetOperation.Request, GameDataSheetDescribeResult>
{
    private readonly GameDataSheetService service;

    public GameDataDescribeSheetOperation(GameDataSheetService service)
    {
        this.service = service ?? throw new ArgumentNullException(nameof(service));
    }

    public ValueTask<GameDataSheetDescribeResult> ExecuteAsync(Request request, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        context.CancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(service.DescribeSheet(request.Sheet, request.Language));
    }

    [MemoryPackable]
    [ProtocolOperation("game-data.describe-sheet")]
    public sealed partial class Request
    {
        [Option("sheet", Description = "Exact Excel sheet name.")]
        public string Sheet { get; init; } = string.Empty;

        [Option("language", Description = "Optional language such as en, ja, de, fr, chs, cht, or ko.", Required = false)]
        public string? Language { get; init; }
    }

    public sealed class TextFormatter : IResultFormatter<GameDataSheetDescribeResult>
    {
        public string? FormatText(GameDataSheetDescribeResult result, OperationContext context) => result.SummaryText;
    }
}

[Operation("game-data.get-row", Description = "Gets a row from any typed or raw Lumina Excel sheet and projects selected fields.", Summary = "Gets an Excel row.")]
[ResultFormatter(typeof(GameDataGetRowOperation.TextFormatter))]
[CliCommand("game-data", "get-row")]
[McpTool("game_data_get_row")]
public sealed partial class GameDataGetRowOperation
    : IOperation<GameDataGetRowOperation.Request, GameDataRowResult>
{
    private readonly GameDataSheetService service;

    public GameDataGetRowOperation(GameDataSheetService service)
    {
        this.service = service ?? throw new ArgumentNullException(nameof(service));
    }

    public ValueTask<GameDataRowResult> ExecuteAsync(Request request, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        context.CancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(service.GetRow(request.Sheet, request.RowId, request.Fields, request.Language));
    }

    [MemoryPackable]
    [ProtocolOperation("game-data.get-row")]
    public sealed partial class Request
    {
        [Option("sheet", Description = "Exact Excel sheet name.")]
        public string Sheet { get; init; } = string.Empty;

        [Option("row-id", Description = "Row ID in the UInt32 range.")]
        public long RowId { get; init; }

        [Option("fields", Description = "Optional field names to project. Raw fields use RowId and columnN.", Required = false)]
        public string[]? Fields { get; init; }

        [Option("language", Description = "Optional language such as en, ja, de, fr, chs, cht, or ko.", Required = false)]
        public string? Language { get; init; }
    }

    public sealed class TextFormatter : IResultFormatter<GameDataRowResult>
    {
        public string? FormatText(GameDataRowResult result, OperationContext context) => result.SummaryText;
    }
}

[Operation("game-data.search", Description = "Searches projected values in any typed or raw Lumina Excel sheet with bounded scanning and cursor pagination.", Summary = "Searches an Excel sheet.")]
[ResultFormatter(typeof(GameDataSearchOperation.TextFormatter))]
[CliCommand("game-data", "search")]
[McpTool("game_data_search")]
public sealed partial class GameDataSearchOperation
    : IOperation<GameDataSearchOperation.Request, GameDataSearchResult>
{
    private readonly GameDataSheetService service;

    public GameDataSearchOperation(GameDataSheetService service)
    {
        this.service = service ?? throw new ArgumentNullException(nameof(service));
    }

    public ValueTask<GameDataSearchResult> ExecuteAsync(Request request, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        return ValueTask.FromResult(service.Search(
            request.Sheet,
            request.Query,
            request.Fields,
            request.Where,
            request.Language,
            request.Cursor,
            request.Limit,
            context.CancellationToken));
    }

    [MemoryPackable]
    [ProtocolOperation("game-data.search")]
    public sealed partial class Request
    {
        [Option("sheet", Description = "Exact Excel sheet name.")]
        public string Sheet { get; init; } = string.Empty;

        [Option("query", Description = "Optional case-insensitive substring matched against projected JSON values.", Required = false)]
        public string? Query { get; init; }

        [Option("fields", Description = "Optional field names to project and search. Raw fields use RowId and columnN.", Required = false)]
        public string[]? Fields { get; init; }

        [Option("where", Description = "Optional JSON predicate: {field, operator, value}. Operators: eq, ne, contains, startsWith, endsWith, gt, gte, lt, lte.", Required = false)]
        public string? Where { get; init; }

        [Option("language", Description = "Optional language such as en, ja, de, fr, chs, cht, or ko.", Required = false)]
        public string? Language { get; init; }

        [Option("cursor", Description = "Opaque cursor returned by a previous call.", Required = false)]
        public string? Cursor { get; init; }

        [Option("limit", Description = "Maximum number of matching rows to return, from 1 through 100.", Required = false)]
        public int? Limit { get; init; }
    }

    public sealed class TextFormatter : IResultFormatter<GameDataSearchResult>
    {
        public string? FormatText(GameDataSearchResult result, OperationContext context) => result.SummaryText;
    }
}
