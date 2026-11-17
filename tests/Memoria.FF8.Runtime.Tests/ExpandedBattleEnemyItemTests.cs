using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class ExpandedBattleEnemyItemTests
{
    [Fact]
    public void EnemyItemUsesItsSelectedActorAndLeavesPartyStockIntactAfterRestore()
    {
        BattleNativeAction item = new(4, 3, 7, 3)
        {
            Name = "Hi-Potion", MagicId = 51, AttackType = 4, HitCount = 1, Power = 20, Hit = 255, TargetFlags = 0x14, AttackFlags = 0xE2,
            DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = [], SupplementalRecord = []
        };
        NativeBattleAiDefinition ai = new([[0], [4, 200, 12, 0, 0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(4, 0, 3), 48).ToArray(), 0, []);
        Assert.Equal(ExpandedAiAdmission.Supported, NativeBattleExpandedAi.Evaluate(ai, [item], 0));
        BattleSession battle = new(new()
        {
            Items = [new(3, 2)],
            NativeActions = [item],
            NativeSeed = 0,
            Combatants =
            [
                new() { Id = "ally", Name = "Ally", IsPlayer = true, ModelId = 0, CurrentHp = 900, MaximumHp = 2000, Speed = 1 },
                new() { Id = "enemy", Name = "Enemy", ModelId = 28, CurrentHp = 700, MaximumHp = 2000, Speed = 1, InitialAtb = BattleSession.AtbMaximum, NativeAi = ai }
            ]
        }, BattleAtbMode.Normal);
        battle.Tick();
        Assert.Equal("enemy", battle.ActiveAction!.TargetId);
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);

        for (int tick = 0; tick <= BattleSession.AttackWindupFrames; tick++)
        {
            battle.Tick();
            restored.Tick();
        }

        Assert.Equal(900, battle.Combatants.Single(actor => actor.Id == "ally").CurrentHp);
        Assert.Equal(1700, battle.Combatants.Single(actor => actor.Id == "enemy").CurrentHp);
        Assert.Equal(2, Assert.Single(battle.Items, stock => stock.Id == 3).Count);
        Assert.Single(battle.Events, entry => entry.Kind == "EffectContact" && entry.ActorId == "enemy" && entry.TargetId == "enemy");
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }
}
