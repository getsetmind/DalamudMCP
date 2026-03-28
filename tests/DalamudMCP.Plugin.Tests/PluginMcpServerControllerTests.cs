using System.Text.Json;
using DalamudMCP.Plugin.Hosting;

namespace DalamudMCP.Plugin.Tests;

public sealed class PluginMcpServerControllerTests
{
    [Fact]
    public void Start_returns_false_when_cli_cannot_be_resolved()
    {
        string root = CreateTempDirectory();
        try
        {
            string pluginOutput = Path.Combine(root, "src", "DalamudMCP.Plugin", "bin", "Debug", "net10.0");
            Directory.CreateDirectory(pluginOutput);
            string pluginAssemblyPath = Path.Combine(pluginOutput, "DalamudMCP.dll");
            File.WriteAllText(pluginAssemblyPath, string.Empty);

            PluginMcpServerController controller = new(
                new PluginCliPathResolver(pluginAssemblyPath, "DalamudMCP.12345"),
                static _ => false);

            bool started = controller.Start();

            Assert.False(started);
            Assert.False(controller.IsRunning);
            Assert.Contains("could not be resolved", controller.LastError, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void GetStatus_reports_external_server_when_probe_succeeds()
    {
        string root = CreateTempDirectory();
        try
        {
            string pluginOutput = Path.Combine(root, "src", "DalamudMCP.Plugin", "bin", "Debug", "net10.0");
            Directory.CreateDirectory(pluginOutput);
            string pluginAssemblyPath = Path.Combine(pluginOutput, "DalamudMCP.dll");
            File.WriteAllText(pluginAssemblyPath, string.Empty);

            PluginMcpServerController controller = new(
                new PluginCliPathResolver(pluginAssemblyPath, "DalamudMCP.12345"),
                static _ => true);

            PluginMcpServerStatus status = controller.GetStatus();

            Assert.True(status.IsRunning);
            Assert.Equal("http://127.0.0.1:38473/mcp", status.EndpointUrl);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void IsRunning_does_not_allocate_after_cached_probe()
    {
        string root = CreateTempDirectory();
        try
        {
            string pluginOutput = Path.Combine(root, "src", "DalamudMCP.Plugin", "bin", "Debug", "net10.0");
            Directory.CreateDirectory(pluginOutput);
            string pluginAssemblyPath = Path.Combine(pluginOutput, "DalamudMCP.dll");
            File.WriteAllText(pluginAssemblyPath, string.Empty);

            PluginMcpServerController controller = new(
                new PluginCliPathResolver(pluginAssemblyPath, "DalamudMCP.12345"),
                static _ => true);

            Assert.True(controller.IsRunning);

            long before = GC.GetAllocatedBytesForCurrentThread();
            bool isRunning = controller.IsRunning;
            long after = GC.GetAllocatedBytesForCurrentThread();

            Assert.True(isRunning);
            Assert.Equal(0, after - before);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void IsRunning_returns_false_when_endpoint_catalog_is_stale()
    {
        string root = CreateTempDirectory();
        try
        {
            string pluginOutput = Path.Combine(root, "src", "DalamudMCP.Plugin", "bin", "Debug", "net10.0");
            Directory.CreateDirectory(pluginOutput);
            string pluginAssemblyPath = Path.Combine(pluginOutput, "DalamudMCP.dll");
            File.WriteAllText(pluginAssemblyPath, string.Empty);

            PluginMcpServerController controller = new(
                new PluginCliPathResolver(pluginAssemblyPath, "DalamudMCP.12345"),
                static _ => new EndpointProbeResult(true, false, "stale_server"),
                static _ => false);

            bool isRunning = controller.IsRunning;

            Assert.False(isRunning);
            Assert.Equal("stale_server", controller.LastError);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void TryReadToolNames_reads_names_from_tools_list_payload()
    {
        using JsonDocument document = JsonDocument.Parse(
            """
            {
              "jsonrpc": "2.0",
              "id": "probe-list",
              "result": {
                "tools": [
                  { "name": "get_player_context" },
                  { "name": "get_duty_context" }
                ]
              }
            }
            """);

        bool parsed = PluginMcpServerController.TryReadToolNames(document, out HashSet<string>? toolNames);

        Assert.True(parsed);
        Assert.NotNull(toolNames);
        Assert.Equal(
            ["get_duty_context", "get_player_context"],
            toolNames.OrderBy(static name => name, StringComparer.Ordinal));
    }

    [Fact]
    public void TryParseListeningProcessIds_reads_listening_pids_for_port()
    {
        const string netstatOutput =
            """
              Proto  Local Address          Foreign Address        State           PID
              TCP    127.0.0.1:38473        0.0.0.0:0              LISTENING       1234
              TCP    [::]:38473             [::]:0                 LISTENING       5678
              TCP    127.0.0.1:38474        0.0.0.0:0              LISTENING       9012
            """;

        bool parsed = PluginMcpServerController.TryParseListeningProcessIds(netstatOutput, 38473, out List<int> processIds);

        Assert.True(parsed);
        Assert.Equal([1234, 5678], processIds);
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "DalamudMCP.Plugin.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
