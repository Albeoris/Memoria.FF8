namespace Memoria.FF8.Runtime.Tests;

public sealed class BattleModelRosterTests
{
    [Fact]
    public void RosterRetainsAnImmutableOwnerOrderAndExplicitSides()
    {
        BattleEnemyActorModel enemy = new(new("enemy"));
        BattlePartyActorModel party = new(new("party"));
        BattlePartyActorModel form = new(new("guardian-form"));
        BattleEnemyActorModel other = new(new("other-enemy"));
        BattlePartyActorModel extra = new(new("extra-party"));
        BattleModelOwner[] owners = [enemy, party, form, other, extra];
        BattleModelRoster roster = new(owners);

        owners[2] = new BattleEnemyActorModel(new("replacement"));
        Assert.Equal(5, roster.Count);
        Assert.Equal(new BattleModelOwner[] { enemy, party, form, other, extra }, roster.Owners);
        Assert.Equal(new[] { party, form, extra }, roster.Party);
        Assert.Equal(new[] { enemy, other }, roster.Enemies);
        Assert.True(roster.Contains(form));
        Assert.False(roster.Contains(owners[2]));
    }

    [Fact]
    public void CanonicalIdsAreUniqueAcrossSidesUsingOrdinalComparison()
    {
        BattlePartyActorModel ally = new(new("Ally"));
        BattleEnemyActorModel enemy = new(new("ally"));
        BattleModelRoster roster = new([ally, enemy]);
        Assert.Equal(2, roster.Count);
        Assert.Equal(ally, Assert.Single(roster.Party));
        Assert.Equal(enemy, Assert.Single(roster.Enemies));
        Assert.Throws<ArgumentException>(() => new BattleModelRoster([ally, new BattleEnemyActorModel(new("Ally"))]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(26)]
    public void RosterExtentFollowsTheActualOwners(int count)
    {
        BattleModelOwner[] owners = Enumerable.Range(0, count).Select(index => (BattleModelOwner)new BattlePartyActorModel(new($"ally-{index}"))).ToArray();
        BattleModelRoster roster = new(owners);
        Assert.Equal(count, roster.Count);
        Assert.Equal(owners, roster.Owners);
        Assert.Empty(roster.Enemies);
    }

    [Fact]
    public void RosterAndActorKeysRejectNullAndUnnamedOwners()
    {
        Assert.Throws<ArgumentNullException>(() => new BattleModelRoster(null!));
        Assert.Throws<ArgumentNullException>(() => new BattleModelRoster([null!]));
        Assert.Throws<ArgumentException>(() => new BattlePartyActorReference(""));
        Assert.Throws<ArgumentException>(() => new BattlePartyActorReference(" "));
    }
}
