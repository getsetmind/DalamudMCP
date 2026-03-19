namespace DalamudMCP.Contracts.Bridge;

public sealed record BridgeResponseEnvelope(
    string ContractVersion,
    string RequestId,
    string ResponseType,
    bool Success,
    string? ErrorCode,
    string? ErrorMessage,
    object? Payload);
