using System.Globalization;
using System.Numerics;
using System.Runtime.Versioning;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Manifold;
using DalamudMCP.Protocol;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using MemoryPack;

namespace DalamudMCP.Plugin.Operations;

[Operation(
    "interact.with.target",
    Description = "Interacts with the current target with optional validation.",
    Summary = "Interacts with the current target.")]
[ResultFormatter(typeof(InteractWithTargetOperation.TextFormatter))]
[CliCommand("interact", "with", "target")]
[McpTool("interact_with_target")]
public sealed partial class InteractWithTargetOperation : IOperation<InteractWithTargetOperation.Request, InteractWithTargetResult>
{
    private readonly Func<Request, CancellationToken, ValueTask<InteractWithTargetResult>> executor;

    [SupportedOSPlatform("windows")]
    public InteractWithTargetOperation(
        IFramework framework,
        IClientState clientState,
        IObjectTable objectTable,
        ITargetManager targetManager)
    {
        ArgumentNullException.ThrowIfNull(framework);
        ArgumentNullException.ThrowIfNull(clientState);
        ArgumentNullException.ThrowIfNull(objectTable);
        ArgumentNullException.ThrowIfNull(targetManager);

        executor = CreateDalamudExecutor(framework, clientState, objectTable, targetManager);
    }

    internal InteractWithTargetOperation(Func<Request, CancellationToken, ValueTask<InteractWithTargetResult>> executor)
    {
        this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public ValueTask<InteractWithTargetResult> ExecuteAsync(Request request, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        return executor(request, context.CancellationToken);
    }

    [MemoryPackable]
    [ProtocolOperation("interact.with.target")]
    [LegacyBridgeRequest("InteractWithTarget")]
    public sealed partial class Request
    {
        [Option("expected-game-object-id", Description = "Expected target game object id.", Required = false)]
        public string? ExpectedGameObjectId { get; init; }

        [Option("check-line-of-sight", Description = "Require line-of-sight validation before interacting.", Required = false)]
        public bool? CheckLineOfSight { get; init; }
    }

    public sealed class TextFormatter : IResultFormatter<InteractWithTargetResult>
    {
        public string? FormatText(InteractWithTargetResult result, OperationContext context)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(context);

            return result.SummaryText;
        }
    }

    [SupportedOSPlatform("windows")]
    private static Func<Request, CancellationToken, ValueTask<InteractWithTargetResult>> CreateDalamudExecutor(
        IFramework framework,
        IClientState clientState,
        IObjectTable objectTable,
        ITargetManager targetManager)
    {
        return async (request, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (framework.IsInFrameworkUpdateThread)
                return InteractCore(clientState, objectTable, targetManager, request.ExpectedGameObjectId, request.CheckLineOfSight ?? false, cancellationToken);

            return await framework.RunOnFrameworkThread(
                    () => InteractCore(clientState, objectTable, targetManager, request.ExpectedGameObjectId, request.CheckLineOfSight ?? false, cancellationToken))
                .ConfigureAwait(false);
        };
    }

    [SupportedOSPlatform("windows")]
    private static unsafe InteractWithTargetResult InteractCore(
        IClientState clientState,
        IObjectTable objectTable,
        ITargetManager targetManager,
        string? expectedGameObjectId,
        bool checkLineOfSight,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!clientState.IsLoggedIn)
        {
            return new InteractWithTargetResult(
                expectedGameObjectId,
                false,
                "not_logged_in",
                null,
                null,
                null,
                null,
                "Player is not logged in.");
        }

        IGameObject? target = targetManager.Target;
        if (target is null)
        {
            return new InteractWithTargetResult(
                expectedGameObjectId,
                false,
                "no_current_target",
                null,
                null,
                null,
                null,
                "No current target is selected.");
        }

        if (!TryValidateExpectedTarget(expectedGameObjectId, target, objectTable, out InteractWithTargetResult? validationFailure))
            return validationFailure!;

        if (!target.IsTargetable)
        {
            return new InteractWithTargetResult(
                expectedGameObjectId,
                false,
                "target_not_targetable",
                $"0x{target.GameObjectId:X}",
                ReadName(target),
                target.ObjectKind.ToString(),
                CalculateDistance(objectTable, target),
                $"Target {ReadName(target)} is not targetable.");
        }

        TargetSystem* targetSystem = TargetSystem.Instance();
        if (targetSystem is null)
        {
            return new InteractWithTargetResult(
                expectedGameObjectId,
                false,
                "target_system_unavailable",
                $"0x{target.GameObjectId:X}",
                ReadName(target),
                target.ObjectKind.ToString(),
                CalculateDistance(objectTable, target),
                "Target system is unavailable.");
        }

        GameObject* targetStruct = (GameObject*)target.Address;
        if (targetStruct is null)
        {
            return new InteractWithTargetResult(
                expectedGameObjectId,
                false,
                "target_struct_unavailable",
                $"0x{target.GameObjectId:X}",
                ReadName(target),
                target.ObjectKind.ToString(),
                CalculateDistance(objectTable, target),
                $"Target {ReadName(target)} does not expose a native object pointer.");
        }

        ulong interactionResult = targetSystem->InteractWithObject(targetStruct, checkLineOfSight);
        bool succeeded = interactionResult != 0;
        return new InteractWithTargetResult(
            expectedGameObjectId,
            succeeded,
            succeeded ? null : "interaction_rejected",
            $"0x{target.GameObjectId:X}",
            ReadName(target),
            target.ObjectKind.ToString(),
            CalculateDistance(objectTable, target),
            succeeded
                ? $"Interaction started with {ReadName(target)}."
                : $"Interaction with {ReadName(target)} was rejected.");
    }

    private static bool TryValidateExpectedTarget(
        string? expectedGameObjectId,
        IGameObject target,
        IObjectTable objectTable,
        out InteractWithTargetResult? failure)
    {
        failure = null;
        if (string.IsNullOrWhiteSpace(expectedGameObjectId))
            return true;

        if (!TryParseGameObjectId(expectedGameObjectId, out ulong parsedId))
        {
            failure = new InteractWithTargetResult(
                expectedGameObjectId,
                false,
                "invalid_object_id",
                $"0x{target.GameObjectId:X}",
                ReadName(target),
                target.ObjectKind.ToString(),
                CalculateDistance(objectTable, target),
                $"'{expectedGameObjectId}' is not a valid game object id.");
            return false;
        }

        if (target.GameObjectId != parsedId)
        {
            failure = new InteractWithTargetResult(
                expectedGameObjectId,
                false,
                "target_mismatch",
                $"0x{target.GameObjectId:X}",
                ReadName(target),
                target.ObjectKind.ToString(),
                CalculateDistance(objectTable, target),
                $"Current target {ReadName(target)} does not match {expectedGameObjectId}.");
            return false;
        }

        return true;
    }

    private static bool TryParseGameObjectId(string value, out ulong gameObjectId)
    {
        string trimmed = value.Trim();
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return ulong.TryParse(trimmed[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out gameObjectId);

        return ulong.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out gameObjectId);
    }

    private static string ReadName(IGameObject gameObject)
    {
        return string.IsNullOrWhiteSpace(gameObject.Name.TextValue)
            ? $"Object#{gameObject.GameObjectId:X}"
            : gameObject.Name.TextValue;
    }

    private static double? CalculateDistance(IObjectTable objectTable, IGameObject gameObject)
    {
        IPlayerCharacter? localPlayer = objectTable.LocalPlayer;
        return localPlayer is null ? null : Vector3.Distance(localPlayer.Position, gameObject.Position);
    }
}

[MemoryPackable]
public sealed partial record InteractWithTargetResult(
    string? ExpectedGameObjectId,
    bool Succeeded,
    string? Reason,
    string? InteractedGameObjectId,
    string? TargetName,
    string? ObjectKind,
    double? Distance,
    string SummaryText);



