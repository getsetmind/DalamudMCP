namespace DalamudMCP.Contracts.Bridge.Responses;

public sealed record StringTableEntryContract(int Index, string? RawValue, string? DecodedValue);
