using System.Text.Json;

namespace DalamudMCP.Cli.Tests;

public sealed class PackagingMetadataTests
{
    [Fact]
    public void Server_manifest_matches_the_CLI_package()
    {
        string manifestPath = Path.Combine(AppContext.BaseDirectory, ".mcp", "server.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        JsonElement root = document.RootElement;

        Assert.Equal("io.github.getsetmind/dalamudmcp", root.GetProperty("name").GetString());
        Assert.Equal(GetPackageVersion(), root.GetProperty("version").GetString());

        JsonElement package = Assert.Single(root.GetProperty("packages").EnumerateArray());
        Assert.Equal("nuget", package.GetProperty("registryType").GetString());
        Assert.Equal("DalamudMCP.Cli", package.GetProperty("identifier").GetString());
        Assert.Equal(GetPackageVersion(), package.GetProperty("version").GetString());
        Assert.Equal("stdio", package.GetProperty("transport").GetProperty("type").GetString());

        string[] arguments = package.GetProperty("packageArguments")
            .EnumerateArray()
            .Select(argument => argument.GetProperty("value").GetString()
                ?? throw new InvalidOperationException("A package argument value is missing."))
            .ToArray();
        Assert.Equal(["serve", "mcp"], arguments);
    }

    private static string GetPackageVersion()
    {
        Version version = typeof(CliProgram).Assembly.GetName().Version
            ?? throw new InvalidOperationException("The CLI assembly version is unavailable.");
        return $"{version.Major}.{version.Minor}.{version.Build}";
    }
}
