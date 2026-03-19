using DalamudMCP.Domain.Session;

namespace DalamudMCP.Domain.Tests.Session;

public sealed class SessionStateTests
{
    [Fact]
    public void Constructor_NormalizesComponentsAndExposesCounts()
    {
        var state = new SessionState(
            new DateTimeOffset(2026, 3, 20, 3, 0, 0, TimeSpan.Zero),
            " DalamudMCP.123 ",
            true,
            [
                new SessionComponentState("player_context", false, "not_attached"),
                new SessionComponentState("player_context", true, "ready"),
                new SessionComponentState("addon_tree", false, "not_attached"),
            ],
            " Session is healthy. ");

        Assert.Equal("DalamudMCP.123", state.PipeName);
        Assert.Equal(1, state.ReadyComponentCount);
        Assert.Equal(2, state.TotalComponentCount);
        Assert.Equal("ready", state.Components.Single(component => component.ComponentName == "player_context").Status);
        Assert.Equal("Session is healthy.", state.SummaryText);
    }
}
