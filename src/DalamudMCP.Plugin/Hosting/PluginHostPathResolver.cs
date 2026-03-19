namespace DalamudMCP.Plugin.Hosting;

public sealed class PluginHostPathResolver
{
    private readonly string pluginAssemblyPath;
    private readonly string pipeName;

    public PluginHostPathResolver(string pluginAssemblyPath, string pipeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginAssemblyPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        this.pluginAssemblyPath = Path.GetFullPath(pluginAssemblyPath);
        this.pipeName = pipeName.Trim();
    }

    public HostLaunchResolution? TryResolveConsole()
    {
        return TryResolveCore(
            ["DalamudMCP.Host.dll", "--pipe-name", pipeName],
            endpointUrl: null);
    }

    public HostLaunchResolution? TryResolveHttpServer(int port, string httpHost = "127.0.0.1", string mcpPath = "/mcp")
    {
        if (port <= 0 || port > 65535)
        {
            return null;
        }

        var normalizedPath = mcpPath.Length > 0 && mcpPath[0] == '/' ? mcpPath : "/" + mcpPath;
        return TryResolveCore(
            [
                "DalamudMCP.Host.dll",
                "--pipe-name",
                pipeName,
                "--transport",
                "http",
                "--http-host",
                httpHost,
                "--http-port",
                port.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "--mcp-path",
                normalizedPath,
            ],
            $"http://{httpHost}:{port}{normalizedPath}");
    }

    private HostLaunchResolution? TryResolveCore(string[] suffixArguments, string? endpointUrl)
    {
        var pluginAssemblyDirectory = Path.GetDirectoryName(pluginAssemblyPath);
        if (string.IsNullOrWhiteSpace(pluginAssemblyDirectory))
        {
            return null;
        }

        var hostDllPath = ResolveHostDllPath(pluginAssemblyDirectory);
        if (hostDllPath is null)
        {
            return null;
        }

        var allArguments = new[] { hostDllPath }.Concat(suffixArguments.Skip(1)).ToArray();
        return new HostLaunchResolution(
            ResolveDotNetExecutable(pluginAssemblyDirectory),
            hostDllPath,
            pipeName,
            allArguments,
            endpointUrl);
    }

    private static string ResolveDotNetExecutable(string pluginAssemblyDirectory)
    {
        var repoLocalDotNet = Path.GetFullPath(Path.Combine(pluginAssemblyDirectory, "..", "..", "..", "..", "..", ".dotnet", "dotnet.exe"));
        return File.Exists(repoLocalDotNet)
            ? repoLocalDotNet
            : "dotnet";
    }

    private static string? ResolveHostDllPath(string pluginAssemblyDirectory)
    {
        var frameworkMoniker = new DirectoryInfo(pluginAssemblyDirectory).Name;
        var configurationDirectory = Directory.GetParent(pluginAssemblyDirectory)?.Name;
        if (string.IsNullOrWhiteSpace(configurationDirectory))
        {
            return null;
        }

        var repoRoot = Path.GetFullPath(Path.Combine(
            pluginAssemblyDirectory,
            "..",
            "..",
            "..",
            "..",
            ".."));

        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(pluginAssemblyDirectory, "DalamudMCP.Host.dll")),
            Path.Combine(
                repoRoot,
                "artifacts",
                "verify",
                configurationDirectory,
                frameworkMoniker,
                "DalamudMCP.Host.dll"),
            Path.GetFullPath(Path.Combine(
                repoRoot,
                "src",
                "DalamudMCP.Host",
                "bin",
                configurationDirectory,
                frameworkMoniker,
                "DalamudMCP.Host.dll")),
        }.Select(Path.GetFullPath);

        return candidates.FirstOrDefault(File.Exists);
    }
}
