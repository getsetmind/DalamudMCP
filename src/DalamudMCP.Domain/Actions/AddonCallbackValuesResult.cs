namespace DalamudMCP.Domain.Actions;

public sealed record AddonCallbackValuesResult(
    string AddonName,
    IReadOnlyList<int> Values,
    bool Succeeded,
    string? Reason,
    string SummaryText);
