namespace DalamudMCP.Contracts.Bridge.Requests;

public sealed record AddonCallbackValuesRequest(
    string AddonName,
    int[] Values);
