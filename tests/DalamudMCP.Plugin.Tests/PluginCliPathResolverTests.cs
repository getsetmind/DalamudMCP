using DalamudMCP.Plugin.Hosting;

namespace DalamudMCP.Plugin.Tests;

public sealed class PluginCliPathResolverTests
{
    [Fact]
    public void ResolveHttpServer_prefers_bundled_cli_executable()
    {
        string root = CreateTempDirectory();
        try
        {
            string pluginOutput = Path.Combine(root, "src", "DalamudMCP.Plugin", "bin", "Debug", "net10.0");
            Directory.CreateDirectory(Path.Combine(pluginOutput, "server"));
            string pluginAssemblyPath = Path.Combine(pluginOutput, "DalamudMCP.dll");
            string bundledCliExePath = Path.Combine(pluginOutput, "server", "DalamudMCP.Cli.exe");
            string bundledCliPath = Path.Combine(pluginOutput, "server", "DalamudMCP.Cli.dll");
            File.WriteAllText(pluginAssemblyPath, string.Empty);
            File.WriteAllText(bundledCliExePath, string.Empty);
            File.WriteAllText(bundledCliPath, string.Empty);

            PluginCliPathResolver resolver = new(pluginAssemblyPath, "DalamudMCP.12345");
            PluginCliLaunchResolution? resolution = resolver.ResolveHttpServer(38473);

            Assert.NotNull(resolution);
            Assert.Equal(Path.GetFullPath(bundledCliExePath), resolution!.FileName);
            Assert.Equal("http://127.0.0.1:38473/mcp", resolution.EndpointUrl);
            Assert.Equal(Path.GetFullPath(Path.Combine(pluginOutput, "server")), resolution.WorkingDirectory);
            Assert.Contains("--pipe", resolution.Arguments);
            Assert.Contains("serve", resolution.Arguments);
            Assert.Contains("http", resolution.Arguments);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ResolveHttpServer_falls_back_to_repo_cli_output()
    {
        string root = CreateTempDirectory();
        try
        {
            string pluginOutput = Path.Combine(root, "src", "DalamudMCP.Plugin", "bin", "Debug", "net10.0");
            string cliOutput = Path.Combine(root, "src", "DalamudMCP.Cli", "bin", "Debug", "net10.0");
            Directory.CreateDirectory(pluginOutput);
            Directory.CreateDirectory(cliOutput);

            string pluginAssemblyPath = Path.Combine(pluginOutput, "DalamudMCP.dll");
            string repoCliPath = Path.Combine(cliOutput, "DalamudMCP.Cli.exe");
            File.WriteAllText(pluginAssemblyPath, string.Empty);
            File.WriteAllText(repoCliPath, string.Empty);

            PluginCliPathResolver resolver = new(pluginAssemblyPath, "DalamudMCP.77777");
            PluginCliLaunchResolution? resolution = resolver.ResolveHttpServer(39555, "/live");

            Assert.NotNull(resolution);
            Assert.Equal(Path.GetFullPath(repoCliPath), resolution!.FileName);
            Assert.Equal("http://127.0.0.1:39555/live", resolution.EndpointUrl);
            Assert.Equal(Path.GetFullPath(cliOutput), resolution.WorkingDirectory);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ResolveHttpServerCandidates_returns_exe_then_dll_fallbacks()
    {
        string root = CreateTempDirectory();
        try
        {
            string pluginOutput = Path.Combine(root, "src", "DalamudMCP.Plugin", "bin", "Debug", "net10.0");
            string serverOutput = Path.Combine(pluginOutput, "server");
            Directory.CreateDirectory(serverOutput);

            string pluginAssemblyPath = Path.Combine(pluginOutput, "DalamudMCP.dll");
            string bundledExePath = Path.Combine(serverOutput, "DalamudMCP.Cli.exe");
            string bundledDllPath = Path.Combine(serverOutput, "DalamudMCP.Cli.dll");
            File.WriteAllText(pluginAssemblyPath, string.Empty);
            File.WriteAllText(bundledExePath, string.Empty);
            File.WriteAllText(bundledDllPath, string.Empty);

            PluginCliPathResolver resolver = new(pluginAssemblyPath, "DalamudMCP.12345");
            IReadOnlyList<PluginCliLaunchResolution> resolutions = resolver.ResolveHttpServerCandidates(38473);

            Assert.Equal(2, resolutions.Count);
            Assert.Equal(Path.GetFullPath(bundledExePath), resolutions[0].FileName);
            Assert.Equal("dotnet", Path.GetFileNameWithoutExtension(resolutions[1].FileName));
            Assert.Equal(Path.GetFullPath(bundledDllPath), resolutions[1].Arguments[0]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "DalamudMCP.Plugin.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
