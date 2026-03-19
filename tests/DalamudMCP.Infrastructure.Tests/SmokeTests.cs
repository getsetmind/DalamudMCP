namespace DalamudMCP.Infrastructure.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void InfrastructureAssembly_Loads()
    {
        Assert.NotNull(typeof(DalamudMCP.Infrastructure.AssemblyMarker).Assembly);
    }
}
