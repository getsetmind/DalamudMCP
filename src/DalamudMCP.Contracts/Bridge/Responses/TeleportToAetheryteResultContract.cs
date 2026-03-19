namespace DalamudMCP.Contracts.Bridge.Responses;

public sealed record TeleportToAetheryteResultContract(
    string RequestedQuery,
    bool Succeeded,
    string? Reason,
    uint? AetheryteId,
    string? AetheryteName,
    string? TerritoryName,
    string SummaryText);
