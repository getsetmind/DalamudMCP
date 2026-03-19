using DalamudMCP.Application.Abstractions.Actions;
using DalamudMCP.Domain.Actions;

namespace DalamudMCP.Plugin.Readers;

public sealed class NullTargetObjectController : ITargetObjectController
{
    public Task<TargetObjectResult> TargetAsync(string gameObjectId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(
            new TargetObjectResult(
                gameObjectId,
                Succeeded: false,
                Reason: "targeting_not_attached",
                TargetedGameObjectId: null,
                TargetName: null,
                ObjectKind: null,
                SummaryText: "Targeting controller is not attached."));
    }
}
