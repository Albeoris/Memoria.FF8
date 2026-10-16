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

        BattleSnapshot prepared = battle.Capture();
        Assert.Equal(0, battle.Frame);
        Assert.Equal(9999, battle.Combatants[1].CurrentHp);
        Assert.Equal(77u, prepared.NativeAiVariables![0]);
        Assert.Equal(0, Assert.Single(prepared.QueuedActions).NativeHeaderIndex);
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
}
