using DalamudMCP.Application.Abstractions;
using DalamudMCP.Application.Common;

namespace DalamudMCP.Application.Services;

public sealed class SnapshotFreshnessPolicy
{
    private readonly IClock clock;
    private readonly TimeSpan staleAfter;

    public SnapshotFreshnessPolicy(IClock clock, TimeSpan? staleAfter = null)
    {
        this.clock = clock;
        this.staleAfter = staleAfter ?? TimeSpan.FromSeconds(2);
    }

    public FreshnessEvaluation Evaluate(DateTimeOffset capturedAt)
    {
        var age = clock.UtcNow - capturedAt;
        var ageMs = (int)Math.Max(0, age.TotalMilliseconds);
        var state = age > staleAfter ? FreshnessState.Stale : FreshnessState.Fresh;
        return new FreshnessEvaluation(state, ageMs);
    }
}
