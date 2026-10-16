using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeMagicGroupTests
{
    [Theory]
    [InlineData(0x20000, 2, 0, 2)]
    [InlineData(0x40000, 3, 0, 3)]
    [InlineData(0x20000, 2, 0x20, 1)]
    [InlineData(0x40000, 3, 0x40, 1)]
    [InlineData(0x60000, 2, 0x40, 2)]
    public void OriginalRepeatedMagicKeepsOneSpellWithDistinctTargetMasksAndExactStockCost(uint status, int count, uint passive, int cost)
    {
        BattleSession battle = Create(status, passive, cost);
        battle.QueueAction("caster", "enemy", BattleAbility.Magic, 1, magicTargets: Enumerable.Repeat("enemy", count).ToArray());
        Assert.Equal(count, Assert.Single(battle.QueuedActions).NativeMagicTargets.Length);
        battle.Tick();
        BattleSession restored = BattleSession.Restore(battle.Capture());
        Complete(battle);
        Complete(restored);
        Assert.Equal(count, battle.Events.Count(item => item.Kind == "Damage" && item.TargetId == "enemy"));
        Assert.Empty(battle.Combatants[0].MagicStocks);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Fact]
    public void LosingTripleBeforeDispatchExecutesOnlyTheFirstSelectedTarget()
    {
        BattleSession battle = Create(0x40000, 0, 3);
        battle.QueueAction("caster", "enemy", BattleAbility.Magic, 1, magicTargets: ["enemy", "enemy", "enemy"]);
        BattleSnapshot queued = battle.Capture();
        BattleCombatantSnapshot[] actors = queued.Combatants.ToArray();
        actors[0] = actors[0] with { NativeStatus = actors[0].NativeStatus! with { Battle = 0 } };
        battle = BattleSession.Restore(queued with { Combatants = actors });
        Complete(battle);
        Assert.Single(battle.Events, item => item.Kind == "Damage" && item.TargetId == "enemy");
        Assert.Equal(2, Assert.Single(battle.Combatants[0].MagicStocks).Count);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void StockLostAfterQueueingStopsTheGroupAtTheLastAvailableSpell(int remaining)
    {
        BattleSession battle = Create(0x40000, 0, 3);
        battle.QueueAction("caster", "enemy", BattleAbility.Magic, 1, magicTargets: ["enemy", "enemy", "enemy"]);
        BattleSnapshot queued = battle.Capture();
        BattleCombatantSnapshot[] actors = queued.Combatants.ToArray();
        NativeMagicStock[] stocks = actors[0].NativeMagicInventory!.ToArray();
        stocks[0] = stocks[0] with { Count = checked((byte)remaining) };
        actors[0] = actors[0] with { NativeMagicInventory = stocks };
        battle = BattleSession.Restore(queued with { Combatants = actors });
        battle.Tick();
        BattleSession restored = BattleSession.Restore(battle.Capture());
        Complete(battle);
        Complete(restored);
        Assert.Equal(remaining, battle.Events.Count(item => item.Kind == "Damage" && item.TargetId == "enemy"));
        Assert.Empty(battle.Combatants[0].MagicStocks);
        Assert.Equal(new NativeMagicStock(0, 0), battle.Combatants[0].NativeMagicSlots[0]);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Fact]
    public void RemainingCastsWithoutAnyTargetsDoNotSpendAdditionalStock()
    {
        BattleSession battle = Create(0x40000, 0, 3, 1);
        battle.QueueAction("caster", "enemy", BattleAbility.Magic, 1, magicTargets: ["enemy", "enemy", "enemy"]);
        Complete(battle);
        Assert.Equal(BattleOutcome.Victory, battle.Outcome);
        Assert.Equal(2, Assert.Single(battle.Combatants[0].MagicStocks).Count);
    }

    [Theory]
    [InlineData(3, 0, 2)]
    [InlineData(1, 0x40, 3)]
    public void ExhaustingOrdinaryStockSkipsAffinityButDeferredSingleCostKeepsEveryCast(int stock, uint passive, int affinityUpdates)
    {
        BattleSession battle = Create(0x40000, passive, stock, includeAffinity: true);
        BattleNativeAction spell = battle.Capture().Definition.ActionDefinitions.Single(action => action.Ability == BattleAbility.Magic && action.Id == 1).NativeAction!;
        int gf = Enumerable.Range(0, 16).First(id => spell.RawRecord[42 + id] != 100);
        int delta = spell.RawRecord[42 + gf] - 100;
        battle.QueueAction("caster", "enemy", BattleAbility.Magic, 1, magicTargets: ["enemy", "enemy", "enemy"]);
        Complete(battle);
        Assert.Equal(3, battle.Events.Count(item => item.Kind == "Damage" && item.TargetId == "enemy"));
        Assert.Empty(battle.Combatants[0].MagicStocks);
        Assert.Equal(3000 + affinityUpdates * delta, battle.Capture().Combatants[0].GuardianForceCompatibility![gf]);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    public void SingleCastSkipsAffinityOnlyWhenItsDebitExhaustsStock(int stock, int affinityUpdates)
    {
        BattleSession battle = Create(0, 0, stock, includeAffinity: true);
        BattleNativeAction spell = battle.Capture().Definition.ActionDefinitions.Single(action => action.Ability == BattleAbility.Magic && action.Id == 1).NativeAction!;
        int gf = Enumerable.Range(0, 16).First(id => spell.RawRecord[42 + id] != 100);
        int delta = spell.RawRecord[42 + gf] - 100;
        battle.QueueAction("caster", "enemy", BattleAbility.Magic, 1);
        battle.Tick();
        BattleSession restored = BattleSession.Restore(battle.Capture());
        Complete(battle);
        Complete(restored);
        Assert.Single(battle.Events, item => item.Kind == "Damage" && item.TargetId == "enemy");
        Assert.Equal(stock - 1, battle.Combatants[0].MagicStocks.Sum(entry => entry.Count));
        Assert.Equal(3000 + affinityUpdates * delta, battle.Capture().Combatants[0].GuardianForceCompatibility![gf]);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Fact]
    public void RepeatedCastsCoalesceAiCountersAndCommitPoisonOnlyOnce()
    {
        BattleSession battle = Create(0x40000, 0, 3);
        BattleSnapshot saved = battle.Capture();
        NativeBattleAiDefinition ai = new([[0], [0], [15, 96, 1, 0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []);
        BattleCombatantDefinition[] definitions = saved.Definition.Combatants.ToArray();
        definitions[1] = definitions[1] with { NativeAi = ai };
        BattleCombatantSnapshot[] actors = saved.Combatants.ToArray();
        actors[0] = actors[0] with { NativeStatus = actors[0].NativeStatus! with { Saved = (ushort)(actors[0].NativeStatus!.Saved | 2) } };
        battle = BattleSession.Restore(saved with { Definition = saved.Definition with { Combatants = definitions }, Combatants = actors, NativeAiVariables = new uint[8] });
        battle.QueueAction("caster", "enemy", BattleAbility.Magic, 1, magicTargets: ["enemy", "enemy", "enemy"]);
        battle.Tick();
        Assert.Equal(3, battle.Events.Count(item => item.Kind == "Damage" && item.TargetId == "enemy"));
        Assert.Equal(0u, battle.Capture().NativeAiVariables![0]);
        Assert.Equal(400, battle.Combatants[0].CurrentHp);
        Assert.DoesNotContain(battle.Events, item => item.Message.StartsWith("Poison:", StringComparison.Ordinal));
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        Complete(battle);
        Complete(restored);
        Assert.Equal(1u, battle.Capture().NativeAiVariables![0]);
        Assert.Equal(3, battle.Events.Count(item => item.Kind == "Damage" && item.TargetId == "enemy"));
        Assert.Single(battle.Events, item => item.Kind == "Damage" && item.TargetId == "caster" && item.Message.StartsWith("Poison:", StringComparison.Ordinal));
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        string completed = JsonSerializer.Serialize(battle.Capture());
        Complete(battle);
        Assert.Equal(completed, JsonSerializer.Serialize(battle.Capture()));
    }

    [Fact]
    public void TripleWithoutDoubleDoesNotInventATwoCastCommand()
    {
        BattleSession battle = Create(0x40000, 0x40, 1);
        Assert.Equal(new[] { 1, 3 }, battle.GetNativeMagicCastCounts("caster", 1));
        string before = JsonSerializer.Serialize(battle.Capture());
        Assert.Throws<InvalidOperationException>(() => battle.QueueAction("caster", "enemy", BattleAbility.Magic, 1, magicTargets: ["enemy", "enemy"]));
        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
    }

    private static void Complete(BattleSession battle)
    {
        //Three cast presentations, one post-command poison packet and a later callback2 are separate admissions.
        for (int frame = 0; frame < 100 && (battle.ActiveAction is not null || battle.QueuedActions.Count != 0); frame++)
        {
            battle.Tick();
        }
        Assert.Null(battle.ActiveAction);
        Assert.Empty(battle.QueuedActions);
    }

    private static BattleSession Create(uint status, uint passive, int stock, int enemyHp = 10000, bool includeAffinity = false)
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals();
        NativeCharacterVital character = vitals.Records[0];
        BattleNativePlayerStatRules rules = assets.ReadPlayerNativeStatRules(vitals, 0) with { NativeStatusPassiveFlags = passive };
        return new(new BattleDefinition
        {
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            NativeActions = assets.NativeActions.ToArray(),
            ActionDefinitions = assets.ActionDefinitions.ToArray(),
            NativeStatusRules = assets.ReadNativeStatusRules(0),
            GuardianForces = includeAffinity ? Enumerable.Range(0, 16).Select(id => new BattleGuardianForceVital(id, 1000, 1000)).ToArray() : [],
            Combatants =
            [
                new() { Id = "caster", Name = "Caster", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, MaximumHp = 486, CurrentHp = 400, Magic = assets.ReadPlayerStats(vitals, 0).Magic, NativeStatus1 = status, NativeDeathCount = 0, Abilities = [BattleAbility.Magic], MagicStocks = [new(1, stock)], NativePlayerStatRules = rules, NativeGrowth = new(character.HitPointBonus, character.StatBonuses.ToImmutableArray()), NativeStatusResistances = new byte[40], GuardianForceCompatibility = includeAffinity ? Enumerable.Repeat((ushort)3000, 16).ToArray() : null },
                new() { Id = "enemy", Name = "Enemy", MaximumHp = enemyHp, CurrentHp = enemyHp, Speed = 1, NativeStatusResistances = new byte[40] }
            ]
        }, BattleAtbMode.TurnBased);
    }
}
