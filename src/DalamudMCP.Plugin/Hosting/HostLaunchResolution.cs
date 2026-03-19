namespace DalamudMCP.Plugin.Hosting;

public sealed record HostLaunchResolution(
    string DotNetExecutable,
    string HostDllPath,
    string PipeName,
    string[] Arguments,
    string? EndpointUrl)
{
    public string WorkingDirectory =>
        Path.GetDirectoryName(HostDllPath)
        ?? throw new InvalidOperationException("Host DLL path has no directory.");

    public string CommandText =>
        $"{Quote(DotNetExecutable)} {string.Join(" ", Arguments.Select(Quote))}";

    private static string Quote(string value) =>
        string.IsNullOrWhiteSpace(value) || value.Contains(' ', StringComparison.Ordinal)
            ? $"\"{value}\""
            : value;
}
