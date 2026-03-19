using DalamudMCP.Application.Abstractions;

namespace DalamudMCP.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
