using System.Globalization;

namespace DalamudMCP.Plugin.Hosting;

public sealed class PluginCliPathResolver
{
    private readonly string pluginAssemblyPath;
    private readonly string pipeName;

    public PluginCliPathResolver(string pluginAssemblyPath, string pipeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginAssemblyPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);

        this.pluginAssemblyPath = Path.GetFullPath(pluginAssemblyPath);
        this.pipeName = pipeName.Trim();
    }

    public PluginCliLaunchResolution? ResolveHttpServer(int port, string? path = null)
    {
        IReadOnlyList<PluginCliLaunchResolution> resolutions = ResolveHttpServerCandidates(port, path);
        return resolutions.Count == 0
            ? null
            : resolutions[0];
    }

    public IReadOnlyList<PluginCliLaunchResolution> ResolveHttpServerCandidates(int port, string? path = null)
    {
        if (port <= 0 || port > 65535)
            return [];

        string normalizedPath = NormalizePath(path);
        string[] suffixArguments = ["--pipe", pipeName, "serve", "http", "--port", port.ToString(CultureInfo.InvariantCulture), "--path", normalizedPath];
        string endpointUrl = $"http://127.0.0.1:{port.ToString(CultureInfo.InvariantCulture)}{normalizedPath}";

        string? pluginAssemblyDirectory = Path.GetDirectoryName(pluginAssemblyPath);
        if (string.IsNullOrWhiteSpace(pluginAssemblyDirectory))
            return [];

        List<PluginCliLaunchResolution> resolutions = [];

        string bundledDirectory = Path.Combine(pluginAssemblyDirectory, "server");
        AddResolutionIfExists(resolutions, Path.Combine(bundledDirectory, "DalamudMCP.Cli.exe"), suffixArguments, endpointUrl);
        AddResolutionIfExists(resolutions, Path.Combine(bundledDirectory, "DalamudMCP.Cli.dll"), suffixArguments, endpointUrl);

        string repoRoot = Path.GetFullPath(Path.Combine(
            pluginAssemblyDirectory,
            "..",
            "..",
            "..",
            "..",
            ".."));
        string frameworkMoniker = new DirectoryInfo(pluginAssemblyDirectory).Name;
        string? configurationDirectory = Directory.GetParent(pluginAssemblyDirectory)?.Name;
        if (string.IsNullOrWhiteSpace(configurationDirectory))
            return resolutions;

        string repoOutputDirectory = Path.Combine(repoRoot, "src", "DalamudMCP.Cli", "bin", configurationDirectory, frameworkMoniker);
        AddResolutionIfExists(resolutions, Path.Combine(repoOutputDirectory, "DalamudMCP.Cli.exe"), suffixArguments, endpointUrl);
        AddResolutionIfExists(resolutions, Path.Combine(repoOutputDirectory, "DalamudMCP.Cli.dll"), suffixArguments, endpointUrl);
        return resolutions;
    }

    private static PluginCliLaunchResolution CreateResolution(
        string executableOrDllPath,
        IReadOnlyList<string> suffixArguments,
        string endpointUrl)
    {
        string fullPath = Path.GetFullPath(executableOrDllPath);
        string workingDirectory = Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory;
        bool isDll = string.Equals(Path.GetExtension(fullPath), ".dll", StringComparison.OrdinalIgnoreCase);
        string fileName = isDll ? ResolveDotNetExecutable(workingDirectory) : fullPath;
        string[] arguments = isDll
            ? [fullPath, .. suffixArguments]
            : [.. suffixArguments];

        string commandText = string.Join(
            ' ',
            [QuoteIfNeeded(fileName), .. arguments.Select(QuoteIfNeeded)]);

        return new PluginCliLaunchResolution(fileName, workingDirectory, arguments, endpointUrl, commandText);
    }

    private static void AddResolutionIfExists(
        List<PluginCliLaunchResolution> resolutions,
        string candidatePath,
        IReadOnlyList<string> suffixArguments,
        string endpointUrl)
    {
        if (!File.Exists(candidatePath))
            return;

        resolutions.Add(CreateResolution(candidatePath, suffixArguments, endpointUrl));
    }

    private static string ResolveDotNetExecutable(string workingDirectory)
    {
        DirectoryInfo? current = new(Path.GetFullPath(workingDirectory));
        while (current is not null)
        {
            string repoLocalDotNet = Path.Combine(current.FullName, ".dotnet", "dotnet.exe");
            if (File.Exists(repoLocalDotNet))
                return repoLocalDotNet;

            current = current.Parent;
        }

        return "dotnet";
    }

    private static string NormalizePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return CliDefaults.HttpPath;

        string trimmed = value.Trim();
        return trimmed.StartsWith('/')
            ? trimmed
            : "/" + trimmed;
    }

    private static string QuoteIfNeeded(string value)
    {
        return value.Contains(' ')
            ? $"\"{value}\""
            : value;
    }
}

public sealed record PluginCliLaunchResolution(
    string FileName,
    string WorkingDirectory,
    IReadOnlyList<string> Arguments,
    string EndpointUrl,
    string CommandText);

internal static class CliDefaults
{
    public const int HttpPort = 38473;
    public const string HttpPath = "/mcp";
}
