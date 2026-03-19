namespace DalamudMCP.Contracts.Bridge.Responses;

public sealed record NodeContract(
    int NodeId,
    string NodeType,
    bool Visible,
    float X,
    float Y,
    float Width,
    float Height,
    string? Text,
    IReadOnlyList<NodeContract> Children);
