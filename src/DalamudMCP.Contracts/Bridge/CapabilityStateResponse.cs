namespace DalamudMCP.Contracts.Bridge;

public sealed record CapabilityStateResponse(
    string ContractVersion,
    IReadOnlyList<string> EnabledTools,
    IReadOnlyList<string> EnabledResources,
    IReadOnlyList<string> EnabledAddons,
    bool ObservationProfileEnabled,
    bool ActionProfileEnabled);
