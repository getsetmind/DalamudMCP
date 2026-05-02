using System.Runtime.Versioning;
using Dalamud.Game.NativeWrapper;
using Dalamud.Plugin.Services;
using Manifold;
using DalamudMCP.Protocol;
using FFXIVClientStructs.FFXIV.Component.GUI;
using MemoryPack;

namespace DalamudMCP.Plugin.Operations;

[Operation(
    "addon.callback.values",
    Description = "Sends callback values to an allowlisted addon.",
    Summary = "Sends addon callback values.")]
[ResultFormatter(typeof(AddonCallbackValuesOperation.TextFormatter))]
[CliCommand("addon", "callback", "values")]
[McpTool("send_addon_callback_values")]
public sealed partial class AddonCallbackValuesOperation : IOperation<AddonCallbackValuesOperation.Request, AddonCallbackValuesResult>
{
    private readonly Func<Request, CancellationToken, ValueTask<AddonCallbackValuesResult>> executor;

    [SupportedOSPlatform("windows")]
    public AddonCallbackValuesOperation(
        IFramework framework,
        IClientState clientState,
        IGameGui gameGui)
    {
        ArgumentNullException.ThrowIfNull(framework);
        ArgumentNullException.ThrowIfNull(clientState);
        ArgumentNullException.ThrowIfNull(gameGui);

        executor = CreateDalamudExecutor(framework, clientState, gameGui);
    }

    internal AddonCallbackValuesOperation(Func<Request, CancellationToken, ValueTask<AddonCallbackValuesResult>> executor)
    {
        this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public ValueTask<AddonCallbackValuesResult> ExecuteAsync(Request request, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        return executor(request, context.CancellationToken);
    }

    [MemoryPackable]
    [ProtocolOperation("addon.callback.values")]
    [LegacyBridgeRequest("SendAddonCallbackValues")]
    public sealed partial class Request
    {
        [Option("addon", Description = "Addon name to target.")]
        public string AddonName { get; init; } = string.Empty;

        [Option("values", Description = "Comma-separated callback values such as '8,1'.")]
        public int[] Values { get; init; } = [];

        [Option("update-state", Description = "Pass true to update addon state before firing the callback.", Required = false)]
        public bool? UpdateState { get; init; }
    }

    public sealed class TextFormatter : IResultFormatter<AddonCallbackValuesResult>
    {
        public string? FormatText(AddonCallbackValuesResult result, OperationContext context)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(context);

            return result.SummaryText;
        }
    }

    [SupportedOSPlatform("windows")]
    private static Func<Request, CancellationToken, ValueTask<AddonCallbackValuesResult>> CreateDalamudExecutor(
        IFramework framework,
        IClientState clientState,
        IGameGui gameGui)
    {
        return async (request, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            string addonName = NormalizeAddonName(request.AddonName);
            int[] values = request.Values ?? [];
            bool updateState = request.UpdateState ?? false;

            if (framework.IsInFrameworkUpdateThread)
                return SendValuesCore(gameGui, addonName, values, updateState, cancellationToken);

            return await framework.RunOnFrameworkThread(() => SendValuesCore(gameGui, addonName, values, updateState, cancellationToken))
                .ConfigureAwait(false);
        };
    }

    [SupportedOSPlatform("windows")]
    private static unsafe AddonCallbackValuesResult SendValuesCore(
        IGameGui gameGui,
        string addonName,
        int[] values,
        bool updateState,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (values.Length == 0)
            return new AddonCallbackValuesResult(addonName, [], false, "no_values", "At least one callback value is required.");

        if (!TryGetReadyAddon(gameGui, addonName, out AtkUnitBase* addonStruct, out string reason, out string summary))
            return new AddonCallbackValuesResult(addonName, [.. values], false, reason, summary);

        int[] copiedValues = values.ToArray();
        AtkValue* atkValues = stackalloc AtkValue[copiedValues.Length];
        for (int index = 0; index < copiedValues.Length; index++)
        {
            atkValues[index] = new AtkValue
            {
                Type = FFXIVClientStructs.FFXIV.Component.GUI.AtkValueType.Int,
                Int = copiedValues[index]
            };
        }

        addonStruct->FireCallback((uint)copiedValues.Length, atkValues, updateState);
        return new AddonCallbackValuesResult(
            addonName,
            copiedValues,
            true,
            null,
            $"Sent callback values [{string.Join(", ", copiedValues)}] to {addonName} with updateState={updateState}.");
    }

    [SupportedOSPlatform("windows")]
    private static unsafe bool TryGetReadyAddon(
        IGameGui gameGui,
        string addonName,
        out AtkUnitBase* addonStruct,
        out string reason,
        out string summary)
    {
        addonStruct = null;
        AtkUnitBasePtr addon = gameGui.GetAddonByName(addonName, 1);
        if (addon.IsNull || !addon.IsReady)
        {
            reason = "addon_not_ready";
            summary = $"{addonName} is not ready.";
            return false;
        }

        addonStruct = gameGui.GetAddonByName<AtkUnitBase>(addonName, 1);
        if (addonStruct is null)
        {
            reason = "addon_struct_unavailable";
            summary = $"{addonName} does not expose a native addon pointer.";
            return false;
        }

        reason = string.Empty;
        summary = string.Empty;
        return true;
    }

    private static string NormalizeAddonName(string addonName)
    {
        return string.IsNullOrWhiteSpace(addonName)
            ? throw new ArgumentException("addon is required.", nameof(addonName))
            : addonName.Trim();
    }
}

[MemoryPackable]
public sealed partial record AddonCallbackValuesResult(
    string AddonName,
    int[] Values,
    bool Succeeded,
    string? Reason,
    string SummaryText);
