using DalamudMCP.Domain.Session;

namespace DalamudMCP.Application.Abstractions.Readers;

public interface ISessionStateReader
{
    public Task<SessionState> ReadCurrentAsync(CancellationToken cancellationToken);
}
