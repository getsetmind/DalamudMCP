using System.Runtime.Versioning;
using Dalamud.Game.NativeWrapper;
using Dalamud.Plugin.Services;
using Manifold;
using DalamudMCP.Plugin.Readers;
using DalamudMCP.Protocol;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.Interop;
using MemoryPack;

namespace DalamudMCP.Plugin.Operations;

[Operation(
    "addon.list",
    Description = "Gets the loaded addon list.",
    Summary = "Gets the loaded addon list.")]
[ResultFormatter(typeof(AddonListOperation.TextFormatter))]
[CliCommand("addon", "list")]
[McpTool("get_addon_list")]
public sealed partial class AddonListOperation
    : IOperation<AddonListOperation.Request, AddonSummary[]>, IPluginReaderStatus
{
    private readonly Func<CancellationToken, ValueTask<AddonSummary[]>> executor;
    private readonly Func<bool>? isReadyProvider;
    private readonly Func<string>? detailProvider;
    private readonly string unavailableDetail;

    [SupportedOSPlatform("windows")]
    public AddonListOperation(
        IFramework framework,
        IClientState clientState,
        IGameGui gameGui)
    {
        ArgumentNullException.ThrowIfNull(framework);
        ArgumentNullException.ThrowIfNull(clientState);
        ArgumentNullException.ThrowIfNull(gameGui);

        executor = CreateDalamudExecutor(framework, clientState, gameGui);
        isReadyProvider = () => clientState.IsLoggedIn;
        detailProvider = () => clientState.IsLoggedIn ? "ready" : "not_logged_in";
        unavailableDetail = "not_logged_in";
    }

    internal AddonListOperation(
        Func<CancellationToken, ValueTask<AddonSummary[]>> executor,
        bool isReady = true,
        string detail = "ready")
    {
        this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
        isReadyProvider = () => isReady;
        detailProvider = () => string.IsNullOrWhiteSpace(detail) ? "ready" : detail;
        unavailableDetail = string.IsNullOrWhiteSpace(detail) ? "ready" : detail;
    }

    public string ReaderKey => "addon.list";

    public bool IsReady => isReadyProvider?.Invoke() ?? false;

    public string Detail => detailProvider?.Invoke() ?? unavailableDetail;

    public ValueTask<AddonSummary[]> ExecuteAsync(Request request, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        return executor(context.CancellationToken);
    }

    [MemoryPackable]
    [ProtocolOperation("addon.list")]
    [LegacyBridgeRequest("GetAddonList")]
    public sealed partial record Request;

    public sealed class TextFormatter : IResultFormatter<AddonSummary[]>
    {
        public string? FormatText(AddonSummary[] result, OperationContext context)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(context);

            return $"addonCount={result.Length}";
        }
    }

    [SupportedOSPlatform("windows")]
    private static Func<CancellationToken, ValueTask<AddonSummary[]>> CreateDalamudExecutor(
        IFramework framework,
        IClientState clientState,
        IGameGui gameGui)
    {
        return async cancellationToken =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (framework.IsInFrameworkUpdateThread)
                return ReadCurrentCore(clientState, gameGui, cancellationToken);

            return await framework.RunOnFrameworkThread(() => ReadCurrentCore(clientState, gameGui, cancellationToken))
                .ConfigureAwait(false);
        };
    }

    [SupportedOSPlatform("windows")]
    private static unsafe AddonSummary[] ReadCurrentCore(
        IClientState clientState,
        IGameGui gameGui,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!clientState.IsLoggedIn)
            throw new InvalidOperationException("Addon list is not available because the local player is not logged in.");

        DateTimeOffset capturedAt = DateTimeOffset.UtcNow;
        Dictionary<string, AddonSummary> summaries = new(StringComparer.OrdinalIgnoreCase);

        RaptureAtkUnitManager* unitManager = RaptureAtkUnitManager.Instance();
        if (unitManager is null)
            return [];

        Span<Pointer<AtkUnitBase>> entries = unitManager->AllLoadedUnitsList.Entries;
        int count = Math.Min(entries.Length, unitManager->AllLoadedUnitsList.Count);
        for (int index = 0; index < count; index++)
        {
            AtkUnitBase* addonStruct = entries[index];
            if (addonStruct is null)
                continue;

            string? addonName = addonStruct->NameString;
            if (string.IsNullOrWhiteSpace(addonName) || summaries.ContainsKey(addonName))
                continue;

            AtkUnitBasePtr addon = gameGui.GetAddonByName(addonName, 1);
            bool isReady = !addon.IsNull && addon.IsReady;
            bool isVisible = !addon.IsNull && addon.IsVisible;
            summaries[addonName] = new AddonSummary(
                addonName,
                isReady,
                isVisible,
                capturedAt,
                CreateSummaryText(addonName, isReady, isVisible));
        }

        return summaries.Values
            .OrderBy(static summary => summary.AddonName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string CreateSummaryText(string displayName, bool isReady, bool isVisible)
    {
        string visibility = isVisible ? "visible" : "hidden";
        return isReady
            ? $"{displayName} is open and {visibility}."
            : $"{displayName} is not currently open.";
    }
}

[MemoryPackable]
public sealed partial record AddonSummary(
    string AddonName,
    bool IsReady,
    bool IsVisible,
    DateTimeOffset CapturedAt,
    string SummaryText);



