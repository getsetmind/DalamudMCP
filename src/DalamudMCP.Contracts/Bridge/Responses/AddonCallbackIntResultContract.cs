namespace DalamudMCP.Contracts.Bridge.Responses;

public sealed record AddonCallbackIntResultContract(
    string AddonName,
    int Value,
    bool Succeeded,
    string? Reason,
    string SummaryText);
