namespace DalamudMCP.Contracts.Bridge.Requests;

public sealed record AddonCallbackIntRequest(
    string AddonName,
    int Value);
