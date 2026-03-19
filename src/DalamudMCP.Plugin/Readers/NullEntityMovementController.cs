using DalamudMCP.Application.Abstractions.Actions;
using DalamudMCP.Domain.Actions;

namespace DalamudMCP.Plugin.Readers;

public sealed class NullEntityMovementController : IEntityMovementController
{
    public Task<MoveToEntityResult> MoveToEntityAsync(string gameObjectId, bool allowFlight, CancellationToken cancellationToken)
    {
        _ = allowFlight;
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(
            new MoveToEntityResult(
                gameObjectId,
                Succeeded: false,
                Reason: "movement_unavailable",
                ResolvedGameObjectId: null,
                TargetName: null,
                ObjectKind: null,
                Destination: null,
                SummaryText: "World movement is not available in the current plugin runtime."));
    }
}
