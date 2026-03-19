namespace DalamudMCP.Application.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void ApplicationAssembly_Loads()
    {
        Assert.NotNull(typeof(DalamudMCP.Application.AssemblyMarker).Assembly);
    }
}
