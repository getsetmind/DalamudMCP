namespace DalamudMCP.Contracts.Bridge.Responses;

public sealed record SessionStateContract(
    string PipeName,
    bool IsBridgeServerRunning,
    int ReadyComponentCount,
    int TotalComponentCount,
    IReadOnlyList<SessionComponentContract> Components,
    string SummaryText);
