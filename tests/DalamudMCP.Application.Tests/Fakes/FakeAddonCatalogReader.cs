using DalamudMCP.Application.Abstractions.Readers;
using DalamudMCP.Domain.Snapshots;

namespace DalamudMCP.Application.Tests.Fakes;

internal sealed class FakeAddonCatalogReader : IAddonCatalogReader
{
    public IReadOnlyList<AddonSummary> Snapshots { get; set; } = [];

    public Task<IReadOnlyList<AddonSummary>> ReadCurrentAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Snapshots);
}
