namespace DalamudMCP.Host.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void HostAssembly_Loads()
    {
        Assert.NotNull(typeof(DalamudMCP.Host.McpServerHost).Assembly);
    }
}
