namespace DalamudMCP.Contracts.Bridge.Responses;

public sealed record StringTableContract(
    string AddonName,
    DateTimeOffset CapturedAt,
    IReadOnlyList<StringTableEntryContract> Entries);
