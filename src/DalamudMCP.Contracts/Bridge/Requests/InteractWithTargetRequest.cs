namespace DalamudMCP.Contracts.Bridge.Requests;

public sealed record InteractWithTargetRequest(string? ExpectedGameObjectId, bool? CheckLineOfSight);
