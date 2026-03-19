using DalamudMCP.Domain.Capabilities;

namespace DalamudMCP.Domain.Registry;

public sealed record AddonMetadata(
    string AddonName,
    string DisplayName,
    CapabilityCategory Category,
    SensitivityLevel Sensitivity,
    bool DefaultEnabled,
    bool Denied,
    bool Recommended,
    string Notes,
    IReadOnlyList<string> IntrospectionModes,
    IReadOnlyList<ProfileType> ProfileAvailability);
