using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeActionProductionTests
{
    [Fact]
    public void InterpreterObservesAppliedHitBeforeItsNextPredicateAndPresentationDoesNotReplayIt()
    {
        using BattleAssets assets = new(LaunchOptions.DefaultGameDirectory);
        BattleNativeAction hit = assets.NativeActions.First(action => action.Command == 8) with { ActionId = 100, AttackType = 35, Power = 0, HitCount = 1, AttackFlags = 0, DamagePresentation = 0, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Element = 0 };
        //Queue one fixed1HP self hit, then test HP<10000 before setting global96 to77.
        byte[] initializer = [4, 200, 7, 100, 6, 2, 0, 200, 1, 0x10, 0x27, 3, 0, 15, 96, 77, 0];
        NativeBattleAiDefinition ai = new([initializer, [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []);
        BattleSession battle = new(new BattleDefinition
        {
            NativeFieldBattleRules = 0,
            NativeActions = [hit],
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, Speed = 1 },
                new() { Id = "enemy", Name = "Enemy", MaximumHp = 10000, CurrentHp = 10000, Speed = 1, NativeAi = ai, NativeStatusResistances = new byte[40] }
            ]
        }, BattleAtbMode.TurnBased);

        Assert.Equal(10000, battle.Combatants[1].CurrentHp);
        Assert.Equal(new BattleNativeCallback(3, 0), Assert.Single(battle.QueuedActions).NativeCallback);
        battle.Tick();
        BattleSnapshot prepared = battle.Capture();
        Assert.Equal(1, battle.Frame);
        Assert.Equal(9999, battle.Combatants[1].CurrentHp);
        Assert.Equal(77u, prepared.NativeAiVariables![0]);
        Assert.Null(prepared.ActiveAction);
        Assert.Equal(0, Assert.Single(battle.QueuedActions).NativeHeaderIndex);
        Assert.Equal((byte)1, prepared.NativeActionPackets!.ActionCount);
        Assert.Equal((byte)1, prepared.NativeActionPackets.ContactCount);
        Assert.Equal((ushort)1, prepared.NativeActionPackets.ReadContact(0).Primary.Amount6);
        NativeBattleRandomState random = prepared.Random!;
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(prepared))!);

        for (int frame = 0; frame < 30; frame++)
        {
            battle.Tick();
            restored.Tick();
        }

        Assert.Equal(9999, battle.Combatants[1].CurrentHp);
        Assert.Single(battle.Events, item => item.Kind == "Damage");
        Assert.Equal(random, battle.Capture().Random);
        Assert.Equal((byte)0, battle.NativeActionPackets.ActionCount);
        Assert.Equal((byte)0, battle.NativeActionPackets.ContactCount);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Fact]
    public void GunbladePublishesZeroThenRecomputesItsContactOnceAndCompletesVictory()
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = CreatePhysicalBattle(game, assets, true, false);
        battle.SetGunbladeMode(BattleGunbladeMode.Automatic);
        NativeBattleRandomState before = battle.Capture().Random!;
        battle.QueueAttack("party-0", "enemy");
        battle.Tick();

        Assert.Equal(1000, battle.Combatants[1].CurrentHp);
        Assert.Equal((ushort)0, battle.NativeActionPackets.ReadContact(0).Primary.Amount6);
        Assert.Equal(before, battle.Capture().Random);
        Assert.DoesNotContain(battle.Events, item => item.Kind == "Damage");
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        for (int tick = 0; tick < 40 && battle.Outcome == BattleOutcome.Active; tick++)
        {
            battle.Tick();
            restored.Tick();
        }

        Assert.Equal(BattleOutcome.Victory, battle.Outcome);
        Assert.Equal(0, battle.Combatants[1].CurrentHp);
        Assert.False(battle.Capture().NativeActionScratch!.DeferredEffectsE00);
        Assert.Single(battle.Events, item => item.Kind == "Damage");
        Assert.Single(battle.Events, item => item.Kind == "GunbladeTrigger");
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Fact]
    public void PoisonProducesOneSeparateFactualCommandAtCompletionAndE120DoesNotRepeatIt()
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = CreatePhysicalBattle(game, assets, false, true);
        battle.QueueAttack("party-0", "enemy");
        battle.Tick();
        Assert.Equal(1000, battle.Combatants[0].CurrentHp);
        Assert.Equal(999, battle.Combatants[1].CurrentHp);
        for (int tick = 0; tick < 40 && battle.QueuedActions.All(command => command.NativeQueuedCommand != 0); tick++)
        {
            battle.Tick();
        }

        BattleAttackCommand poison = Assert.Single(battle.QueuedActions);
        Assert.Equal((byte)0, poison.NativeQueuedCommand);
        Assert.Equal(2, poison.ActionId);
        Assert.NotNull(poison.NativeHeaderIndex);
        Assert.InRange(battle.Combatants[0].CurrentHp, 910, 950);
        BattleSnapshot prepared = battle.Capture();
        Assert.Equal(0x47E120u, prepared.NativeActionScratch!.ComponentCompletion);
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(prepared))!);
        for (int tick = 0; tick < 40; tick++)
        {
            battle.Tick();
            restored.Tick();
        }

        Assert.Single(battle.Events, item => item.Kind == "Damage" && item.Message.StartsWith("Poison:", StringComparison.Ordinal));
        Assert.Null(battle.Capture().NativeActionScratch!.ComponentCompletion);
        Assert.Equal((byte)0, battle.NativeActionPackets.ActionCount);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Fact]
    public void Ordinary_attack_does_not_invent_a_clear_for_historical_unknown_E03()
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = CreatePhysicalBattle(game, assets, true, false);
        battle.SetGunbladeMode(BattleGunbladeMode.Automatic);
        battle.QueueAttack("party-0", "enemy");
        battle.Tick();
        BattleSnapshot prepared = battle.Capture();
        battle = BattleSession.Restore(prepared with { NativeActionScratch = prepared.NativeActionScratch! with { FinisherPendingE03 = null } });
        NotSupportedException failure = Assert.Throws<NotSupportedException>(() =>
        {
            for (int tick = 0; tick < 20; tick++)
            {
                battle.Tick();
            }
        });
        Assert.Contains("retained E03", failure.Message);
        Assert.Equal(1000, battle.Combatants[1].CurrentHp);
        Assert.Equal(JsonSerializer.Serialize(prepared.Random), JsonSerializer.Serialize(battle.Capture().Random));
        Assert.Null(battle.Capture().NativeActionScratch!.FinisherPendingE03);
        Assert.DoesNotContain(battle.Events, item => item.Kind == "Damage");
    }

    private static BattleSession CreatePhysicalBattle(GameAssets game, BattleAssets assets, bool gunblade, bool poison)
    {
        BattleNativeAction attack = assets.NativeActions.First(action => action.Command == 8) with { Command = 1, ActionId = 0, AttackType = gunblade ? (byte)1 : (byte)35, Power = gunblade ? (byte)150 : (byte)0, HitCount = 1, AttackFlags = 0, DamagePresentation = 4, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Element = 0 };
        return new(new BattleDefinition
        {
            NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(),
            NativeCommandSequences = game.ReadNativeBattleCommandSequences(),
            NativeFieldBattleRules = 0,
            Combatants =
            [
                new() { Id = "party-0", Name = "Party", ModelId = gunblade ? 0 : 1, IsPlayer = true, WeaponId = gunblade ? 0 : null, MaximumHp = 1000, CurrentHp = 1000, InitialAtb = BattleSession.AtbMaximum, Speed = 1, Attack = 255, NativeAttack = attack, NativeStatus0 = poison ? (ushort)2 : (ushort)0, NativeStatusResistances = new byte[40] },
                new() { Id = "enemy", Name = "Enemy", ModelId = 28, MaximumHp = 1000, CurrentHp = 1000, Speed = 1, NativeStatusResistances = new byte[40] }
            ]
        }, BattleAtbMode.TurnBased);
    }
}
