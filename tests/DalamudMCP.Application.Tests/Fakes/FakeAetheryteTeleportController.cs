using DalamudMCP.Application.Abstractions.Actions;
using DalamudMCP.Domain.Actions;

namespace DalamudMCP.Application.Tests.Fakes;

public sealed class FakeAetheryteTeleportController : IAetheryteTeleportController
{
    public string? LastQuery { get; private set; }

    public TeleportToAetheryteResult Result { get; set; } =
        new(
            "gold saucer",
            Succeeded: false,
            Reason: "teleport_not_configured",
            AetheryteId: null,
            AetheryteName: null,
            TerritoryName: null,
            SummaryText: "Teleport was not configured.");

    public Task<TeleportToAetheryteResult> TeleportAsync(string query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastQuery = query;
        return Task.FromResult(Result);
    }
}
