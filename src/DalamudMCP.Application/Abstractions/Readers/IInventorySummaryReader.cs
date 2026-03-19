using DalamudMCP.Domain.Snapshots;

namespace DalamudMCP.Application.Abstractions.Readers;

public interface IInventorySummaryReader
{
    public Task<InventorySummarySnapshot?> ReadCurrentAsync(CancellationToken cancellationToken);
}
