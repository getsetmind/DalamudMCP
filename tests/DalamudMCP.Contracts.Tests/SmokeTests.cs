namespace DalamudMCP.Contracts.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void ContractsAssembly_Loads()
    {
        Assert.NotNull(typeof(DalamudMCP.Contracts.AssemblyMarker).Assembly);
    }
}
