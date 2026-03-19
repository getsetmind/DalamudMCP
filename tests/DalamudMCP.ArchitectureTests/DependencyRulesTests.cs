namespace DalamudMCP.ArchitectureTests;

public sealed class DependencyRulesTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void Domain_DoesNotReferenceOuterAssemblies()
    {
        AssertDoesNotReference(
            typeof(DalamudMCP.Domain.AssemblyMarker).Assembly,
            "DalamudMCP.Application",
            "DalamudMCP.Infrastructure",
            "DalamudMCP.Plugin",
            "DalamudMCP.Host");
    }

    [Fact]
    public void Application_DoesNotReferenceInfrastructure()
    {
        AssertDoesNotReference(
            typeof(DalamudMCP.Application.AssemblyMarker).Assembly,
            "DalamudMCP.Infrastructure");
    }

    [Fact]
    public void Contracts_DoesNotReferencePluginOrHost()
    {
        AssertDoesNotReference(
            typeof(DalamudMCP.Contracts.AssemblyMarker).Assembly,
            "DalamudMCP.Plugin",
            "DalamudMCP.Host");
    }

    [Fact]
    public void Infrastructure_DoesNotReferencePluginOrHost()
    {
        AssertDoesNotReference(
            typeof(DalamudMCP.Infrastructure.AssemblyMarker).Assembly,
            "DalamudMCP.Plugin",
            "DalamudMCP.Host");
    }

    [Fact]
    public void Plugin_DoesNotReferenceHost()
    {
        AssertDoesNotReference(
            typeof(DalamudMCP.Plugin.PluginEntryPoint).Assembly,
            "DalamudMCP.Host");
    }

    [Fact]
    public void Host_DoesNotReferencePlugin()
    {
        AssertDoesNotReference(
            typeof(DalamudMCP.Host.HostProgram).Assembly,
            "DalamudMCP.Plugin");
    }

    [Fact]
    public void Domain_DoesNotReferenceDalamudOrMcpAssemblies()
    {
        AssertDoesNotReferencePrefixes(
            typeof(DalamudMCP.Domain.AssemblyMarker).Assembly,
            "Dalamud",
            "ModelContextProtocol");
    }

    [Fact]
    public void Contracts_DoesNotReferenceDalamudOrMcpAssemblies()
    {
        AssertDoesNotReferencePrefixes(
            typeof(DalamudMCP.Contracts.AssemblyMarker).Assembly,
            "Dalamud",
            "ModelContextProtocol");
    }

    [Fact]
    public void Plugin_SourceFiles_StayWithin_CompositionRoot_Reader_AndUi_Areas()
    {
        AssertProjectSourceLayout(
            "DalamudMCP.Plugin",
            allowedRootFiles:
            [
                "PluginCompositionRoot.cs",
                "PluginEntryPoint.cs",
                "PluginRuntimeOptions.cs",
            ],
            allowedRootPrefixes: [],
            allowedDirectories:
            [
                "Readers",
                "UI",
                "ViewModels",
                "Views",
                "Windows",
            ]);
    }

    [Fact]
    public void Host_SourceFiles_StayWithin_Transport_Registry_Handler_AndCli_Areas()
    {
        AssertProjectSourceLayout(
            "DalamudMCP.Host",
            allowedRootFiles:
            [
                "HostProgram.cs",
                "HostRuntimeOptions.cs",
                "PluginBridgeClient.cs",
                "Program.cs",
                "StdioTransportHost.cs",
            ],
            allowedRootPrefixes:
            [
                "Mcp",
            ],
            allowedDirectories:
            [
                "Bridge",
                "Resources",
                "Tools",
            ]);
    }

    [Fact]
    public void Contracts_SourceFiles_DoNotDeclare_OrImport_McpDtos()
    {
        var contractsDirectory = Path.Combine(RepositoryRoot, "src", "DalamudMCP.Contracts");
        var sourceFiles = EnumerateProjectSourceFiles(contractsDirectory);
        var offendingNames = sourceFiles
            .Select(path => Path.GetRelativePath(contractsDirectory, path))
            .Where(static path => Path.GetFileName(path).StartsWith("Mcp", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var offendingImports = sourceFiles
            .Select(path => new
            {
                RelativePath = Path.GetRelativePath(contractsDirectory, path),
                Content = File.ReadAllText(path),
            })
            .Where(static file => file.Content.Contains("using ModelContextProtocol", StringComparison.OrdinalIgnoreCase) ||
                                  file.Content.Contains("namespace ModelContextProtocol", StringComparison.OrdinalIgnoreCase))
            .Select(static file => file.RelativePath)
            .ToArray();

        Assert.True(
            offendingNames.Length == 0 && offendingImports.Length == 0,
            $"Contracts source must stay bridge-specific and must not declare or import MCP DTOs. Offending file names: {string.Join(", ", offendingNames)}. Offending imports: {string.Join(", ", offendingImports)}.");
    }

    private static void AssertDoesNotReference(System.Reflection.Assembly assembly, params string[] forbiddenAssemblyNames)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(forbiddenAssemblyNames);

        var references = GetReferenceNames(assembly);
        foreach (var forbiddenAssemblyName in forbiddenAssemblyNames)
        {
            Assert.False(
                references.Contains(forbiddenAssemblyName),
                CreateAssemblyReferenceFailureMessage(assembly.GetName().Name!, forbiddenAssemblyName, references));
        }
    }

    private static void AssertDoesNotReferencePrefixes(System.Reflection.Assembly assembly, params string[] forbiddenPrefixes)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(forbiddenPrefixes);

        var references = GetReferenceNames(assembly);
        foreach (var forbiddenPrefix in forbiddenPrefixes)
        {
            var offending = references
                .Where(reference => reference.StartsWith(forbiddenPrefix, StringComparison.OrdinalIgnoreCase))
                .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            Assert.True(
                offending.Length == 0,
                CreateAssemblyPrefixFailureMessage(assembly.GetName().Name!, forbiddenPrefix, offending, references));
        }
    }

    private static void AssertProjectSourceLayout(
        string projectName,
        IReadOnlyCollection<string> allowedRootFiles,
        IReadOnlyCollection<string> allowedRootPrefixes,
        IReadOnlyCollection<string> allowedDirectories)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectName);
        ArgumentNullException.ThrowIfNull(allowedRootFiles);
        ArgumentNullException.ThrowIfNull(allowedRootPrefixes);
        ArgumentNullException.ThrowIfNull(allowedDirectories);

        var projectDirectory = Path.Combine(RepositoryRoot, "src", projectName);
        var sourceFiles = EnumerateProjectSourceFiles(projectDirectory)
            .Select(path => Path.GetRelativePath(projectDirectory, path))
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var offendingFiles = sourceFiles
            .Where(path => !IsAllowedSourcePath(path, allowedRootFiles, allowedRootPrefixes, allowedDirectories))
            .ToArray();

        Assert.True(
            offendingFiles.Length == 0,
            CreateSourceLayoutFailureMessage(projectName, offendingFiles, allowedRootFiles, allowedRootPrefixes, allowedDirectories, sourceFiles));
    }

    private static HashSet<string> GetReferenceNames(System.Reflection.Assembly assembly) =>
        assembly
            .GetReferencedAssemblies()
            .Select(static reference => reference.Name)
            .OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static bool IsAllowedSourcePath(
        string relativePath,
        IReadOnlyCollection<string> allowedRootFiles,
        IReadOnlyCollection<string> allowedRootPrefixes,
        IReadOnlyCollection<string> allowedDirectories)
    {
        var segments = relativePath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 1)
        {
            var fileName = segments[0];
            return allowedRootFiles.Contains(fileName, StringComparer.OrdinalIgnoreCase) ||
                   allowedRootPrefixes.Any(prefix => fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }

        return allowedDirectories.Contains(segments[0], StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> EnumerateProjectSourceFiles(string projectDirectory) =>
        Directory
            .EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));

    private static string CreateAssemblyReferenceFailureMessage(
        string assemblyName,
        string forbiddenAssemblyName,
        IReadOnlyCollection<string> references) =>
        $"Assembly '{assemblyName}' must not reference '{forbiddenAssemblyName}'. Referenced assemblies: {string.Join(", ", references.OrderBy(static value => value, StringComparer.OrdinalIgnoreCase))}";

    private static string CreateAssemblyPrefixFailureMessage(
        string assemblyName,
        string forbiddenPrefix,
        IReadOnlyCollection<string> offendingReferences,
        IReadOnlyCollection<string> references) =>
        $"Assembly '{assemblyName}' must not reference assemblies with prefix '{forbiddenPrefix}'. Offending references: {string.Join(", ", offendingReferences)}. Referenced assemblies: {string.Join(", ", references.OrderBy(static value => value, StringComparer.OrdinalIgnoreCase))}";

    private static string CreateSourceLayoutFailureMessage(
        string projectName,
        IReadOnlyCollection<string> offendingFiles,
        IReadOnlyCollection<string> allowedRootFiles,
        IReadOnlyCollection<string> allowedRootPrefixes,
        IReadOnlyCollection<string> allowedDirectories,
        IReadOnlyCollection<string> discoveredFiles) =>
        $"Project '{projectName}' contains source files outside the allowed responsibility areas. Offending files: {string.Join(", ", offendingFiles)}. Allowed root files: {string.Join(", ", allowedRootFiles.OrderBy(static value => value, StringComparer.OrdinalIgnoreCase))}. Allowed root prefixes: {string.Join(", ", allowedRootPrefixes.OrderBy(static value => value, StringComparer.OrdinalIgnoreCase))}. Allowed directories: {string.Join(", ", allowedDirectories.OrderBy(static value => value, StringComparer.OrdinalIgnoreCase))}. Discovered source files: {string.Join(", ", discoveredFiles)}";

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DalamudMCP.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the DalamudMCP repository root from the architecture test output directory.");
    }
}
