using DalamudMCP.Application.Abstractions.Readers;
using DalamudMCP.Domain.Actions;

namespace DalamudMCP.Application.Tests.Fakes;

internal sealed class FakeNearbyInteractablesReader : INearbyInteractablesReader
{
    public NearbyInteractablesSnapshot? Snapshot { get; set; }

    public Task<NearbyInteractablesSnapshot?> ReadCurrentAsync(
        double maxDistance,
        string? nameContains,
        bool includePlayers,
        CancellationToken cancellationToken)
    {
        _ = maxDistance;
        _ = nameContains;
        _ = includePlayers;
        return Task.FromResult(Snapshot);
    }
}
