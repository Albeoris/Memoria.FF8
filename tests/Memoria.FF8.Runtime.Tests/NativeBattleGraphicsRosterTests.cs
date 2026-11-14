using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGraphicsRosterTests
{
    [Fact]
    public void MaximumRosterRoundTripsOnlyItsRowsWithHolesAndExplicitSides()
    {
        BattleModelOwner?[] rows = new BattleModelOwner?[25];
        rows[0] = new("enemy", false);
        rows[7] = new("guardian", true);
        rows[24] = new("extra", true);
        BattleModelRoster roster = new(rows.ToImmutableArray());

        string json = JsonSerializer.Serialize(roster);
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.Equal(nameof(BattleModelRoster.Rows), Assert.Single(document.RootElement.EnumerateObject()).Name);
        BattleModelRoster restored = JsonSerializer.Deserialize<BattleModelRoster>(json)!;
        Assert.Equal(rows, restored.Rows.ToArray());
        Assert.Equal(new[] { 7, 24 }, restored.PartyRows);
        Assert.Equal(new[] { 0 }, restored.EnemyRows);
    }

    [Fact]
    public void HostedRowsUseTheirExplicitSidesAndSkipUnownedHoles()
    {
        BattleModelOwner?[] rows = new BattleModelOwner?[25];
        rows[0] = new("enemy-in-source-row", IsParty: false);
        rows[2] = new("party", IsParty: true);
        rows[7] = new("guardian-form", IsParty: true);
        rows[9] = new("enemy", IsParty: false);
        rows[24] = new("extra-party", IsParty: true);
        BattleModelRoster roster = new(rows.ToImmutableArray());

        rows[7] = new("replacement", IsParty: false);
        Assert.Equal(25, roster.ActorCount);
        Assert.Equal(new[] { 2, 7, 24 }, roster.PartyRows);
        Assert.Equal(new[] { 0, 9 }, roster.EnemyRows);
        Assert.Equal(new BattleModelOwner("guardian-form", IsParty: true), roster[7]);
        Assert.Null(roster.Rows[3]);
        Assert.Throws<InvalidDataException>(() => roster[3]);
        Assert.Throws<ArgumentOutOfRangeException>(() => roster[-1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => roster[25]);
    }

    [Fact]
    public void IDsAreUniqueAcrossSidesUsingOrdinalComparison()
    {
        BattleModelRoster roster = new([new("Ally", true), null, new("ally", false), null, null, null, null]);
        Assert.Equal(7, roster.ActorCount);
        Assert.Equal(new[] { 0 }, roster.PartyRows);
        Assert.Equal(new[] { 2 }, roster.EnemyRows);
        Assert.Throws<InvalidDataException>(() => new BattleModelRoster(roster.Rows.SetItem(2, new("Ally", false))));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(26)]
    public void RosterRejectsInvalidCapacity(int count)
    {
        Assert.Throws<InvalidDataException>(() => new BattleModelRoster(new BattleModelOwner?[count].ToImmutableArray()));
    }

    [Fact]
    public void RosterRejectsDefaultEmptyAndUnnamedBindings()
    {
        Assert.Throws<InvalidDataException>(() => new BattleModelRoster(default));
        ImmutableArray<BattleModelOwner?> holes = [null, null, null, null, null, null, null];
        Assert.Throws<InvalidDataException>(() => new BattleModelRoster(holes));
        Assert.Throws<InvalidDataException>(() => new BattleModelRoster(holes.SetItem(6, new("", true))));
        Assert.Throws<InvalidDataException>(() => new BattleModelRoster(holes.SetItem(6, new(" ", true))));
    }
}
