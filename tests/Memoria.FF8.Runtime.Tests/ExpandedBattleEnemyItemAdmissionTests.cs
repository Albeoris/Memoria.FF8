using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class ExpandedBattleEnemyItemAdmissionTests
{
    private const ushort Silence = 0x10;

    [Theory]
    [InlineData(1, 50, 4, 0, 0x00, 0x0000, 900, 0x10)]
    [InlineData(3, 51, 20, 0, 0x00, 0x0000, 1700, 0x10)]
    [InlineData(16, 67, 0, 200, 0x7E, 0x5201, 700, 0x00)]
    public void ItemRecordTargetsTheExtraAllyAndPreservesInventoryAfterRestore(ushort id, ushort magicId, byte power, byte statusAttack, ushort statuses0, uint statuses1, int expectedHp, ushort expectedStatus)
    {
        BattleNativeAction item = new(4, id, 7, id)
        {
            MagicId = magicId, AttackType = 4, Power = power, HitCount = 1, TargetFlags = 0x14, AttackFlags = 0xE2,
            DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = statusAttack,
            Statuses0 = statuses0, Statuses1 = statuses1, Hit = 255, Critical = null, PowerModifier = null, LevelModifier = null,
            RawRecord = [], SupplementalRecord = []
        };
        NativeBattleAiDefinition ai = new([[0], [4, 3, 12, 0, 0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(4, 0, id), 48).ToArray(), 0, []);
        Assert.Equal(ExpandedAiAdmission.Supported, NativeBattleExpandedAi.Evaluate(ai, [item], 0));
        Assert.Equal(ExpandedAiAdmission.RequiresUnsupportedService, NativeBattleExpandedAi.Evaluate(ai, [item with { KernelSection = 2 }], 0));

        BattleCombatantDefinition[] allies = Enumerable.Range(0, 4).Select(model => new BattleCombatantDefinition
        {
            Id = $"ally-{model}", Name = $"Ally {model}", IsPlayer = true, ModelId = model, CurrentHp = 700, MaximumHp = 2000, Speed = 1, NativeStatus0 = Silence
        }).ToArray();
        BattleSession battle = new(new()
        {
            UsesExpandedRoster = true,
            Items = [new(id, 2)],
            NativeActions = [item],
            NativeSeed = 0,
            Combatants = [.. allies, new() { Id = "enemy", Name = "Enemy", ModelId = 28, CurrentHp = 700, MaximumHp = 2000, Speed = 1, InitialAtb = BattleSession.AtbMaximum, NativeAi = ai }]
        }, BattleAtbMode.Normal);

        NativeItemStock[] inventory = battle.Capture().NativeItemInventory!;
        battle.Tick();
        BattleActionPresentation active = Assert.IsType<BattleActionPresentation>(battle.ActiveAction);
        Assert.Equal("ally-3", active.TargetId);
        Assert.Equal(new BattlePartyActorReference("ally-3"), Assert.Single(Assert.IsType<BattleTargetSelection.Explicit>(active.TargetRequest.Selection).Actors));
        BattleSnapshot midpoint = battle.Capture();
        BattleSession restored = BattleSession.Restore(midpoint);
        Assert.Equal(JsonSerializer.Serialize(midpoint), JsonSerializer.Serialize(restored.Capture()));

        for (int tick = 0; tick <= BattleSession.AttackWindupFrames; tick++)
        {
            battle.Tick();
            restored.Tick();
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        }

        BattleCombatant target = battle.Combatants.Single(actor => actor.Id == "ally-3");
        Assert.Equal(expectedHp, target.CurrentHp);
        Assert.Equal(expectedStatus, (ushort)(target.NativeStatus0 & Silence));
        Assert.All(battle.Combatants.Where(actor => actor.Id != target.Id), actor => Assert.Equal(700, actor.CurrentHp));
        Assert.All(battle.Combatants.Where(actor => actor.IsPlayer && actor.Id != target.Id), actor => Assert.Equal(Silence, (ushort)(actor.NativeStatus0 & Silence)));
        Assert.Equal(inventory, battle.Capture().NativeItemInventory);
        Assert.Equal(2, Assert.Single(battle.Items, stock => stock.Id == id).Count);
        Assert.Single(battle.Events, entry => entry.Kind == "EffectContact" && entry.ActorId == "enemy" && entry.TargetId == target.Id);
    }
}
