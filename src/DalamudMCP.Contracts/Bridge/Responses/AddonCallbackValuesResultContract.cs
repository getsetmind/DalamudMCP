namespace DalamudMCP.Contracts.Bridge.Responses;

public sealed record AddonCallbackValuesResultContract(
    string AddonName,
    int[] Values,
    bool Succeeded,
    string? Reason,
    string SummaryText);
