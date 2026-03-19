namespace DalamudMCP.Contracts.Bridge.Responses;

public sealed record NearbyInteractableContract(
    string GameObjectId,
    string Name,
    string ObjectKind,
    bool IsTargetable,
    double Distance,
    double HitboxRadius,
    PositionContract? Position);
