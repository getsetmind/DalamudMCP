using System.Text.Json;

namespace DalamudMCP.Protocol;

public sealed record ProtocolClientDiscoveryRecord(
    string PipeName,
    int ProcessId,
    DateTimeOffset UpdatedAtUtc);

public static class ProtocolClientDiscovery
{
    public const string ConfigDirectoryEnvironmentVariableName = "DALAMUD_MCP_CONFIG_DIR";
    public const string FileName = "active-instance.json";

    public static string GetDefaultConfigDirectory()
    {
        string? configuredDirectory = Environment.GetEnvironmentVariable(ConfigDirectoryEnvironmentVariableName);
        if (!string.IsNullOrWhiteSpace(configuredDirectory))
            return configuredDirectory.Trim();

        string applicationDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(applicationDataPath, "XIVLauncher", "pluginConfigs", "DalamudMCP");
    }

    public static string GetFilePath(string? configDirectory = null)
    {
        string directory = string.IsNullOrWhiteSpace(configDirectory)
            ? GetDefaultConfigDirectory()
            : configDirectory.Trim();
        return Path.Combine(directory, FileName);
    }

    public static void Write(ProtocolClientDiscoveryRecord record, string? configDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(record);

        string filePath = GetFilePath(configDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? throw new InvalidOperationException("Discovery file path did not include a directory."));

        string temporaryFilePath = filePath + ".tmp";
        string json = JsonSerializer.Serialize(record, ProtocolContract.JsonOptions);
        File.WriteAllText(temporaryFilePath, json);
        File.Move(temporaryFilePath, filePath, overwrite: true);
    }

    public static bool TryRead(out ProtocolClientDiscoveryRecord? record, string? configDirectory = null)
    {
        string filePath = GetFilePath(configDirectory);
        return TryReadFile(filePath, out record);
    }

    public static void DeleteIfMatches(string expectedPipeName, string? configDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedPipeName);

        string filePath = GetFilePath(configDirectory);
        if (!TryReadFile(filePath, out ProtocolClientDiscoveryRecord? current) ||
            current is null ||
            !string.Equals(current.PipeName, expectedPipeName, StringComparison.Ordinal))
        {
            return;
        }

        File.Delete(filePath);
    }

    private static bool TryReadFile(string filePath, out ProtocolClientDiscoveryRecord? record)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        try
        {
            if (!File.Exists(filePath))
            {
                record = null;
                return false;
            }

            string json = File.ReadAllText(filePath);
            record = JsonSerializer.Deserialize<ProtocolClientDiscoveryRecord>(json, ProtocolContract.JsonOptions);
            return record is not null && !string.IsNullOrWhiteSpace(record.PipeName);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            record = null;
            return false;
        }
    }
}
