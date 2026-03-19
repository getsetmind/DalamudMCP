namespace DalamudMCP.Contracts.Bridge.Responses;

public sealed record TargetObjectResultContract(
    string RequestedGameObjectId,
    bool Succeeded,
    string? Reason,
    string? TargetedGameObjectId,
    string? TargetName,
    string? ObjectKind,
    string SummaryText);
