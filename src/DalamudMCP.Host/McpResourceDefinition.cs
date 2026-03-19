namespace DalamudMCP.Host;

public sealed record McpResourceDefinition(
    string UriTemplate,
    string CapabilityId,
    string MimeType,
    string ProviderType,
    bool SupportsSubscription,
    string DisplayName,
    string Description);
