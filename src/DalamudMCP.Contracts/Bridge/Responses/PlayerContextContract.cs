namespace DalamudMCP.Contracts.Bridge.Responses;

public sealed record PlayerContextContract(
    string CharacterName,
    string? HomeWorld,
    string? CurrentWorld,
    int? ClassJobId,
    string? ClassJobName,
    int? Level,
    int? TerritoryId,
    string? TerritoryName,
    int? MapId,
    string? MapName,
    PositionContract? Position,
    bool? InCombat,
    bool? InDuty,
    bool? IsCrafting,
    bool? IsGathering,
    bool? IsMounted,
    bool? IsMoving,
    string? ZoneType,
    string? ContentStatus,
    string SummaryText);
