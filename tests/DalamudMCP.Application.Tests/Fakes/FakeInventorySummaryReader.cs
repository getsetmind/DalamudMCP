using DalamudMCP.Application.Abstractions.Readers;
using DalamudMCP.Domain.Snapshots;

namespace DalamudMCP.Application.Tests.Fakes;

internal sealed class FakeInventorySummaryReader : IInventorySummaryReader
{
    public InventorySummarySnapshot? Snapshot { get; set; }

    public Task<InventorySummarySnapshot?> ReadCurrentAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Snapshot);
}
