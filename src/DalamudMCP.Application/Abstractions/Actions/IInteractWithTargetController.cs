using DalamudMCP.Domain.Actions;

namespace DalamudMCP.Application.Abstractions.Actions;

public interface IInteractWithTargetController
{
    public Task<InteractWithTargetResult> InteractAsync(string? expectedGameObjectId, bool checkLineOfSight, CancellationToken cancellationToken);
}
