using DalamudMCP.Domain.Snapshots;

namespace DalamudMCP.Domain.Tests.Snapshots;

public sealed class SnapshotSmokeTests
{
    [Fact]
    public void PlayerContextSnapshot_RetainsValues()
    {
        var snapshot = new PlayerContextSnapshot(
            CapturedAt: DateTimeOffset.UtcNow,
            CharacterName: "Alice",
            HomeWorld: "Tonberry",
            CurrentWorld: "Tonberry",
            ClassJobId: 24,
            ClassJobName: "White Mage",
            Level: 100,
            TerritoryId: 1,
            TerritoryName: "Limsa Lominsa",
            MapId: 2,
            MapName: "Lower Decks",
            Position: new PositionSnapshot(1, 2, 3, "coarse"),
            InCombat: false,
            InDuty: false,
            IsCrafting: false,
            IsGathering: false,
            IsMounted: false,
            IsMoving: false,
            ZoneType: "city",
            ContentStatus: "idle",
            SummaryText: "Lv100 White Mage in Limsa.");

        Assert.Equal("Alice", snapshot.CharacterName);
        Assert.Equal("coarse", snapshot.Position?.Precision);
    }
}
