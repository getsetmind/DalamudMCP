namespace DalamudMCP.Contracts.Bridge.Responses;

public sealed record InventorySummaryContract(
    int CurrencyGil,
    int OccupiedSlots,
    int TotalSlots,
    IReadOnlyDictionary<string, int> CategoryCounts,
    string SummaryText);
