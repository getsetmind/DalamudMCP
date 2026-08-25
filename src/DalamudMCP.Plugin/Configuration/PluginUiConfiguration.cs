using System.Security.Cryptography;
using Dalamud.Configuration;

namespace DalamudMCP.Plugin.Configuration;

public sealed class PluginUiConfiguration : IPluginConfiguration
{
    public int Version { get; set; } = 5;

    public bool AutoStartHttpServerOnLoad { get; set; }

    public bool EnableActionOperations { get; set; }

    public bool EnableUnsafeOperations { get; set; }

    public Dictionary<string, CapabilityPolicyConfiguration> CapabilityPolicies { get; } = new(StringComparer.Ordinal);

    public string HttpBearerToken { get; set; } = CreateBearerToken();

    public string SelectedLanguage { get; set; } = "en";

    internal static string CreateBearerToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
}

public enum CapabilityAccessMode
{
    Deny = 0,
    Confirm = 1,
    Allow = 2,
}

public sealed class CapabilityPolicyConfiguration
{
    public CapabilityAccessMode Access { get; set; } = CapabilityAccessMode.Deny;

    public string[] AllowedPluginNames { get; set; } = ["*"];

    public string[] AllowedActionTypes { get; set; } = [];

    public long[] AllowedActionIds { get; set; } = [];

    public uint[] AllowedClassJobIds { get; set; } = [];

    public uint[] AllowedTerritoryIds { get; set; } = [];

    public string[] AllowedSheetNames { get; set; } = [];

    public string[] AllowedCallgates { get; set; } = [];

    public string[] AllowedTargetObjectIds { get; set; } = [];

    public int MaximumResultCount { get; set; }

    public int MaximumCallsPerMinute { get; set; }
}
