using DalamudMCP.Domain.Snapshots;

namespace DalamudMCP.Application.Abstractions.Readers;

public interface IAddonTreeReader
{
    public Task<AddonTreeSnapshot?> ReadCurrentAsync(string addonName, CancellationToken cancellationToken);
}
