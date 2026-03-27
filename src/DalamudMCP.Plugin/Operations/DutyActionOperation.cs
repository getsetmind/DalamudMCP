using System.Runtime.Versioning;
using Dalamud.Plugin.Services;
using DalamudMCP.Framework;
using DalamudMCP.Protocol;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using MemoryPack;

namespace DalamudMCP.Plugin.Operations;

[Operation(
    "duty.action",
    Description = "Invokes a duty action slot.",
    Summary = "Uses a duty action.")]
[ResultFormatter(typeof(DutyActionOperation.TextFormatter))]
[CliCommand("duty", "action")]
[McpTool("use_duty_action")]
public sealed partial class DutyActionOperation : IOperation<DutyActionOperation.Request, DutyActionResult>
{
    private readonly Func<Request, CancellationToken, ValueTask<DutyActionResult>> executor;

    [SupportedOSPlatform("windows")]
    public DutyActionOperation(
        IFramework framework,
        IClientState clientState)
    {
        ArgumentNullException.ThrowIfNull(framework);
        ArgumentNullException.ThrowIfNull(clientState);

        executor = CreateDalamudExecutor(framework, clientState);
    }

    internal DutyActionOperation(Func<Request, CancellationToken, ValueTask<DutyActionResult>> executor)
    {
        this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public ValueTask<DutyActionResult> ExecuteAsync(Request request, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        return executor(request, context.CancellationToken);
    }

    [MemoryPackable]
    [ProtocolOperation("duty.action")]
    [LegacyBridgeRequest("UseDutyAction")]
    public sealed partial class Request
    {
        [Option("slot", Description = "Duty action slot number to invoke.")]
        public int Slot { get; init; }
    }

    public sealed class TextFormatter : IResultFormatter<DutyActionResult>
    {
        public string? FormatText(DutyActionResult result, OperationContext context)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(context);

            return result.SummaryText;
        }
    }

    [SupportedOSPlatform("windows")]
    private static Func<Request, CancellationToken, ValueTask<DutyActionResult>> CreateDalamudExecutor(
        IFramework framework,
        IClientState clientState)
    {
        return async (request, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request.Slot < 1)
            {
                return new DutyActionResult(
                    request.Slot,
                    false,
                    "invalid_slot",
                    null,
                    "Duty action slot must be greater than or equal to 1.");
            }

            if (framework.IsInFrameworkUpdateThread)
                return UseDutyActionCore(clientState, request.Slot, cancellationToken);

            return await framework.RunOnFrameworkThread(() => UseDutyActionCore(clientState, request.Slot, cancellationToken))
                .ConfigureAwait(false);
        };
    }

    [SupportedOSPlatform("windows")]
    private static unsafe DutyActionResult UseDutyActionCore(
        IClientState clientState,
        int slot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!clientState.IsLoggedIn)
        {
            return new DutyActionResult(
                slot,
                false,
                "duty_action_not_ready",
                null,
                "Duty actions are not ready because the player is not logged in.");
        }

        UIModule* uiModule = UIModule.Instance();
        if (uiModule is null)
            return new DutyActionResult(slot, false, "ui_module_unavailable", null, "UI module is unavailable.");

        RaptureHotbarModule* hotbarModule = uiModule->GetRaptureHotbarModule();
        if (hotbarModule is null)
        {
            return new DutyActionResult(
                slot,
                false,
                "hotbar_module_unavailable",
                null,
                "Rapture hotbar module is unavailable.");
        }

        if (!hotbarModule->DutyActionsPresent)
        {
            return new DutyActionResult(
                slot,
                false,
                "duty_actions_not_present",
                null,
                "Duty actions are not available in the current content.");
        }

        bool succeeded = hotbarModule->ExecuteDutyActionSlot((uint)(slot - 1));
        return new DutyActionResult(
            slot,
            succeeded,
            succeeded ? null : "execute_failed",
            null,
            succeeded
                ? $"Executed duty action slot {slot}."
                : $"Failed to execute duty action slot {slot}.");
    }
}

[MemoryPackable]
public sealed partial record DutyActionResult(
    int RequestedSlot,
    bool Succeeded,
    string? Reason,
    uint? ActionId,
    string SummaryText);
