using System.Globalization;
using System.Runtime.Versioning;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using Manifold;
using DalamudMCP.Plugin.Readers;
using DalamudMCP.Protocol;
using MemoryPack;

namespace DalamudMCP.Plugin.Operations;

[Operation(
    "duty.context",
    Description = "Gets the current duty context.",
    Summary = "Gets duty context.")]
[ResultFormatter(typeof(DutyContextOperation.TextFormatter))]
[CliCommand("duty", "context")]
[McpTool("get_duty_context")]
public sealed partial class DutyContextOperation
    : IOperation<DutyContextOperation.Request, DutyContextSnapshot>, IPluginReaderStatus
{
    private readonly Func<CancellationToken, ValueTask<DutyContextSnapshot>> executor;
    private readonly Func<bool>? isReadyProvider;
    private readonly Func<string>? detailProvider;
    private readonly string unavailableDetail;

    [SupportedOSPlatform("windows")]
    public DutyContextOperation(
        IFramework framework,
        IClientState clientState,
        ICondition condition)
    {
        ArgumentNullException.ThrowIfNull(framework);
        ArgumentNullException.ThrowIfNull(clientState);
        ArgumentNullException.ThrowIfNull(condition);

        executor = CreateDalamudExecutor(framework, clientState, condition);
        isReadyProvider = () => clientState.IsLoggedIn;
        detailProvider = () => clientState.IsLoggedIn ? "ready" : "not_logged_in";
        unavailableDetail = "not_logged_in";
    }

    internal DutyContextOperation(
        Func<CancellationToken, ValueTask<DutyContextSnapshot>> executor,
        bool isReady = true,
        string detail = "ready")
    {
        this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
        isReadyProvider = () => isReady;
        detailProvider = () => string.IsNullOrWhiteSpace(detail) ? "ready" : detail;
        unavailableDetail = string.IsNullOrWhiteSpace(detail) ? "ready" : detail;
    }

    public string ReaderKey => "duty.context";

    public bool IsReady => isReadyProvider?.Invoke() ?? false;

    public string Detail => detailProvider?.Invoke() ?? unavailableDetail;

    public ValueTask<DutyContextSnapshot> ExecuteAsync(Request request, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        return executor(context.CancellationToken);
    }

    [MemoryPackable]
    [ProtocolOperation("duty.context")]
    [LegacyBridgeRequest("GetDutyContext")]
    public sealed partial record Request;

    public sealed class TextFormatter : IResultFormatter<DutyContextSnapshot>
    {
        public string? FormatText(DutyContextSnapshot result, OperationContext context)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(context);

            return result.SummaryText;
        }
    }

    [SupportedOSPlatform("windows")]
    private static Func<CancellationToken, ValueTask<DutyContextSnapshot>> CreateDalamudExecutor(
        IFramework framework,
        IClientState clientState,
        ICondition condition)
    {
        return async cancellationToken =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (framework.IsInFrameworkUpdateThread)
                return ReadCurrentCore(clientState, condition, cancellationToken);

            return await framework.RunOnFrameworkThread(() =>
                    ReadCurrentCore(clientState, condition, cancellationToken))
                .ConfigureAwait(false);
        };
    }

    [SupportedOSPlatform("windows")]
    private static DutyContextSnapshot ReadCurrentCore(
        IClientState clientState,
        ICondition condition,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!clientState.IsLoggedIn)
            throw new InvalidOperationException("Duty context is not available because the local player is not logged in.");

        int? territoryId = ConvertToNullableInt(clientState.TerritoryType);
        bool inDuty = condition.Any(
            ConditionFlag.BoundByDuty,
            ConditionFlag.BoundByDuty56,
            ConditionFlag.BoundByDuty95);
        string? dutyName = inDuty ? FormatDutyName(territoryId) : null;
        string dutyType = inDuty ? "duty" : "world";
        bool isDutyComplete = condition.Any(
            ConditionFlag.WatchingCutscene78,
            ConditionFlag.OccupiedInQuestEvent);
        string summaryText = FormatDutySummary(inDuty, dutyName ?? "Unknown duty", territoryId, isDutyComplete);

        return new DutyContextSnapshot(
            territoryId,
            dutyName,
            dutyType,
            inDuty,
            isDutyComplete,
            summaryText);
    }

    private static int? ConvertToNullableInt<T>(T value)
        where T : struct
    {
        try
        {
            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }

    private static string FormatDutyName(int? territoryId)
    {
        return territoryId is null
            ? "Unknown duty"
            : $"Territory#{territoryId.Value.ToString(CultureInfo.InvariantCulture)}";
    }

    private static string FormatDutySummary(bool inDuty, string dutyName, int? territoryId, bool isDutyComplete)
    {
        if (!inDuty)
        {
            return territoryId is null
                ? "Not currently in duty."
                : $"Not currently in duty; current territory is {territoryId.Value.ToString(CultureInfo.InvariantCulture)}.";
        }

        string completionText = isDutyComplete ? " Duty completion has been detected." : string.Empty;
        return $"{dutyName} is active.{completionText}";
    }
}

[MemoryPackable]
public sealed partial record DutyContextSnapshot(
    int? TerritoryId,
    string? DutyName,
    string DutyType,
    bool InDuty,
    bool IsDutyComplete,
    string SummaryText);