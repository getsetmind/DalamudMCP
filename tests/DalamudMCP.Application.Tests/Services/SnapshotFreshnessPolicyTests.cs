using DalamudMCP.Application.Common;
using DalamudMCP.Application.Services;
using DalamudMCP.Application.Tests.Fakes;

namespace DalamudMCP.Application.Tests.Services;

public sealed class SnapshotFreshnessPolicyTests
{
    [Fact]
    public void Evaluate_ReturnsFresh_WhenSnapshotIsRecent()
    {
        var clock = new FakeClock { UtcNow = new DateTimeOffset(2026, 3, 20, 0, 0, 1, TimeSpan.Zero) };
        var policy = new SnapshotFreshnessPolicy(clock, TimeSpan.FromSeconds(2));

        var evaluation = policy.Evaluate(new DateTimeOffset(2026, 3, 20, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal(FreshnessState.Fresh, evaluation.State);
        Assert.Equal(1000, evaluation.AgeMs);
    }
}
