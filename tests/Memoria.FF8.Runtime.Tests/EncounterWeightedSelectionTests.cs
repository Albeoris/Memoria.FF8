using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class EncounterWeightedSelectionTests
{
    private static readonly ImmutableArray<EncounterSpellChoice> Pool = [new("Fire", 0, true, 3), new("Blizzard", 1, true, 1)];

    [Fact]
    public void UnknownAffinityFallbackUsesStableWeightedSpellIntervals()
    {
        List<int> draws = [];
        EncounterOffensiveChoice first = EncounterActionSelection.Select(Pool, [new("party", false)], [], new(), BattleObserverSide.Enemy, false, count =>
        {
            draws.Add(count);
            return count == 1 ? 0 : 2;
        })!;
        EncounterOffensiveChoice second = EncounterActionSelection.Select(Pool.Reverse().ToImmutableArray(), [new("party", false)], [], new(), BattleObserverSide.Enemy, false, count => count == 1 ? 0 : 2)!;
        Assert.Equal("Fire", first.Spell);
        Assert.Equal(first, second);
        Assert.Equal(new[] { 1, 4 }, draws);
    }

    [Fact]
    public void KnownEqualStrongestPairsStillUseUniformTieSelection()
    {
        BattleScanKnowledge knowledge = new BattleScanKnowledge().Observe(new(BattleObserverSide.Enemy, "party", "observer", 1, Enumerable.Repeat(BattleKnownAffinity.FromNativeDefense(800), 8).ToImmutableArray()));
        List<int> draws = [];
        EncounterOffensiveChoice result = EncounterActionSelection.Select(Pool, [new("party", false)], [], knowledge, BattleObserverSide.Enemy, false, count =>
        {
            draws.Add(count);
            return 0;
        })!;
        Assert.Equal("Blizzard", result.Spell);
        Assert.Equal(new[] { 2 }, draws);
    }

    [Fact]
    public void ExplicitAllReflectBounceRemainsUniformRegardlessOfOffensiveWeights()
    {
        List<int> draws = [];
        EncounterOffensiveChoice result = EncounterActionSelection.Select(Pool, [new("party", true)], [new("ally", true)], new(), BattleObserverSide.Enemy, true, count =>
        {
            draws.Add(count);
            return 0;
        })!;
        Assert.Equal(EncounterOffensiveDelivery.AlliedReflector, result.Delivery);
        Assert.Equal("Blizzard", result.Spell);
        Assert.Equal(new[] { 1, 2 }, draws);
    }
}
