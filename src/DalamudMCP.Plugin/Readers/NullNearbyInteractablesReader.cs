using DalamudMCP.Application.Abstractions.Readers;
using DalamudMCP.Domain.Actions;

namespace DalamudMCP.Plugin.Readers;

public sealed class NullNearbyInteractablesReader : INearbyInteractablesReader
{
    public Task<NearbyInteractablesSnapshot?> ReadCurrentAsync(
        double maxDistance,
        string? nameContains,
        bool includePlayers,
        CancellationToken cancellationToken)
    {
        _ = maxDistance;
        _ = nameContains;
        _ = includePlayers;
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<NearbyInteractablesSnapshot?>(null);
    }
}
