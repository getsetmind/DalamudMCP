using DalamudMCP.Application.Abstractions.Readers;
using DalamudMCP.Domain.Snapshots;

namespace DalamudMCP.Application.Tests.Fakes;

internal sealed class FakeAddonTreeReader : IAddonTreeReader
{
    private readonly Dictionary<string, AddonTreeSnapshot?> snapshots = new(StringComparer.OrdinalIgnoreCase);

    public void Set(string addonName, AddonTreeSnapshot? snapshot)
    {
        snapshots[addonName] = snapshot;
    }

    public Task<AddonTreeSnapshot?> ReadCurrentAsync(string addonName, CancellationToken cancellationToken) =>
        Task.FromResult(snapshots.TryGetValue(addonName, out var snapshot) ? snapshot : null);
}
