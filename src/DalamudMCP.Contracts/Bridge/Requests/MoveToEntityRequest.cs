namespace DalamudMCP.Contracts.Bridge.Requests;

public sealed record MoveToEntityRequest(string GameObjectId, bool? AllowFlight);
