using System.Runtime.Versioning;
using Dalamud.Game.NativeWrapper;
using Dalamud.Plugin.Services;
using DalamudMCP.Protocol;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Manifold;
using MemoryPack;

namespace DalamudMCP.Plugin.Operations;

[Operation(
    "addon.input",
    Description = "Sends low-level input to an allowlisted addon.",
    Summary = "Sends addon input.")]
[ResultFormatter(typeof(AddonInputOperation.TextFormatter))]
[CliCommand("addon", "input")]
[McpTool("send_addon_input")]
public sealed partial class AddonInputOperation : IOperation<AddonInputOperation.Request, AddonInputResult>
{
    private readonly Func<Request, CancellationToken, ValueTask<AddonInputResult>> executor;

    [SupportedOSPlatform("windows")]
    public AddonInputOperation(
        IFramework framework,
        IClientState clientState,
        IGameGui gameGui)
    {
        ArgumentNullException.ThrowIfNull(framework);
        ArgumentNullException.ThrowIfNull(clientState);
        ArgumentNullException.ThrowIfNull(gameGui);

        executor = CreateDalamudExecutor(framework, clientState, gameGui);
    }

    internal AddonInputOperation(Func<Request, CancellationToken, ValueTask<AddonInputResult>> executor)
    {
        this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public ValueTask<AddonInputResult> ExecuteAsync(Request request, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        return executor(request, context.CancellationToken);
    }

    [MemoryPackable]
    [ProtocolOperation("addon.input")]
    [LegacyBridgeRequest("SendAddonInput")]
    public sealed partial class Request
    {
        [Option("addon", Description = "Addon name to target.")]
        public string AddonName { get; init; } = string.Empty;

        [Option("input-type", Description = "Input type such as 'gamepad' or 'dpad'.")]
        public string InputType { get; init; } = string.Empty;

        [Option("input-id", Description = "Input id to send.")]
        public int InputId { get; init; }

        [Option("auxiliary-state", Description = "Auxiliary state flag.", Required = false)]
        public bool? AuxiliaryState { get; init; }

        [Option("input-state", Description = "Optional input state such as 'down'.", Required = false)]
        public string? InputState { get; init; }
    }

    public sealed class TextFormatter : IResultFormatter<AddonInputResult>
    {
        public string? FormatText(AddonInputResult result, OperationContext context)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(context);

            return result.SummaryText;
        }
    }

    [SupportedOSPlatform("windows")]
    private static Func<Request, CancellationToken, ValueTask<AddonInputResult>> CreateDalamudExecutor(
        IFramework framework,
        IClientState clientState,
        IGameGui gameGui)
    {
        return async (request, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            string addonName = NormalizeAddonName(request.AddonName);
            string inputTypeName = NormalizeRequiredText(request.InputType, nameof(request.InputType));
            bool auxiliaryState = request.AuxiliaryState ?? false;

            if (framework.IsInFrameworkUpdateThread)
            {
                return SendInputCore(
                    gameGui,
                    addonName,
                    inputTypeName,
                    request.InputId,
                    request.InputState,
                    auxiliaryState,
                    cancellationToken);
            }

            return await framework.RunOnFrameworkThread(
                    () => SendInputCore(
                        gameGui,
                        addonName,
                        inputTypeName,
                        request.InputId,
                        request.InputState,
                        auxiliaryState,
                        cancellationToken))
                .ConfigureAwait(false);
        };
    }

    [SupportedOSPlatform("windows")]
    private static unsafe AddonInputResult SendInputCore(
        IGameGui gameGui,
        string addonName,
        string inputTypeName,
        int inputId,
        string? inputStateName,
        bool auxiliaryState,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryParseInputType(inputTypeName, out AddonInputType inputType))
        {
            return new AddonInputResult(
                addonName,
                inputTypeName,
                inputId,
                auxiliaryState,
                false,
                "invalid_input_type",
                $"'{inputTypeName}' is not a supported input type.");
        }

        if (!TryParseInputState(inputStateName, out AddonInputState? inputState))
        {
            return new AddonInputResult(
                addonName,
                inputTypeName,
                inputId,
                auxiliaryState,
                false,
                "invalid_input_state",
                $"'{inputStateName}' is not a supported input state.");
        }

        if (!TryGetReadyAddon(gameGui, addonName, out AtkUnitBase* addonStruct, out string reason, out string summary))
            return new AddonInputResult(addonName, inputTypeName, inputId, auxiliaryState, false, reason, summary);

        bool succeeded = inputType switch
        {
            AddonInputType.DPad => addonStruct->HandleDPadInput(inputId, auxiliaryState),
            AddonInputType.BackButton => addonStruct->HandleBackButtonInput(inputId, auxiliaryState),
            AddonInputType.Gamepad => HandleGamepadInput(addonStruct, inputId, inputState, auxiliaryState),
            _ => false
        };

        string? failureReason = succeeded ? null : "input_not_handled";
        string stateText = DescribeInputState(inputType, inputState, auxiliaryState);
        string summaryText = succeeded
            ? $"Sent {ToExternalName(inputType)} input {inputId} ({stateText}) to {addonName}."
            : $"{addonName} did not handle {ToExternalName(inputType)} input {inputId} ({stateText}).";

        return new AddonInputResult(addonName, ToExternalName(inputType), inputId, auxiliaryState, succeeded, failureReason, summaryText);
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

    [SupportedOSPlatform("windows")]
    private static unsafe bool HandleGamepadInput(
        AtkUnitBase* addonStruct,
        int inputId,
        AddonInputState? inputState,
        bool auxiliaryState)
    {
        AtkEventData.AtkInputData inputData = new()
        {
            InputId = inputId,
            State = ToNativeInputState(inputState, auxiliaryState)
        };

        return addonStruct->HandleCustomInput(&inputData);
    }

    private static string NormalizeAddonName(string addonName)
    {
        return string.IsNullOrWhiteSpace(addonName)
            ? throw new ArgumentException("addon is required.", nameof(addonName))
            : addonName.Trim();
    }

    private static string NormalizeRequiredText(string value, string parameterName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"{parameterName} is required.", parameterName)
            : value.Trim();
    }

    private static bool TryParseInputType(string value, out AddonInputType inputType)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "dpad":
                inputType = AddonInputType.DPad;
                return true;
            case "back":
            case "backbutton":
                inputType = AddonInputType.BackButton;
                return true;
            case "gamepad":
                inputType = AddonInputType.Gamepad;
                return true;
            default:
                inputType = default;
                return false;
        }
    }

    private static bool TryParseInputState(string? value, out AddonInputState? inputState)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            inputState = null;
            return true;
        }

        switch (value.Trim().ToLowerInvariant())
        {
            case "down":
                inputState = AddonInputState.Down;
                return true;
            case "up":
                inputState = AddonInputState.Up;
                return true;
            case "held":
                inputState = AddonInputState.Held;
                return true;
            case "repeat":
                inputState = AddonInputState.Repeat;
                return true;
            default:
                inputState = null;
                return false;
        }
    }

    private static string ToExternalName(AddonInputType inputType)
    {
        return inputType switch
        {
            AddonInputType.DPad => "dpad",
            AddonInputType.BackButton => "back",
            AddonInputType.Gamepad => "gamepad",
            _ => inputType.ToString().ToLowerInvariant()
        };
    }

    private static InputState ToNativeInputState(AddonInputState? inputState, bool auxiliaryState)
    {
        AddonInputState effectiveState = inputState ?? (auxiliaryState ? AddonInputState.Repeat : AddonInputState.Down);
        return effectiveState switch
        {
            AddonInputState.Down => InputState.Down,
            AddonInputState.Up => InputState.Up,
            AddonInputState.Held => InputState.Held,
            AddonInputState.Repeat => InputState.Repeat,
            _ => InputState.Down
        };
    }

    private static string DescribeInputState(AddonInputType inputType, AddonInputState? inputState, bool auxiliaryState)
    {
        if (inputType is not AddonInputType.Gamepad)
            return auxiliaryState ? "aux" : "default";

        AddonInputState effectiveState = inputState ?? (auxiliaryState ? AddonInputState.Repeat : AddonInputState.Down);
        return effectiveState.ToString().ToLowerInvariant();
    }
}

public enum AddonInputType
{
    DPad = 0,
    BackButton = 1,
    Gamepad = 2
}

public enum AddonInputState
{
    Down = 0,
    Up = 1,
    Held = 2,
    Repeat = 3
}

[MemoryPackable]
public sealed partial record AddonInputResult(
    string AddonName,
    string InputType,
    int InputId,
    bool AuxiliaryState,
    bool Succeeded,
    string? Reason,
    string SummaryText);
