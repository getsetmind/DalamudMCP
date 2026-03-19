using DalamudMCP.Domain.Snapshots;

namespace DalamudMCP.Application.Abstractions.Readers;

public interface IStringTableReader
{
    public Task<StringTableSnapshot?> ReadCurrentAsync(string addonName, CancellationToken cancellationToken);
}
