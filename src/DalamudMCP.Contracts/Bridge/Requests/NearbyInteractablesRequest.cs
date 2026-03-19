namespace DalamudMCP.Contracts.Bridge.Requests;

public sealed record NearbyInteractablesRequest(
    double? MaxDistance,
    string? NameContains,
    bool IncludePlayers = false);
