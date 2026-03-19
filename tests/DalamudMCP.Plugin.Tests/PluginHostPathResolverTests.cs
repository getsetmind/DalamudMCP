using DalamudMCP.Plugin.Hosting;

namespace DalamudMCP.Plugin.Tests;

public sealed class PluginHostPathResolverTests
{
    [Fact]
    public void TryResolveHttpServer_UsesRepoLocalDotNetAndMatchingHostOutput()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, ".dotnet"));
        File.WriteAllText(Path.Combine(root, ".dotnet", "dotnet.exe"), string.Empty);

        var pluginDirectory = Path.Combine(root, "src", "DalamudMCP.Plugin", "bin", "Debug", "net10.0");
        var hostDirectory = Path.Combine(root, "src", "DalamudMCP.Host", "bin", "Debug", "net10.0");
        Directory.CreateDirectory(pluginDirectory);
        Directory.CreateDirectory(hostDirectory);

        var pluginAssemblyPath = Path.Combine(pluginDirectory, "DalamudMCP.dll");
        var hostDllPath = Path.Combine(hostDirectory, "DalamudMCP.Host.dll");
        File.WriteAllText(pluginAssemblyPath, string.Empty);
        File.WriteAllText(hostDllPath, string.Empty);

        var resolver = new PluginHostPathResolver(pluginAssemblyPath, "DalamudMCP.123");
        var resolution = resolver.TryResolveHttpServer(39123);

        Assert.NotNull(resolution);
        Assert.Equal(Path.Combine(root, ".dotnet", "dotnet.exe"), resolution!.DotNetExecutable);
        Assert.Equal(hostDllPath, resolution.HostDllPath);
        Assert.Equal("DalamudMCP.123", resolution.PipeName);
        Assert.Equal("http://127.0.0.1:39123/mcp", resolution.EndpointUrl);
        Assert.Contains("--transport", resolution.CommandText, StringComparison.Ordinal);
    }

    [Fact]
    public void TryResolveConsole_FallsBackToPathDotNetWhenRepoLocalSdkIsMissing()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var pluginDirectory = Path.Combine(root, "src", "DalamudMCP.Plugin", "bin", "Release", "net10.0");
        var hostDirectory = Path.Combine(root, "src", "DalamudMCP.Host", "bin", "Release", "net10.0");
        Directory.CreateDirectory(pluginDirectory);
        Directory.CreateDirectory(hostDirectory);

        var pluginAssemblyPath = Path.Combine(pluginDirectory, "DalamudMCP.dll");
        var hostDllPath = Path.Combine(hostDirectory, "DalamudMCP.Host.dll");
        File.WriteAllText(pluginAssemblyPath, string.Empty);
        File.WriteAllText(hostDllPath, string.Empty);

        var resolver = new PluginHostPathResolver(pluginAssemblyPath, "DalamudMCP.456");
        var resolution = resolver.TryResolveConsole();

        Assert.NotNull(resolution);
        Assert.Equal("dotnet", resolution!.DotNetExecutable);
        Assert.Equal(hostDllPath, resolution.HostDllPath);
        Assert.Contains("--pipe-name", resolution.CommandText, StringComparison.Ordinal);
    }
}
