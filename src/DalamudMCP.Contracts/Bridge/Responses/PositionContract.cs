namespace DalamudMCP.Contracts.Bridge.Responses;

public sealed record PositionContract(double? X, double? Y, double? Z, string Precision);
