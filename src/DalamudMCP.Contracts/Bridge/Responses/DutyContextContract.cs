namespace DalamudMCP.Contracts.Bridge.Responses;

public sealed record DutyContextContract(
    int? TerritoryId,
    string? DutyName,
    string? DutyType,
    bool InDuty,
    bool IsDutyComplete,
    string SummaryText);
