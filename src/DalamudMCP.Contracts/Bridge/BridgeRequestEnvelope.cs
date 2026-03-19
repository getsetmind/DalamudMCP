namespace DalamudMCP.Contracts.Bridge;

public sealed record BridgeRequestEnvelope(
    string ContractVersion,
    string RequestType,
    string RequestId,
    object? Payload);
