namespace DalamudMCP.Contracts.Bridge.Responses;

public sealed record AddonTreeContract(
    string AddonName,
    DateTimeOffset CapturedAt,
    IReadOnlyList<NodeContract> Roots);
