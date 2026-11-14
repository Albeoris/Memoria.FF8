using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class ExpandedBattleActionCompletionTests
{
    [Theory]
    [InlineData(false, 2, 1, 3125)]
    [InlineData(false, 1, 0, 3100)]
    [InlineData(true, 0, 0, 3125)]
    public void SuccessfulMagicAppliesCompatibilityUnlessItSpentTheLastStock(bool drawCast, int stock, int remaining, int compatibility)
    {
        BattleDefinition definition = CreateDefinition(stock);
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        if (drawCast)
        {
            battle.QueueAction("caster", "source", BattleAbility.Draw, 1, drawCast: true, drawCastTargetId: "target");
        }
        else
        {
            battle.QueueAction("caster", "target", BattleAbility.Magic, 1);
        }

        battle = RoundTrip(battle);
        Complete(battle);

        BattleCombatant caster = battle.Combatants.Single(actor => actor.Id == "caster");
        Assert.Equal(remaining, caster.MagicStocks.Sum(entry => entry.Count));
        Assert.Equal((ushort)compatibility, caster.GuardianForceCompatibility![0]);
        Assert.Contains(battle.Events, entry => entry.Kind == "EffectContact" && entry.TargetId == "target");
    }

    [Fact]
    public void DrawStockRecordsTheOriginalZeroContactAndRunsPostActionPoison()
    {
        BattleDefinition definition = CreateDefinition(0, poisoned: true);
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        battle.QueueAction("caster", "source", BattleAbility.Draw, 1);

        battle = RoundTrip(battle);
        Complete(battle);

        BattleCombatant caster = battle.Combatants.Single(actor => actor.Id == "caster");
        BattleCombatant source = battle.Combatants.Single(actor => actor.Id == "source");
        Assert.NotEmpty(caster.MagicStocks);
        Assert.Equal((ushort)3100, caster.GuardianForceCompatibility![0]);
        Assert.Equal(1000, source.CurrentHp);
        Assert.Equal((byte)0, source.LastNativeAction!.AttackType);
        Assert.Equal((byte)6, source.LastNativeAction.Command);
        Assert.Equal((byte)1, source.LastNativeAction.ActionId);
        Assert.Contains(battle.Events, entry => entry.Kind == "EffectContact" && entry.TargetId == "source" && entry.Amount == 0);
        Assert.Contains(battle.Events, entry => entry.Kind == "Damage" && entry.TargetId == "caster" && entry.Amount > 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExpandedAttackAppliesTheGunbladeTriggerUnlessBlinded(bool blind)
    {
        BattleDefinition definition = CreateDefinition(0);
        BattleCombatantDefinition caster = definition.Combatants[0] with
        {
            ModelId = 0, WeaponId = 0, Attack = 20, NativeStatus0 = blind ? (ushort)8 : (ushort)0,
            NativeAttack = new(1, 0, -1, -1) { Name = "Attack", MagicId = 0, AttackType = 1, Power = 20, HitCount = 1, TargetFlags = 0x50, AttackFlags = 0, DamagePresentation = 4, Element = 0, ElementPercent = 0, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = 255, Critical = 0, PowerModifier = 0, LevelModifier = 0, RawRecord = [], SupplementalRecord = [] }
        };
        BattleSession battle = new(definition with { Combatants = [caster, .. definition.Combatants.Skip(1)] }, BattleAtbMode.TurnBased);
        battle.SetGunbladeMode(BattleGunbladeMode.Manual);
        battle.QueueAttack("caster", "target");
        for (int frame = 0; frame < 20 && !battle.GunbladeTriggerWindowOpen; frame++)
        {
            battle.Tick();
        }
        Assert.True(battle.TriggerGunblade());
        short timing = battle.ActiveAction!.GunbladeTriggerTiming!.Value;
        _ = battle.Capture().Random!.Draw(out byte roll);

        battle = RoundTrip(battle);
        Complete(battle);

        Assert.Equal(!blind, battle.Events.Any(entry => entry.Kind == "GunbladeTrigger" && entry.TargetId == "target"));
        Assert.Equal(NativeGunbladeAttack.Damage(20, 5, 20, roll, blind ? (short)1 : timing), Assert.Single(battle.Events, entry => entry.Kind == "Damage").Amount);
    }

    private static BattleDefinition CreateDefinition(int stock, bool poisoned = false)
    {
        byte[] rawSpell = new byte[60];
        rawSpell[42] = 125;
        BattleNativeAction spell = new(2, 1, 1, 1) { Name = "Fire", MagicId = 2, AttackType = 2, Power = 10, HitCount = 1, TargetFlags = 0x50, AttackFlags = 1, DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = rawSpell.ToImmutableArray(), SupplementalRecord = [] };
        return new()
        {
            UsesExpandedRoster = true,
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            NativeKnownMagic = new(NativeInputConfiguration.OriginalInitHash, [0, 0]),
            NativeActions = [spell],
            ActionDefinitions = [new(BattleAbility.Magic, 1, "Fire", BattleActionEffect.Native, 10, false) { NativeAction = spell }],
            GuardianForces = [new(0, 100, 100) { Acquired = true }],
            Combatants =
            [
                new() { Id = "caster", Name = "Caster", IsPlayer = true, ModelId = 1, Speed = 1, Magic = 100, MaximumHp = 1000, CurrentHp = 1000, NativeStatus0 = poisoned ? (ushort)2 : (ushort)0, InitialAtb = BattleSession.AtbMaximum, Abilities = [BattleAbility.Attack, BattleAbility.Magic, BattleAbility.Draw], NativeMagicInventory = BattleCombatant.ExpandMagicStocks(stock == 0 ? [] : [new BattleStock(1, stock)]), GuardianForceCompatibility = Enumerable.Repeat((ushort)3100, 16).ToArray() },
                new() { Id = "source", Name = "Source", ModelId = 2, Speed = 1, MaximumHp = 1000, CurrentHp = 1000, NativeDrawSlots = [new(1, 0), new(0, 0), new(0, 0), new(0, 0)] },
                new() { Id = "target", Name = "Target", ModelId = 3, Speed = 1, MaximumHp = 1000, CurrentHp = 1000, Defense = 5 }
            ]
        };
    }

    private static BattleSession RoundTrip(BattleSession battle)
    {
        return BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
    }

    private static void Complete(BattleSession battle)
    {
        for (int frame = 0; frame < 120 && !battle.Events.Any(entry => entry.Kind == "ActionComplete"); frame++)
        {
            battle.Tick();
        }
        Assert.Single(battle.Events, entry => entry.Kind == "ActionComplete");
    }
}
