using DalamudMCP.Application.Abstractions.Actions;
using DalamudMCP.Domain.Actions;

namespace DalamudMCP.Plugin.Readers;

public sealed class NullAetheryteTeleportController : IAetheryteTeleportController
{
    public Task<TeleportToAetheryteResult> TeleportAsync(string query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(
            new TeleportToAetheryteResult(
                query,
                Succeeded: false,
                Reason: "teleport_unavailable",
                AetheryteId: null,
                AetheryteName: null,
                TerritoryName: null,
                SummaryText: "Teleport is not available in the current plugin runtime."));
    }
}
