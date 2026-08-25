using System.Globalization;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using Dalamud.Plugin.Services;
using DalamudMCP.Plugin.Services;
using DalamudMCP.Protocol;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel;
using Manifold;
using MemoryPack;
using LuminaAction = Lumina.Excel.Sheets.Action;

namespace DalamudMCP.Plugin.Operations;

[Operation(
    "game.action.execute",
    Description = "Resolves and executes an arbitrary FFXIV action permitted by the plugin configuration. Use dry-run to inspect action status without executing it.",
    Summary = "Resolves and executes a game action.")]
[ResultFormatter(typeof(GameActionExecuteOperation.TextFormatter))]
[CliCommand("game", "action", "execute")]
[McpTool("game_action_execute")]
public sealed partial class GameActionExecuteOperation
    : IOperation<GameActionExecuteOperation.Request, GameActionExecuteResult>
{
    private const ulong DefaultTargetId = 0xE0000000;
    private readonly Func<Request, CancellationToken, ValueTask<GameActionExecuteResult>> executor;
    private readonly GameEventBufferService? events;

    [SupportedOSPlatform("windows")]
    public GameActionExecuteOperation(
        IFramework framework,
        IClientState clientState,
        IObjectTable objectTable,
        IDataManager dataManager,
        GameEventBufferService events)
    {
        ArgumentNullException.ThrowIfNull(framework);
        ArgumentNullException.ThrowIfNull(clientState);
        ArgumentNullException.ThrowIfNull(objectTable);
        ArgumentNullException.ThrowIfNull(dataManager);
        ArgumentNullException.ThrowIfNull(events);

        executor = CreateDalamudExecutor(framework, clientState, objectTable, dataManager);
        this.events = events;
    }

    internal GameActionExecuteOperation(
        Func<Request, CancellationToken, ValueTask<GameActionExecuteResult>> executor,
        GameEventBufferService? events = null)
    {
        this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
        this.events = events;
    }

    public async ValueTask<GameActionExecuteResult> ExecuteAsync(Request request, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        GameActionExecuteResult result = await executor(request, context.CancellationToken).ConfigureAwait(false);
        if (request.DryRun ?? false)
            return result;

        string correlationId = result.CorrelationId ?? Guid.NewGuid().ToString("N");
        GameActionExecuteResult correlated = result with { CorrelationId = correlationId };
        events?.Publish(result.Accepted ? "action.accepted" : "action.rejected", correlated, correlationId);
        return correlated;
    }

    [MemoryPackable]
    [ProtocolOperation("game.action.execute")]
    public sealed partial class Request
    {
        [Option("action-type", Description = "FFXIV action type such as Action, GeneralAction, Item, Mount, PetAction, or PvPAction.", Required = false)]
        public string? ActionType { get; init; }

        [Option("action-id", Description = "Numeric action identifier. Supply action-id or action-name.", Required = false)]
        public long? ActionId { get; init; }

        [Option("action-name", Description = "Exact Action sheet name. Name lookup is available when action-type is Action.", Required = false)]
        public string? ActionName { get; init; }

        [Option("target-object-id", Description = "Target GameObject ID in decimal or 0x-prefixed hexadecimal. Omit to use the game's default target.", Required = false)]
        public string? TargetObjectId { get; init; }

        [Option("expected-revision", Description = "Target revision returned by game_action_resolve. Execution is rejected when the target snapshot changed.", Required = false)]
        public string? ExpectedRevision { get; init; }

        [Option("required-class-job-id", Description = "Require the local player's current ClassJob row ID to match before execution.", Required = false)]
        public long? RequiredClassJobId { get; init; }

        [Option("required-territory-id", Description = "Require the current TerritoryType row ID to match before execution.", Required = false)]
        public long? RequiredTerritoryId { get; init; }

        [Option("mode", Description = "UseAction mode: None, Queue, Macro, or Combo.", Required = false)]
        public string? Mode { get; init; }

        [Option("extra-param", Description = "Optional low-level UseAction parameter in the UInt32 range.", Required = false)]
        public long? ExtraParam { get; init; }

        [Option("combo-route-id", Description = "Optional combo route identifier in the UInt32 range.", Required = false)]
        public long? ComboRouteId { get; init; }

        [Option("dry-run", Description = "Resolve the action and query its current status without executing it.", Required = false)]
        public bool? DryRun { get; init; }
    }

    public sealed class TextFormatter : IResultFormatter<GameActionExecuteResult>
    {
        public string? FormatText(GameActionExecuteResult result, OperationContext context)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(context);
            return result.SummaryText;
        }
    }

    [SupportedOSPlatform("windows")]
    private static Func<Request, CancellationToken, ValueTask<GameActionExecuteResult>> CreateDalamudExecutor(
        IFramework framework,
        IClientState clientState,
        IObjectTable objectTable,
        IDataManager dataManager)
    {
        return async (request, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (framework.IsInFrameworkUpdateThread)
                return ExecuteCore(request, clientState, objectTable, dataManager, cancellationToken);

            return await framework.RunOnFrameworkThread(
                    () => ExecuteCore(request, clientState, objectTable, dataManager, cancellationToken))
                .ConfigureAwait(false);
        };
    }

    [SupportedOSPlatform("windows")]
    private static unsafe GameActionExecuteResult ExecuteCore(
        Request request,
        IClientState clientState,
        IObjectTable objectTable,
        IDataManager dataManager,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!clientState.IsLoggedIn)
            return Failure(request, "not_logged_in", "The player is not logged in.");

        if (request.RequiredClassJobId.HasValue &&
            (objectTable.LocalPlayer is null || objectTable.LocalPlayer.ClassJob.RowId != request.RequiredClassJobId.Value))
        {
            return Failure(request, "class_job_mismatch", "The local player's current ClassJob does not match required-class-job-id.");
        }

        if (request.RequiredTerritoryId.HasValue && clientState.TerritoryType != request.RequiredTerritoryId.Value)
            return Failure(request, "territory_mismatch", "The current territory does not match required-territory-id.");

        if (!TryParseActionType(request.ActionType, out ActionType actionType))
            return Failure(request, "invalid_action_type", $"Unsupported action type '{request.ActionType}'.");

        if (!TryParseUseActionMode(request.Mode, out ActionManager.UseActionMode mode))
            return Failure(request, "invalid_mode", $"Unsupported UseAction mode '{request.Mode}'.");

        if (!TryToUInt32(request.ExtraParam, out uint extraParam) ||
            !TryToUInt32(request.ComboRouteId, out uint comboRouteId))
        {
            return Failure(request, "invalid_low_level_parameter", "extra-param and combo-route-id must be in the UInt32 range.");
        }

        if (!TryResolveAction(
                dataManager,
                actionType,
                request.ActionId,
                request.ActionName,
                out uint actionId,
                out string? actionName,
                out GameActionCandidate[] candidates,
                out string? resolutionError))
        {
            return Failure(
                request,
                candidates.Length > 1 ? "ambiguous_action_name" : "action_not_found",
                resolutionError ?? "The action could not be resolved.",
                candidates);
        }

        if (!TryResolveTarget(
                objectTable,
                request.TargetObjectId,
                out ulong targetId,
                out string targetRevision,
                out string? targetError))
        {
            return Failure(request, "target_not_found", targetError ?? "The target could not be resolved.");
        }

        if (!string.IsNullOrWhiteSpace(request.ExpectedRevision) &&
            !string.Equals(request.ExpectedRevision.Trim(), targetRevision, StringComparison.Ordinal))
        {
            return Failure(
                request,
                "target_revision_mismatch",
                "The target snapshot changed after resolution. Resolve the action again before executing it.");
        }

        ActionManager* actionManager = ActionManager.Instance();
        if (actionManager is null)
            return Failure(request, "action_manager_unavailable", "ActionManager is unavailable.");

        uint adjustedActionId = actionType == ActionType.Action
            ? actionManager->GetAdjustedActionId(actionId)
            : actionId;
        uint statusCode = actionManager->GetActionStatus(actionType, actionId, targetId, false, false, null);
        bool executable = statusCode == 0;
        bool dryRun = request.DryRun ?? false;
        if (dryRun)
        {
            return new GameActionExecuteResult(
                true,
                false,
                null,
                actionType.ToString(),
                actionId,
                adjustedActionId,
                actionName,
                FormatGameObjectId(targetId),
                true,
                executable,
                statusCode,
                $"Resolved {actionType} {actionId} without executing it. Current status code: {statusCode}.",
                TargetRevision: targetRevision);
        }

        bool accepted = actionManager->UseAction(actionType, actionId, targetId, extraParam, mode, comboRouteId, null);
        return new GameActionExecuteResult(
            accepted,
            accepted,
            accepted ? null : "action_rejected",
            actionType.ToString(),
            actionId,
            adjustedActionId,
            actionName,
            FormatGameObjectId(targetId),
            false,
            executable,
            statusCode,
            accepted
                ? $"ActionManager accepted {actionType} {actionId}."
                : $"ActionManager rejected {actionType} {actionId}; preflight status code was {statusCode}.",
            TargetRevision: targetRevision);
    }

    internal static bool TryParseActionType(string? value, out ActionType actionType)
    {
        string normalized = string.IsNullOrWhiteSpace(value) ? nameof(ActionType.Action) : value.Trim();
        return Enum.TryParse(normalized, ignoreCase: true, out actionType) &&
               Enum.IsDefined(actionType) &&
               actionType != ActionType.None;
    }

    internal static bool TryParseUseActionMode(string? value, out ActionManager.UseActionMode mode)
    {
        string normalized = string.IsNullOrWhiteSpace(value)
            ? nameof(ActionManager.UseActionMode.None)
            : value.Trim();
        return Enum.TryParse(normalized, ignoreCase: true, out mode) && Enum.IsDefined(mode);
    }

    private static bool TryResolveAction(
        IDataManager dataManager,
        ActionType actionType,
        long? requestedActionId,
        string? requestedActionName,
        out uint actionId,
        out string? actionName,
        out GameActionCandidate[] candidates,
        out string? error)
    {
        actionId = 0;
        actionName = null;
        candidates = [];
        error = null;

        if (requestedActionId is >= 1 and <= uint.MaxValue)
        {
            actionId = checked((uint)requestedActionId.Value);
            actionName = ResolveActionName(dataManager, actionType, actionId);
            return true;
        }

        if (requestedActionId is not null)
        {
            error = "action-id must be between 1 and UInt32.MaxValue.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(requestedActionName))
        {
            error = "Supply action-id or action-name.";
            return false;
        }

        if (actionType != ActionType.Action)
        {
            error = "action-name lookup is only available for action-type Action.";
            return false;
        }

        string normalizedName = requestedActionName.Trim();
        ExcelSheet<LuminaAction>? sheet = dataManager.GetExcelSheet<LuminaAction>();
        if (sheet is null)
        {
            error = "The Action sheet is unavailable.";
            return false;
        }

        GameActionCandidate[] exactMatches = sheet
            .Where(row => row.RowId != 0 && string.Equals(row.Name.ToString(), normalizedName, StringComparison.OrdinalIgnoreCase))
            .Select(static row => new GameActionCandidate(row.RowId, row.Name.ToString()))
            .Take(20)
            .ToArray();
        if (exactMatches.Length == 1)
        {
            actionId = exactMatches[0].ActionId;
            actionName = exactMatches[0].ActionName;
            candidates = exactMatches;
            return true;
        }

        if (exactMatches.Length > 1)
        {
            candidates = exactMatches;
            error = $"Action name '{normalizedName}' matched multiple IDs: {string.Join(", ", exactMatches.Select(static match => match.ActionId))}. Supply action-id.";
            return false;
        }

        candidates = sheet
            .Where(row => row.RowId != 0 && row.Name.ToString().Contains(normalizedName, StringComparison.OrdinalIgnoreCase))
            .Select(static row => new GameActionCandidate(row.RowId, row.Name.ToString()))
            .Take(20)
            .ToArray();
        error = candidates.Length == 0
            ? $"No Action sheet row matched '{normalizedName}'."
            : $"No exact Action sheet row matched '{normalizedName}'. Review candidates and supply action-id.";
        return false;
    }

    private static string? ResolveActionName(IDataManager dataManager, ActionType actionType, uint actionId)
    {
        if (actionType != ActionType.Action)
            return null;

        return dataManager.GetExcelSheet<LuminaAction>()?.GetRowOrDefault(actionId)?.Name.ToString();
    }

    private static bool TryResolveTarget(
        IObjectTable objectTable,
        string? requestedTargetId,
        out ulong targetId,
        out string targetRevision,
        out string? error)
    {
        targetId = DefaultTargetId;
        targetRevision = CreateRevision($"default|{DefaultTargetId:X}");
        error = null;
        if (string.IsNullOrWhiteSpace(requestedTargetId))
            return true;

        string value = requestedTargetId.Trim();
        NumberStyles style = NumberStyles.Integer;
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            value = value[2..];
            style = NumberStyles.HexNumber;
        }

        if (!ulong.TryParse(value, style, CultureInfo.InvariantCulture, out ulong parsed))
        {
            error = $"'{requestedTargetId}' is not a valid game object ID.";
            return false;
        }

        if (parsed == DefaultTargetId)
        {
            targetId = parsed;
            return true;
        }

        var target = objectTable.FirstOrDefault(candidate => candidate is not null && candidate.GameObjectId == parsed);
        if (target is null)
        {
            error = $"Game object {FormatGameObjectId(parsed)} is not present in the current ObjectTable snapshot.";
            return false;
        }

        string targetName = string.IsNullOrWhiteSpace(target.Name.TextValue)
            ? string.Empty
            : target.Name.TextValue;
        targetRevision = CreateRevision(string.Create(
            CultureInfo.InvariantCulture,
            $"{target.GameObjectId:X}|{target.ObjectKind}|{targetName}|{target.Position.X:R}|{target.Position.Y:R}|{target.Position.Z:R}"));

        targetId = parsed;
        return true;
    }

    private static bool TryToUInt32(long? value, out uint result)
    {
        if (value is null)
        {
            result = 0;
            return true;
        }

        if (value is < 0 or > uint.MaxValue)
        {
            result = 0;
            return false;
        }

        result = checked((uint)value.Value);
        return true;
    }

    private static string FormatGameObjectId(ulong gameObjectId)
    {
        return $"0x{gameObjectId:X}";
    }

    private static string CreateRevision(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant()[..24];

    private static GameActionExecuteResult Failure(
        Request request,
        string reason,
        string summary,
        GameActionCandidate[]? candidates = null)
    {
        return new GameActionExecuteResult(
            false,
            false,
            reason,
            request.ActionType?.Trim() ?? nameof(ActionType.Action),
            request.ActionId is >= 1 and <= uint.MaxValue ? checked((uint)request.ActionId.Value) : null,
            null,
            request.ActionName?.Trim(),
            request.TargetObjectId?.Trim(),
            request.DryRun ?? false,
            false,
            null,
            summary,
            Candidates: candidates);
    }
}

[MemoryPackable]
public sealed partial record GameActionExecuteResult(
    bool Succeeded,
    bool Accepted,
    string? Reason,
    string ActionType,
    uint? ActionId,
    uint? AdjustedActionId,
    string? ActionName,
    string? TargetObjectId,
    bool DryRun,
    bool Executable,
    uint? StatusCode,
    string SummaryText,
    string? CorrelationId = null,
    string? TargetRevision = null,
    GameActionCandidate[]? Candidates = null);

[MemoryPackable]
public sealed partial record GameActionCandidate(uint ActionId, string ActionName);
