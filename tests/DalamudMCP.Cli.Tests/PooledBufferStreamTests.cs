namespace DalamudMCP.Cli.Tests;

public sealed class PooledBufferStreamTests
{
    [Fact]
    public async Task WriteAsync_collects_written_bytes()
    {
        await using PooledBufferStream stream = new();

        await stream.WriteAsync("hello"u8.ToArray(), TestContext.Current.CancellationToken);
        await stream.WriteAsync(",world"u8.ToArray(), TestContext.Current.CancellationToken);

        Assert.Equal(11, stream.Length);
        Assert.Equal("hello,world", System.Text.Encoding.UTF8.GetString(stream.WrittenMemory.Span));
    }

    [Fact]
    public void Seek_and_overwrite_updates_buffer()
    {
        using PooledBufferStream stream = new();
        stream.Write("hello world"u8);
        stream.Seek(6, SeekOrigin.Begin);
        stream.Write("Dalamud"u8);

        Assert.Equal("hello Dalamud", System.Text.Encoding.UTF8.GetString(stream.WrittenMemory.Span));
    }
}
