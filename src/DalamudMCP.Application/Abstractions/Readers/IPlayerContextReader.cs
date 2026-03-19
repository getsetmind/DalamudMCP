using DalamudMCP.Domain.Snapshots;

namespace DalamudMCP.Application.Abstractions.Readers;

public interface IPlayerContextReader
{
    public Task<PlayerContextSnapshot?> ReadCurrentAsync(CancellationToken cancellationToken);
}
