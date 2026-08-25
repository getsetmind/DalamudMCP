using DalamudMCP.Protocol;
using Manifold;
using MemoryPack;

namespace DalamudMCP.Plugin.Operations;

[Operation(
    "game.action.resolve",
    Description = "Resolves an FFXIV action and target, returning current executability and a target revision without executing it.",
    Summary = "Resolves a game action without executing it.")]
[ResultFormatter(typeof(GameActionResolveOperation.TextFormatter))]
[CliCommand("game", "action", "resolve")]
[McpTool("game_action_resolve")]
public sealed partial class GameActionResolveOperation(GameActionExecuteOperation executeOperation)
    : IOperation<GameActionResolveOperation.Request, GameActionExecuteResult>
{
    public ValueTask<GameActionExecuteResult> ExecuteAsync(Request request, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        return executeOperation.ExecuteAsync(
            new GameActionExecuteOperation.Request
            {
                ActionType = request.ActionType,
                ActionId = request.ActionId,
                ActionName = request.ActionName,
                TargetObjectId = request.TargetObjectId,
                RequiredClassJobId = request.RequiredClassJobId,
                RequiredTerritoryId = request.RequiredTerritoryId,
                DryRun = true,
            },
            context);
    }

    [MemoryPackable]
    [ProtocolOperation("game.action.resolve")]
    public sealed partial class Request
    {
        [Option("action-type", Description = "FFXIV action type such as Action, GeneralAction, Item, Mount, PetAction, or PvPAction.", Required = false)]
        public string? ActionType { get; init; }

        [Option("action-id", Description = "Numeric action identifier. Supply action-id or action-name.", Required = false)]
        public long? ActionId { get; init; }

        [Option("action-name", Description = "Exact Action sheet name. Name lookup is available when action-type is Action.", Required = false)]
        public string? ActionName { get; init; }

        [Option("target-object-id", Description = "Target GameObject ID in decimal or 0x-prefixed hexadecimal.", Required = false)]
        public string? TargetObjectId { get; init; }

        [Option("required-class-job-id", Description = "Require the local player's current ClassJob row ID to match.", Required = false)]
        public long? RequiredClassJobId { get; init; }

        [Option("required-territory-id", Description = "Require the current TerritoryType row ID to match.", Required = false)]
        public long? RequiredTerritoryId { get; init; }
    }

    public sealed class TextFormatter : IResultFormatter<GameActionExecuteResult>
    {
        public string? FormatText(GameActionExecuteResult result, OperationContext context) => result.SummaryText;
    }
}
