namespace DalamudMCP.Contracts.Bridge.Responses;

public sealed record SessionComponentContract(
    string ComponentName,
    bool IsReady,
    string Status);
