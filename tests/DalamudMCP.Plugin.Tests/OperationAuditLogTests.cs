using System.Text.Json;
using DalamudMCP.Plugin.Hosting;
using Manifold;

namespace DalamudMCP.Plugin.Tests;

public sealed class OperationAuditLogTests
{
    [Fact]
    public async Task WriteAsync_records_bounded_arguments_without_secret_payloads()
    {
        string root = Path.Combine(Path.GetTempPath(), "DalamudMCP.Audit.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            PluginRuntimeOptions options = new("DalamudMCP.Test", root);
            using OperationAuditLog log = new(options);
            OperationDescriptor operation = new(
                "command.slash",
                typeof(object),
                "ExecuteAsync",
                typeof(object),
                OperationVisibility.Both,
                []);

            await log.WriteAsync(
                operation,
                "request-1",
                new TestRequest("ExamplePlugin", "reload", "do-not-log"),
                new TestResult("completed", "1.2.3", "https://example.invalid/repository"),
                CapabilityAuthorizationDecision.Allowed("command.execute"),
                succeeded: true,
                errorCode: null,
                elapsedMilliseconds: 12,
                TestContext.Current.CancellationToken);

            string path = Path.Combine(root, "audit", "operations.jsonl");
            using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
            JsonElement entry = document.RootElement;
            Assert.Equal("command.slash", entry.GetProperty("operationId").GetString());
            Assert.Equal("ExamplePlugin", entry.GetProperty("arguments").GetProperty("PluginName").GetString());
            Assert.False(entry.GetProperty("arguments").TryGetProperty("Secret", out _));
            Assert.Equal("1.2.3", entry.GetProperty("result").GetProperty("Version").GetString());
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private sealed record TestRequest(string PluginName, string Action, string Secret);

    private sealed record TestResult(string Status, string Version, string Repository);
}
