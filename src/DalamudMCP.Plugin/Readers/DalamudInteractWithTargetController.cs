using System.Globalization;
using System.Numerics;
using System.Runtime.Versioning;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using DalamudMCP.Application.Abstractions.Actions;
using DalamudMCP.Domain.Actions;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace DalamudMCP.Plugin.Readers;

[SupportedOSPlatform("windows")]
public sealed class DalamudInteractWithTargetController : IInteractWithTargetController
{
    private readonly IClientState clientState;
    private readonly IFramework framework;
    private readonly IObjectTable objectTable;
    private readonly ITargetManager targetManager;

    public DalamudInteractWithTargetController(
        IFramework framework,
        IClientState clientState,
        IObjectTable objectTable,
        ITargetManager targetManager)
    {
        ArgumentNullException.ThrowIfNull(framework);
        ArgumentNullException.ThrowIfNull(clientState);
        ArgumentNullException.ThrowIfNull(objectTable);
        ArgumentNullException.ThrowIfNull(targetManager);
        this.framework = framework;
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.targetManager = targetManager;
    }

    public Task<InteractWithTargetResult> InteractAsync(string? expectedGameObjectId, bool checkLineOfSight, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!framework.IsInFrameworkUpdateThread)
        {
            return framework.RunOnFrameworkThread(() => InteractCore(expectedGameObjectId, checkLineOfSight, cancellationToken));
        }

        return Task.FromResult(InteractCore(expectedGameObjectId, checkLineOfSight, cancellationToken));
    }

    private unsafe InteractWithTargetResult InteractCore(string? expectedGameObjectId, bool checkLineOfSight, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var target = targetManager.Target;
        if (target is null)
        {
            return new InteractWithTargetResult(
                expectedGameObjectId,
                Succeeded: false,
                Reason: "no_current_target",
                InteractedGameObjectId: null,
                TargetName: null,
                ObjectKind: null,
                Distance: null,
                SummaryText: "No current target is selected.");
        }

        if (!TryValidateExpectedTarget(expectedGameObjectId, target, out var validationFailure))
        {
            return validationFailure!;
        }

        if (!target.IsTargetable)
        {
            return new InteractWithTargetResult(
                expectedGameObjectId,
                Succeeded: false,
                Reason: "target_not_targetable",
                InteractedGameObjectId: $"0x{target.GameObjectId:X}",
                TargetName: ReadName(target),
                ObjectKind: target.ObjectKind.ToString(),
                Distance: CalculateDistance(target),
                SummaryText: $"Target {ReadName(target)} is not targetable.");
        }

        var targetSystem = TargetSystem.Instance();
        if (targetSystem is null)
        {
            return new InteractWithTargetResult(
                expectedGameObjectId,
                Succeeded: false,
                Reason: "target_system_unavailable",
                InteractedGameObjectId: $"0x{target.GameObjectId:X}",
                TargetName: ReadName(target),
                ObjectKind: target.ObjectKind.ToString(),
                Distance: CalculateDistance(target),
                SummaryText: "Target system is unavailable.");
        }

        var targetStruct = (GameObject*)target.Address;
        if (targetStruct is null)
        {
            return new InteractWithTargetResult(
                expectedGameObjectId,
                Succeeded: false,
                Reason: "target_struct_unavailable",
                InteractedGameObjectId: $"0x{target.GameObjectId:X}",
                TargetName: ReadName(target),
                ObjectKind: target.ObjectKind.ToString(),
                Distance: CalculateDistance(target),
                SummaryText: $"Target {ReadName(target)} does not expose a native object pointer.");
        }

        var interactionResult = targetSystem->InteractWithObject(targetStruct, checkLineOfSight);
        var succeeded = interactionResult != 0;

        return new InteractWithTargetResult(
            expectedGameObjectId,
            Succeeded: succeeded,
            Reason: succeeded ? null : "interaction_rejected",
            InteractedGameObjectId: $"0x{target.GameObjectId:X}",
            TargetName: ReadName(target),
            ObjectKind: target.ObjectKind.ToString(),
            Distance: CalculateDistance(target),
            SummaryText: succeeded
                ? $"Interaction started with {ReadName(target)}."
                : $"Interaction with {ReadName(target)} was rejected.");
    }

    private bool TryValidateExpectedTarget(
        string? expectedGameObjectId,
        IGameObject target,
        out InteractWithTargetResult? failure)
    {
        failure = null;
        if (string.IsNullOrWhiteSpace(expectedGameObjectId))
        {
            return true;
        }

        if (!TryParseGameObjectId(expectedGameObjectId, out var parsedId))
        {
            failure = new InteractWithTargetResult(
                expectedGameObjectId,
                Succeeded: false,
                Reason: "invalid_object_id",
                InteractedGameObjectId: $"0x{target.GameObjectId:X}",
                TargetName: ReadName(target),
                ObjectKind: target.ObjectKind.ToString(),
                Distance: CalculateDistance(target),
                SummaryText: $"'{expectedGameObjectId}' is not a valid game object id.");
            return false;
        }

        if (target.GameObjectId != parsedId)
        {
            failure = new InteractWithTargetResult(
                expectedGameObjectId,
                Succeeded: false,
                Reason: "target_mismatch",
                InteractedGameObjectId: $"0x{target.GameObjectId:X}",
                TargetName: ReadName(target),
                ObjectKind: target.ObjectKind.ToString(),
                Distance: CalculateDistance(target),
                SummaryText: $"Current target {ReadName(target)} does not match {expectedGameObjectId}.");
            return false;
        }

        return true;
    }

    private static bool TryParseGameObjectId(string value, out ulong gameObjectId)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return ulong.TryParse(trimmed[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out gameObjectId);
        }

        return ulong.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out gameObjectId);
    }

    private static string ReadName(IGameObject gameObject) =>
        string.IsNullOrWhiteSpace(gameObject.Name.TextValue)
            ? $"Object#{gameObject.GameObjectId:X}"
            : gameObject.Name.TextValue;

    private double? CalculateDistance(IGameObject gameObject)
    {
        var localPlayer = objectTable.LocalPlayer;
        if (localPlayer is null)
        {
            return null;
        }

        return Vector3.Distance(localPlayer.Position, gameObject.Position);
    }
}
