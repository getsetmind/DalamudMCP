using DalamudMCP.Domain.Actions;

namespace DalamudMCP.Application.Abstractions.Readers;

public interface INearbyInteractablesReader
{
    public Task<NearbyInteractablesSnapshot?> ReadCurrentAsync(
        double maxDistance,
        string? nameContains,
        bool includePlayers,
        CancellationToken cancellationToken);
}
