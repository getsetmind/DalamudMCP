namespace DalamudMCP.Protocol.Tests;

public sealed class ProtocolClientDiscoveryTests
{
    [Fact]
    public void Write_and_try_read_round_trip_discovery_record()
    {
        string directory = CreateTempDirectory();
        try
        {
            ProtocolClientDiscoveryRecord expected = new("DalamudMCP.12345.instance", 12345, DateTimeOffset.UtcNow);

            ProtocolClientDiscovery.Write(expected, directory);
            bool read = ProtocolClientDiscovery.TryRead(out ProtocolClientDiscoveryRecord? actual, directory);

            Assert.True(read);
            Assert.NotNull(actual);
            Assert.Equal(expected.PipeName, actual!.PipeName);
            Assert.Equal(expected.ProcessId, actual.ProcessId);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void DeleteIfMatches_removes_matching_discovery_file_only()
    {
        string directory = CreateTempDirectory();
        try
        {
            ProtocolClientDiscovery.Write(
                new ProtocolClientDiscoveryRecord("DalamudMCP.keep", 1, DateTimeOffset.UtcNow),
                directory);

            ProtocolClientDiscovery.DeleteIfMatches("DalamudMCP.other", directory);
            Assert.True(File.Exists(ProtocolClientDiscovery.GetFilePath(directory)));

            ProtocolClientDiscovery.DeleteIfMatches("DalamudMCP.keep", directory);
            Assert.False(File.Exists(ProtocolClientDiscovery.GetFilePath(directory)));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "DalamudMCP.Protocol.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
