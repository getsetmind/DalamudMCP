using DalamudMCP.Domain.Actions;

namespace DalamudMCP.Application.Abstractions.Actions;

public interface IEntityMovementController
{
    public Task<MoveToEntityResult> MoveToEntityAsync(string gameObjectId, bool allowFlight, CancellationToken cancellationToken);
}
