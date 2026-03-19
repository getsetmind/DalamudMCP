using System.Runtime.Versioning;
using Dalamud.Plugin.Services;
using DalamudMCP.Application.Abstractions.Readers;
using DalamudMCP.Domain.Actions;
using DalamudMCP.Domain.Snapshots;

namespace DalamudMCP.Plugin.Readers;

[SupportedOSPlatform("windows")]
public sealed class DalamudNearbyInteractablesReader : INearbyInteractablesReader
{
    private readonly IFramework framework;
    private readonly IObjectTable objectTable;

    public DalamudNearbyInteractablesReader(
        IFramework framework,
        IObjectTable objectTable)
    {
        ArgumentNullException.ThrowIfNull(framework);
        ArgumentNullException.ThrowIfNull(objectTable);
        this.framework = framework;
        this.objectTable = objectTable;
    }

    public Task<NearbyInteractablesSnapshot?> ReadCurrentAsync(
        double maxDistance,
        string? nameContains,
        bool includePlayers,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!framework.IsInFrameworkUpdateThread)
        {
            return framework.RunOnFrameworkThread(() => ReadCurrentCore(maxDistance, nameContains, includePlayers, cancellationToken));
        }

        return Task.FromResult(ReadCurrentCore(maxDistance, nameContains, includePlayers, cancellationToken));
    }

    private NearbyInteractablesSnapshot? ReadCurrentCore(
        double maxDistance,
        string? nameContains,
        bool includePlayers,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (objectTable.LocalPlayer is null)
        {
            return null;
        }

        var localPlayer = objectTable.LocalPlayer;
        var interactables = objectTable
            .Where(static gameObject => gameObject is not null)
            .Where(gameObject => gameObject!.GameObjectId != localPlayer.GameObjectId)
            .Select(gameObject => gameObject!)
            .Where(gameObject => gameObject.IsTargetable)
            .Where(gameObject => includePlayers || !string.Equals(gameObject.ObjectKind.ToString(), "Player", StringComparison.OrdinalIgnoreCase))
            .Where(gameObject => MatchesName(gameObject.Name.TextValue, nameContains))
            .Select(gameObject => CreateInteractable(localPlayer.Position, gameObject))
            .Where(interactable => interactable.Distance <= maxDistance)
            .OrderBy(static interactable => interactable.Distance)
            .Take(32)
            .ToArray();

        return new NearbyInteractablesSnapshot(
            DateTimeOffset.UtcNow,
            maxDistance,
            interactables,
            $"{interactables.Length} interactable objects within {maxDistance:0.#} yalms.");
    }

    private static bool MatchesName(string? objectName, string? nameContains)
    {
        if (string.IsNullOrWhiteSpace(nameContains))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(objectName)
            && objectName.Contains(nameContains, StringComparison.OrdinalIgnoreCase);
    }

    private static NearbyInteractable CreateInteractable(System.Numerics.Vector3 localPosition, Dalamud.Game.ClientState.Objects.Types.IGameObject gameObject)
    {
        var position = gameObject.Position;
        var distance = Math.Round(System.Numerics.Vector3.Distance(localPosition, position), 1);
        var objectName = string.IsNullOrWhiteSpace(gameObject.Name.TextValue)
            ? $"Object#{gameObject.GameObjectId:X}"
            : gameObject.Name.TextValue;
        return new NearbyInteractable(
            $"0x{gameObject.GameObjectId:X}",
            objectName,
            gameObject.ObjectKind.ToString(),
            gameObject.IsTargetable,
            distance,
            Math.Round(gameObject.HitboxRadius, 1),
            new PositionSnapshot(
                Math.Round(position.X, 1),
                Math.Round(position.Y, 1),
                Math.Round(position.Z, 1),
                "coarse"));
    }
}
