using DalamudMCP.Domain.Capabilities;

namespace DalamudMCP.Domain.Registry;

public sealed record ResourceBinding(
    CapabilityId CapabilityId,
    string UriTemplate,
    string MimeType,
    string ProviderType,
    bool SupportsSubscription);
