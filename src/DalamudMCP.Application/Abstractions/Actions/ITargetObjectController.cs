using DalamudMCP.Domain.Actions;

namespace DalamudMCP.Application.Abstractions.Actions;

public interface ITargetObjectController
{
    public Task<TargetObjectResult> TargetAsync(string gameObjectId, CancellationToken cancellationToken);
}
