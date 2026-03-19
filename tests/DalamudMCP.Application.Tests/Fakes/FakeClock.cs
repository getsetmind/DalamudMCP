using DalamudMCP.Application.Abstractions;

namespace DalamudMCP.Application.Tests.Fakes;

internal sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; }
}
