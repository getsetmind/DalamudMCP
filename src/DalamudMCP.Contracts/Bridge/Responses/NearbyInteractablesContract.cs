namespace DalamudMCP.Contracts.Bridge.Responses;

public sealed record NearbyInteractablesContract(
    double MaxDistance,
    IReadOnlyList<NearbyInteractableContract> Interactables,
    string SummaryText);
