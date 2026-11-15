using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class ExpandedBattleActionCompletionTests
{
    [Theory]
    [InlineData(0x20000u, 2, 2, 0, 3125)]
    [InlineData(0x40000u, 2, 2, 0, 3125)]
    [InlineData(0x40000u, 3, 4, 1, 3175)]
    public void RepeatMagicRetainsLogicalTargetsAndAppliesPoisonOnce(uint status, int casts, int initialStock, int stockAfter, int affinity)
    {
        BattleSession battle = CreateMulticast(status, initialStock, poison: true);
        string[] targets = ["enemy-10", "enemy-7", "enemy-10"];
        battle.QueueAction("caster", targets[0], BattleAbility.Magic, 1, magicTargets: targets[..casts]);
        Assert.Equal(targets[..casts], Assert.Single(battle.QueuedActions).MagicCasts.Select(cast => Assert.Single(Assert.IsType<BattleTargetSelection.Explicit>(cast.Targets.Selection).Actors).ActorId));
        battle = RoundTrip(battle);
        battle.Tick();
        battle = RoundTrip(battle);
        Complete(battle);

        Assert.Equal(targets[..casts], battle.Events.Where(entry => entry.Kind == "EffectContact").Select(entry => entry.TargetId));
        BattleCombatant caster = battle.Combatants.Single(actor => actor.Id == "caster");
        Assert.Equal(stockAfter, caster.MagicStocks.Sum(entry => entry.Count));
        Assert.Equal((ushort)affinity, caster.GuardianForceCompatibility![0]);
        Assert.Single(battle.Events, entry => entry.Kind == "Damage" && entry.TargetId == caster.Id);
    }

    [Theory]
    [InlineData(0u, false, 1, 3)]
    [InlineData(0x20000u, false, 2, 2)]
    [InlineData(0x20000u, true, 0, 4)]
    public void RepeatMagicRevalidatesStatusBeforeSpendingReservedStock(uint remainingStatus, bool silenced, int performed, int stock)
    {
        BattleSession battle = CreateMulticast(0x40000, 4, useLedger: true);
        battle.QueueAction("caster", "enemy-10", BattleAbility.Magic, 1, magicTargets: ["enemy-10", "enemy-7", "target"]);
        Assert.Equal(3, Assert.Single(battle.MagicVitals!.MagicSystem.Reservations).Count);
        battle.Tick();
        BattleCombatant caster = battle.Combatants.Single(actor => actor.Id == "caster");
        caster.NativeStatus1 = remainingStatus;
        if (silenced)
        {
            caster.NativeStatus0 |= 0x10;
        }
        Complete(battle);
        Assert.Equal(performed, battle.Events.Count(entry => entry.Kind == "EffectContact"));
        Assert.Equal(stock, caster.MagicStocks.Sum(entry => entry.Count));
        Assert.Empty(battle.MagicVitals!.MagicSystem.Reservations);
    }

    [Theory]
    [InlineData(0x20000u, 0x20u, 2)]
    [InlineData(0x40000u, 0x40u, 3)]
    public void SingleStockPassiveDebitsOneAfterEveryRepeat(uint status, uint passive, int casts)
    {
        BattleSession battle = CreateMulticast(status, 1, passive: passive, useLedger: true);
        string[] targets = ["enemy-10", "enemy-7", "target"];
        battle.QueueAction("caster", targets[0], BattleAbility.Magic, 1, magicTargets: targets[..casts]);
        Assert.Equal(1, Assert.Single(battle.MagicVitals!.MagicSystem.Reservations).Count);
        Complete(battle);
        Assert.Equal(casts, battle.Events.Count(entry => entry.Kind == "EffectContact"));
        BattleCombatant caster = battle.Combatants.Single(actor => actor.Id == "caster");
        Assert.Empty(caster.MagicStocks);
        Assert.Equal((ushort)(3100 + 25 * casts), caster.GuardianForceCompatibility![0]);
        Assert.Empty(battle.MagicVitals!.MagicSystem.Reservations);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(0x20u)]
    public void UnavailableSecondTargetFinishesGroupAndConsumesOnlyItsFirstCast(uint passive)
    {
        BattleSession battle = CreateMulticast(0x20000, 2, passive: passive, useLedger: true);
        BattleCombatant first = battle.Combatants.Single(actor => actor.Id == "enemy-10");
        BattleCombatant untargetable = battle.Combatants.Single(actor => actor.Id == "enemy-7");
        battle.QueueAction("caster", first.Id, BattleAbility.Magic, 1, magicTargets: [first.Id, untargetable.Id]);
        battle.Tick();
        foreach (BattleCombatant enemy in battle.Combatants.Where(actor => !actor.IsPlayer && actor != first))
        {
            enemy.Untargetable = true;
        }
        // Keep one living enemy in the outcome roster and make both selected slots unavailable after the first contact.
        first.CurrentHp = 1;
        Complete(battle);

        Assert.Equal(first.Id, Assert.Single(battle.Events, entry => entry.Kind == "EffectContact").TargetId);
        Assert.Equal(1, battle.Combatants.Single(actor => actor.Id == "caster").MagicStocks.Sum(stock => stock.Count));
        Assert.Empty(battle.MagicVitals!.MagicSystem.Reservations);
    }

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

    private static BattleSession CreateMulticast(uint status, int stock, bool poison = false, uint passive = 0, bool useLedger = false)
    {
        BattleDefinition definition = CreateDefinition(stock, poison);
        BattleCombatantDefinition caster = definition.Combatants[0] with { NativeStatus1 = status };
        if (useLedger)
        {
            NativeCharacterVitals vitals = MagicSystemFixture.Create().ChangeMagicStock(1, 1, stock, MagicOperationReason.ScriptReward, out _);
            definition = definition with { MagicVitals = vitals };
            caster = caster with { MagicRecordId = 1 };
            if (passive != 0)
            {
                byte[] character = new byte[36];
                for (int offset = 12; offset < character.Length; offset += 4)
                {
                    character[offset + 1] = 1;
                    character[offset + 2] = 100;
                    character[offset + 3] = 1;
                }
                caster = caster with
                {
                    NativeGrowth = new(0, new byte[6].ToImmutableArray()),
                    NativePlayerStatRules = new(1, 1, character.ToImmutableArray(), new byte[12].ToImmutableArray(), Enumerable.Range(0, 57).Select(_ => new byte[60].ToImmutableArray()).ToImmutableArray(), Enumerable.Range(0, 19).Select(_ => new byte[8].ToImmutableArray()).ToImmutableArray(), new byte[20].ToImmutableArray(), 0, vitals.Definitions) { NativeStatusPassiveFlags = passive }
                };
            }
        }
        BattleCombatantDefinition[] extended = Enumerable.Range(2, 9).Select(index => definition.Combatants[2] with { Id = $"enemy-{index}" }).ToArray();
        return new(definition with { Combatants = [caster, .. definition.Combatants.Skip(1), .. extended] }, BattleAtbMode.TurnBased);
    }

    private static BattleSession RoundTrip(BattleSession battle)
    {
        return BattleSession.Restore(battle.Capture());
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
