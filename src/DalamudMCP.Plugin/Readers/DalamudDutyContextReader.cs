using System.Globalization;
using System.Runtime.Versioning;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using DalamudMCP.Application.Abstractions.Readers;
using DalamudMCP.Domain.Snapshots;

namespace DalamudMCP.Plugin.Readers;

[SupportedOSPlatform("windows")]
public sealed class DalamudDutyContextReader : IDutyContextReader, IPluginReaderDiagnostics
{
    private readonly IClientState clientState;
    private readonly ICondition condition;
    private readonly IFramework framework;

    public DalamudDutyContextReader(
        IFramework framework,
        IClientState clientState,
        ICondition condition)
    {
        ArgumentNullException.ThrowIfNull(framework);
        ArgumentNullException.ThrowIfNull(clientState);
        ArgumentNullException.ThrowIfNull(condition);
        this.framework = framework;
        this.clientState = clientState;
        this.condition = condition;
    }

    public string ComponentName => "duty_context";

    public bool IsReady => clientState.IsLoggedIn;

    public string Status => IsReady ? "ready" : "not_logged_in";

    public Task<DutyContextSnapshot?> ReadCurrentAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!framework.IsInFrameworkUpdateThread)
        {
            return framework.RunOnFrameworkThread(() => ReadCurrentCore(cancellationToken));
        }

        return Task.FromResult(ReadCurrentCore(cancellationToken));
    }

    private DutyContextSnapshot? ReadCurrentCore(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!clientState.IsLoggedIn)
        {
            return null;
        }

        var territoryId = ConvertToNullableInt(clientState.TerritoryType);
        var inDuty = condition.Any(ConditionFlag.BoundByDuty, ConditionFlag.BoundByDuty56, ConditionFlag.BoundByDuty95);
        var dutyName = inDuty ? PluginReaderValueFormatter.FormatDutyName(territoryId) : null;
        var dutyType = inDuty ? "duty" : "world";
        var isDutyComplete = condition.Any(ConditionFlag.WatchingCutscene78, ConditionFlag.OccupiedInQuestEvent);
        var summaryText = PluginReaderValueFormatter.FormatDutySummary(
            inDuty,
            dutyName ?? "Unknown duty",
            territoryId,
            isDutyComplete);

        return new DutyContextSnapshot(
            CapturedAt: DateTimeOffset.UtcNow,
            TerritoryId: territoryId,
            DutyName: dutyName,
            DutyType: dutyType,
            InDuty: inDuty,
            IsDutyComplete: isDutyComplete,
            SummaryText: summaryText);
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
}
