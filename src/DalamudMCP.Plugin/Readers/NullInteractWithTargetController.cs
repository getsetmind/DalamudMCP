using DalamudMCP.Application.Abstractions.Actions;
using DalamudMCP.Domain.Actions;

namespace DalamudMCP.Plugin.Readers;

public sealed class NullInteractWithTargetController : IInteractWithTargetController
{
    public Task<InteractWithTargetResult> InteractAsync(string? expectedGameObjectId, bool checkLineOfSight, CancellationToken cancellationToken)
    {
        _ = checkLineOfSight;
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(
            new InteractWithTargetResult(
                expectedGameObjectId,
                Succeeded: false,
                Reason: "interaction_unavailable",
                InteractedGameObjectId: null,
                TargetName: null,
                ObjectKind: null,
                Distance: null,
                SummaryText: "World interaction is not available in the current plugin runtime."));
    }
}
