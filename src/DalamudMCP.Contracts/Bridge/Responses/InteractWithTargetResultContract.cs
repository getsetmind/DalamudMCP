namespace DalamudMCP.Contracts.Bridge.Responses;

public sealed record InteractWithTargetResultContract(
    string? ExpectedGameObjectId,
    bool Succeeded,
    string? Reason,
    string? InteractedGameObjectId,
    string? TargetName,
    string? ObjectKind,
    double? Distance,
    string SummaryText);
