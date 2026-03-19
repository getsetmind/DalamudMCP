using DalamudMCP.Infrastructure.Time;

namespace DalamudMCP.Infrastructure.Tests.Time;

public sealed class SystemClockTests
{
    [Fact]
    public void UtcNow_ReturnsUtcTimestamp()
    {
        var clock = new SystemClock();

        var value = clock.UtcNow;

        Assert.Equal(TimeSpan.Zero, value.Offset);
    }
}
