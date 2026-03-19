namespace DalamudMCP.Contracts.Bridge.Responses;

public sealed record MoveToEntityResultContract(
    string RequestedGameObjectId,
    bool Succeeded,
    string? Reason,
    string? ResolvedGameObjectId,
    string? TargetName,
    string? ObjectKind,
    PositionContract? Destination,
    string SummaryText);
