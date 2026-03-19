using DalamudMCP.Domain.Actions;

namespace DalamudMCP.Application.Abstractions.Actions;

public interface IAetheryteTeleportController
{
    public Task<TeleportToAetheryteResult> TeleportAsync(string query, CancellationToken cancellationToken);
}
