namespace DalamudMCP.Domain.Actions;

public sealed record AddonCallbackIntResult(
    string AddonName,
    int Value,
    bool Succeeded,
    string? Reason,
    string SummaryText);
