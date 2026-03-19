using DalamudMCP.Domain.Snapshots;

namespace DalamudMCP.Application.Abstractions.Readers;

public interface IAddonCatalogReader
{
    public Task<IReadOnlyList<AddonSummary>> ReadCurrentAsync(CancellationToken cancellationToken);
}
