using System.Globalization;
using System.Runtime.Versioning;
using Dalamud.Plugin.Services;
using DalamudMCP.Application.Abstractions.Actions;
using DalamudMCP.Domain.Actions;

namespace DalamudMCP.Plugin.Readers;

[SupportedOSPlatform("windows")]
public sealed class DalamudTargetObjectController : ITargetObjectController
{
    private readonly IFramework framework;
    private readonly IObjectTable objectTable;
    private readonly ITargetManager targetManager;

    public DalamudTargetObjectController(
        IFramework framework,
        IObjectTable objectTable,
        ITargetManager targetManager)
    {
        ArgumentNullException.ThrowIfNull(framework);
        ArgumentNullException.ThrowIfNull(objectTable);
        ArgumentNullException.ThrowIfNull(targetManager);
        this.framework = framework;
        this.objectTable = objectTable;
        this.targetManager = targetManager;
    }

    public Task<TargetObjectResult> TargetAsync(string gameObjectId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameObjectId);
        cancellationToken.ThrowIfCancellationRequested();
        if (!framework.IsInFrameworkUpdateThread)
        {
            return framework.RunOnFrameworkThread(() => TargetCore(gameObjectId, cancellationToken));
        }

        return Task.FromResult(TargetCore(gameObjectId, cancellationToken));
    }

    private TargetObjectResult TargetCore(string gameObjectId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryParseGameObjectId(gameObjectId, out var parsedId))
        {
            return new TargetObjectResult(
                gameObjectId,
                Succeeded: false,
                Reason: "invalid_object_id",
                TargetedGameObjectId: null,
                TargetName: null,
                ObjectKind: null,
                SummaryText: $"'{gameObjectId}' is not a valid game object id.");
        }

        var gameObject = objectTable.FirstOrDefault(candidate => candidate is not null && candidate.GameObjectId == parsedId);
        if (gameObject is null)
        {
            return new TargetObjectResult(
                gameObjectId,
                Succeeded: false,
                Reason: "object_not_found",
                TargetedGameObjectId: null,
                TargetName: null,
                ObjectKind: null,
                SummaryText: $"Object {gameObjectId} was not found in the current object table.");
        }

        if (!gameObject.IsTargetable)
        {
            return new TargetObjectResult(
                gameObjectId,
                Succeeded: false,
                Reason: "object_not_targetable",
                TargetedGameObjectId: $"0x{gameObject.GameObjectId:X}",
                TargetName: ReadName(gameObject),
                ObjectKind: gameObject.ObjectKind.ToString(),
                SummaryText: $"Object {ReadName(gameObject)} is not targetable.");
        }

        targetManager.Target = gameObject;
        var assignedTarget = targetManager.Target;
        var succeeded = assignedTarget is not null && assignedTarget.GameObjectId == gameObject.GameObjectId;

        return new TargetObjectResult(
            gameObjectId,
            Succeeded: succeeded,
            Reason: succeeded ? null : "target_assignment_failed",
            TargetedGameObjectId: $"0x{gameObject.GameObjectId:X}",
            TargetName: ReadName(gameObject),
            ObjectKind: gameObject.ObjectKind.ToString(),
            SummaryText: succeeded
                ? $"Targeted {ReadName(gameObject)}."
                : $"Failed to target {ReadName(gameObject)}.");
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

    private static string ReadName(Dalamud.Game.ClientState.Objects.Types.IGameObject gameObject) =>
        string.IsNullOrWhiteSpace(gameObject.Name.TextValue)
            ? $"Object#{gameObject.GameObjectId:X}"
            : gameObject.Name.TextValue;
}
