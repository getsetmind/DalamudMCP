using DalamudMCP.Domain.Audit;
using DalamudMCP.Infrastructure.Audit;

namespace DalamudMCP.Infrastructure.Tests.Audit;

public sealed class FileAuditLogWriterTests
{
    [Fact]
    public async Task WriteAsync_AppendsJsonLine()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var path = Path.Combine(dir, "audit.log");
        var writer = new FileAuditLogWriter(path);

        await writer.WriteAsync(
            new AuditEvent(
                new DateTimeOffset(2026, 3, 20, 0, 0, 0, TimeSpan.Zero),
                "tool.enabled",
                "get_player_context"),
            CancellationToken.None);

        var content = await File.ReadAllTextAsync(path, CancellationToken.None);

        Assert.Contains("tool.enabled", content, StringComparison.Ordinal);
        Assert.Contains("get_player_context", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WriteAsync_SupportsConcurrentWriters()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var path = Path.Combine(dir, "audit.log");
        var writer = new FileAuditLogWriter(path);

        await Task.WhenAll(
            Enumerable.Range(0, 12).Select(index =>
                writer.WriteAsync(
                    new AuditEvent(
                        new DateTimeOffset(2026, 3, 20, 0, 0, index, TimeSpan.Zero),
                        "tool.enabled",
                        $"tool_{index}"),
                    CancellationToken.None)));

        var lines = await File.ReadAllLinesAsync(path, CancellationToken.None);

        Assert.Equal(12, lines.Length);
        Assert.Contains(lines, static line => line.Contains("tool_0", StringComparison.Ordinal));
        Assert.Contains(lines, static line => line.Contains("tool_11", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WriteAsync_RotatesFile_WhenMaxSizeExceeded()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var path = Path.Combine(dir, "audit.log");
        var writer = new FileAuditLogWriter(path, maxFileBytes: 120);

        await writer.WriteAsync(
            new AuditEvent(
                new DateTimeOffset(2026, 3, 20, 0, 0, 0, TimeSpan.Zero),
                "tool.enabled",
                new string('a', 80)),
            CancellationToken.None);
        await writer.WriteAsync(
            new AuditEvent(
                new DateTimeOffset(2026, 3, 20, 0, 0, 1, TimeSpan.Zero),
                "tool.disabled",
                new string('b', 80)),
            CancellationToken.None);

        var rotatedFiles = Directory.GetFiles(dir, "audit.*.log");
        var currentContent = await File.ReadAllTextAsync(path, CancellationToken.None);

        Assert.Single(rotatedFiles);
        Assert.Contains("tool.disabled", currentContent, StringComparison.Ordinal);
    }
}
