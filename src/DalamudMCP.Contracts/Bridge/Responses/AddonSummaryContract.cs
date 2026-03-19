namespace DalamudMCP.Contracts.Bridge.Responses;

public sealed record AddonSummaryContract(
    string AddonName,
    bool IsReady,
    bool IsVisible,
    DateTimeOffset CapturedAt,
    string SummaryText);
