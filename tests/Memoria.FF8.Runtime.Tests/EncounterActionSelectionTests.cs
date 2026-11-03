using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class EncounterActionSelectionTests
{
    private static readonly ImmutableArray<EncounterSpellChoice> Pool = [new("Firaga", 0, true), new("Blizzaga", 1, true), new("Thundaga", 2, true)];

    [Fact]
    public void DifferentTargetPreferencePreservesUnreflectedOpponentSafety()
    {
        EncounterOffensiveChoice selected = EncounterActionSelection.Select(Pool, [new("direct", false), new("reflected", true)], [], new(), BattleObserverSide.Enemy, true, _ => 0, ["direct"])!;
        Assert.Equal(EncounterOffensiveDelivery.Direct, selected.Delivery);
        Assert.Equal("direct", selected.TargetId);
    }

    [Fact]
    public void Only_the_scanned_allocation_and_observer_can_supply_known_affinities()
    {
        BattleScanKnowledge knowledge = Observe(new(), "party-1", BattleObserverSide.Enemy, 800, 700, 850);
        knowledge = Observe(knowledge, "same-model-new-allocation", BattleObserverSide.Party, 100, 100, 100);
        EncounterOffensiveChoice selected = EncounterActionSelection.Select(Pool, [new("party-1", false), new("same-model-new-allocation", false)], [], knowledge, BattleObserverSide.Enemy, false, count => 0)!;
        Assert.Equal("party-1", selected.TargetId);
        Assert.Equal("Blizzaga", selected.Spell);
        Assert.Null(knowledge.Find(BattleObserverSide.Enemy, "same-model-new-allocation"));
    }

    [Fact]
    public void Partial_resistance_immunity_and_absorption_use_bounded_random_fallback()
    {
        BattleScanKnowledge knowledge = Observe(new(), "party", BattleObserverSide.Enemy, 850, 900, 1000);
        List<int> draws = [];
        EncounterOffensiveChoice selected = EncounterActionSelection.Select(Pool, [new("party", false)], [], knowledge, BattleObserverSide.Enemy, false, count =>
        {
            draws.Add(count);
            return count - 1;
        })!;
        Assert.Equal("Thundaga", selected.Spell);
        Assert.Equal(new[] { 1, 3 }, draws);
        Assert.True(knowledge.Observations[0].Affinities[2].Absorbs);
        Assert.Equal(0m, knowledge.Observations[0].Affinities[2].DamageMultiplier);
    }

    [Fact]
    public void Stable_candidate_order_does_not_depend_on_actor_storage_order()
    {
        EncounterTargetChoice[] targets = [new("z", false), new("a", false)];
        EncounterOffensiveChoice? first = EncounterActionSelection.Select(Pool, targets, [], new(), BattleObserverSide.Enemy, false, count => 0);
        EncounterOffensiveChoice? second = EncounterActionSelection.Select(Pool.Reverse().ToImmutableArray(), targets.Reverse(), [], new(), BattleObserverSide.Enemy, false, count => 0);
        Assert.Equal(first, second);
        Assert.Equal("a", first!.TargetId);
        Assert.Equal("Blizzaga", first.Spell);
    }

    [Fact]
    public void Reflect_policy_excludes_reflected_opponents_before_using_knowledge()
    {
        BattleScanKnowledge knowledge = Observe(new(), "reflected", BattleObserverSide.Enemy, 100, 100, 100);
        EncounterOffensiveChoice result = EncounterActionSelection.Select(Pool, [new("direct", false), new("reflected", true)], [], knowledge, BattleObserverSide.Enemy, true, count => 0)!;
        Assert.Equal("direct", result.TargetId);
        Assert.Equal(EncounterOffensiveDelivery.Direct, result.Delivery);
    }

    [Fact]
    public void All_reflect_uses_an_allied_reflector_then_dispel_without_a_reflector()
    {
        EncounterTargetChoice[] targets = [new("party", true)];
        EncounterOffensiveChoice bounce = EncounterActionSelection.Select(Pool, targets, [new("other-mod-ally", true)], new(), BattleObserverSide.Enemy, true, count => 0)!;
        Assert.Equal(EncounterOffensiveDelivery.AlliedReflector, bounce.Delivery);
        Assert.Equal("other-mod-ally", bounce.TargetId);
        EncounterOffensiveChoice dispel = EncounterActionSelection.Select(Pool, targets, [], new(), BattleObserverSide.Enemy, true, count => 0)!;
        Assert.Equal(new(EncounterOffensiveDelivery.Dispel, "party", "Dispel"), dispel);
        Assert.Null(EncounterActionSelection.Select(Pool, [], [], new(), BattleObserverSide.Enemy, true, count => throw new InvalidOperationException()));
    }

    [Fact]
    public void Knowledge_is_a_snapshot_until_successfully_refreshed_and_serializes()
    {
        BattleScanKnowledge initial = Observe(new(), "party", BattleObserverSide.Enemy, 800, 800, 800);
        BattleScanKnowledge refreshed = Observe(initial, "party", BattleObserverSide.Enemy, 700, 800, 800);
        Assert.Equal(1m, initial.Observations[0].Affinities[0].DamageMultiplier);
        Assert.Equal(2m, refreshed.Observations[0].Affinities[0].DamageMultiplier);
        BattleScanKnowledge restored = JsonSerializer.Deserialize<BattleScanKnowledge>(JsonSerializer.Serialize(refreshed))!;
        restored.Validate();
        Assert.Equal(refreshed.Observations[0].Affinities, restored.Observations[0].Affinities);
    }

    [Fact]
    public void Admitted_turns_do_not_wrap_at_255_and_saturate_at_the_counter_boundary()
    {
        Assert.Equal(257ul, new EncounterActionCursor(255).Admit().NextOwnTurn);
        Assert.Equal(ulong.MaxValue, new EncounterActionCursor(ulong.MaxValue).Admit().NextOwnTurn);
    }

    private static BattleScanKnowledge Observe(BattleScanKnowledge knowledge, string target, BattleObserverSide side, params int[] defenses)
    {
        return knowledge.Observe(new(side, target, "scanner", 1, defenses.Concat(Enumerable.Repeat(800, 8 - defenses.Length)).Select(BattleKnownAffinity.FromNativeDefense).ToImmutableArray()));
    }
}
