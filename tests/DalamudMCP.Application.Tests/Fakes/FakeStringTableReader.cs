using DalamudMCP.Application.Abstractions.Readers;
using DalamudMCP.Domain.Snapshots;

namespace DalamudMCP.Application.Tests.Fakes;

internal sealed class FakeStringTableReader : IStringTableReader
{
    private readonly Dictionary<string, StringTableSnapshot?> snapshots = new(StringComparer.OrdinalIgnoreCase);

    public void Set(string addonName, StringTableSnapshot? snapshot)
    {
        snapshots[addonName] = snapshot;
    }

    public Task<StringTableSnapshot?> ReadCurrentAsync(string addonName, CancellationToken cancellationToken) =>
        Task.FromResult(snapshots.TryGetValue(addonName, out var snapshot) ? snapshot : null);
}
