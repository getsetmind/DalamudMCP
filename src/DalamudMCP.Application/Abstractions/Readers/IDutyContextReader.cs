using DalamudMCP.Domain.Snapshots;

namespace DalamudMCP.Application.Abstractions.Readers;

public interface IDutyContextReader
{
    public Task<DutyContextSnapshot?> ReadCurrentAsync(CancellationToken cancellationToken);
}
