using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class ExpandedCompelledActionsTests
{
    [Theory]
    [InlineData(0x20, 0, 0, "enemy-0")]
    [InlineData(0x20, 0x4000, 0, "ally-0")]
    [InlineData(0, 0x4000, 2, "ally-6")]
    [InlineData(0, 0x4000, 8, "enemy-6")]
    public void ReadyCompelledActorSelectsTheProvedSideAndRestoresItsAction(ushort savedStatus, uint battleStatus, byte phase, string targetId)
    {
        const string caster = "ally-10";
        BattleNativeAction attack = new(1, 0, 4, 0) { Name = "Attack", MagicId = 0, AttackType = 1, Power = 30, HitCount = 1, TargetFlags = 0x50, AttackFlags = 0, DamagePresentation = 4, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = 255, Critical = 0, PowerModifier = null, LevelModifier = null, RawRecord = [], SupplementalRecord = [] };
        BattleCombatantDefinition[] party = Enumerable.Range(0, 11).Select(i => Combatant($"ally-{i}") with
        {
            IsPlayer = true,
            ModelId = i,
            NativeAttack = attack,
            NativeCommandSlots = [1, 1, 1, 1],
            NativeStatus0 = i == 10 ? savedStatus : (ushort)0,
            NativeStatus1 = i == 10 ? battleStatus : 0u,
            InitialAtb = i >= 9 ? BattleSession.AtbMaximum : 0
        }).ToArray();
        BattleCombatantDefinition[] enemies = Enumerable.Range(0, 11).Select(i => Combatant($"enemy-{i}")).ToArray();
        BattleDefinition definition = new()
        {
            UsesExpandedRoster = true,
            Combatants = [.. party, .. enemies],
            InitialRandom = NativeBattleRandomState.ForecastPhase(phase),
            NativeFieldBattleRules = 0,
            NativeCommandSequences = ImmutableArray.CreateRange(new byte[39])
        };
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        Assert.Equal("ally-9", Assert.Single(battle.ReadyPlayerCommandOrder).Id);
        Assert.Throws<InvalidOperationException>(() => battle.QueueAttack(caster, "enemy-0"));

        battle.Tick();
        BattleActionPresentation action = Assert.IsType<BattleActionPresentation>(battle.ActiveAction);
        Assert.Equal((caster, targetId), (action.ActorId, action.TargetId));
        Assert.Equal(BattleAiTargetPolicy.Compelled, action.AiRecipients!.TargetPolicy);
        Assert.Equal(0, action.NativeTargetMask);

        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        for (int frame = 0; frame <= BattleSession.AttackWindupFrames; frame++)
        {
            battle.Tick();
            restored.Tick();
        }
        Assert.Contains(battle.Events, entry => entry.Kind == "Damage" && entry.ActorId == caster && entry.TargetId == targetId && entry.Amount > 0);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Fact]
    public void BerserkReplacesAQueuedItemAndRefundsItsReservation()
    {
        BattleNativeAction item = new(4, 1, 5, 1) { Name = "Potion", MagicId = 0, AttackType = 3, Power = 20, HitCount = 1, TargetFlags = 0x10, AttackFlags = 2, DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = [.. new byte[24]], SupplementalRecord = [] };
        BattleDefinition definition = new()
        {
            UsesExpandedRoster = true,
            Combatants = [Combatant("caster") with { IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, Abilities = [BattleAbility.Attack, BattleAbility.Item] }, Combatant("enemy")],
            ActionDefinitions = [new(BattleAbility.Item, 1, "Potion", BattleActionEffect.Native, 20, true) { NativeAction = item }],
            NativeActions = [item],
            Items = [new(1, 1)]
        };
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        battle.QueueAction("caster", "caster", BattleAbility.Item, 1);
        Assert.Empty(battle.Items);
        BattleSnapshot queued = battle.Capture();
        queued.Combatants[0] = queued.Combatants[0] with { NativeStatus = queued.Combatants[0].NativeStatus! with { Saved = 0x20 } };
        battle = BattleSession.Restore(queued);

        battle.Tick();

        BattleActionPresentation action = Assert.IsType<BattleActionPresentation>(battle.ActiveAction);
        Assert.Equal((BattleAbility.Attack, "enemy"), (action.Ability, action.TargetId));
        Assert.Equal(new BattleStock(1, 1), Assert.Single(battle.Items));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConfuseReplacesAQueuedAttackWithMagicAndSilencePreventsTheDebit(bool silenced)
    {
        BattleNativeAction spell = new(2, 1, 1, 1) { Name = "Fire", MagicId = 2, AttackType = 2, Power = 22, HitCount = 1, TargetFlags = 0x50, AttackFlags = 1, DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = [.. new byte[60]], SupplementalRecord = [] };
        BattleCombatantDefinition caster = Combatant("caster") with
        {
            IsPlayer = true, ModelId = 1, Magic = 20, InitialAtb = BattleSession.AtbMaximum, Abilities = [BattleAbility.Attack, BattleAbility.Magic],
            NativeCommandSlots = [2, 2, 2, 2], NativeMagicInventory = BattleCombatant.ExpandMagicStocks([new BattleStock(1, 2)])
        };
        BattleDefinition definition = new()
        {
            UsesExpandedRoster = true,
            Combatants = [caster, Combatant("enemy")],
            ActionDefinitions = [new(BattleAbility.Magic, 1, "Fire", BattleActionEffect.Native, 22, false) { NativeAction = spell }],
            NativeActions = [spell],
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            NativeCommandSequences = ImmutableArray.CreateRange(new byte[39]),
            NativeGlobalMessages = Enumerable.Range(0, 101).Select(index => $"Message{index}").ToArray(),
            NativeMessageSpeed = 1,
            NativeFieldBattleRules = 0,
            InitialRandom = NativeBattleRandomState.ForecastPhase(8)
        };
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        battle.QueueAttack("caster", "enemy");
        BattleSnapshot queued = battle.Capture();
        queued.Combatants[0] = queued.Combatants[0] with { NativeStatus = queued.Combatants[0].NativeStatus! with { Saved = silenced ? (ushort)0x10 : (ushort)0, Battle = 0x4000 } };
        battle = BattleSession.Restore(queued);

        battle.Tick();
        Assert.Equal(BattleAbility.Magic, battle.ActiveAction!.Ability);
        Assert.Equal("enemy", battle.ActiveAction.TargetId);
        for (int frame = 0; frame < BattleSession.AttackWindupFrames; frame++)
        {
            battle.Tick();
        }
        Assert.Equal(silenced ? 2 : 1, Assert.Single(battle.Combatants[0].MagicStocks).Count);
        Assert.Equal(silenced, battle.Events.Any(entry => entry.Kind == "NativeText" && entry.Message == "Message100"));
        Assert.Equal(!silenced, battle.Events.Any(entry => entry.Kind == "Damage" && entry.TargetId == "enemy"));
    }

    [Fact]
    public void FailedConfusePreparationKeepsTheQueuedItemItsStockAndRandomCursor()
    {
        BattleNativeAction item = new(4, 1, 7, 1) { Name = "Potion", MagicId = 0, AttackType = 4, Power = 20, HitCount = 1, TargetFlags = 0x10, AttackFlags = 2, DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = [.. new byte[24]], SupplementalRecord = [] };
        BattleNativeAction unavailable = item with { Command = 2, KernelSection = 1, AttackType = 14, TargetFlags = 0x50 };
        BattleDefinition definition = new()
        {
            UsesExpandedRoster = true,
            Combatants = [Combatant("caster") with { IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, Abilities = [BattleAbility.Attack, BattleAbility.Item], NativeCommandSlots = [2, 2, 2, 2], MagicStocks = [new(1, 1)] }, Combatant("enemy")],
            ActionDefinitions = [new(BattleAbility.Item, 1, "Potion", BattleActionEffect.Native, 20, true) { NativeAction = item }],
            NativeActions = [item, unavailable],
            NativeFieldBattleRules = 0,
            Items = [new(1, 1)]
        };
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        battle.QueueAction("caster", "caster", BattleAbility.Item, 1);
        BattleSnapshot queued = battle.Capture();
        queued.Combatants[0] = queued.Combatants[0] with { NativeStatus = queued.Combatants[0].NativeStatus! with { Battle = 0x4000 } };
        battle = BattleSession.Restore(queued);

        Assert.Throws<NotSupportedException>(battle.Tick);

        Assert.Equal(Assert.Single(queued.QueuedActions), Assert.Single(battle.QueuedActions));
        Assert.Equal(queued.NativeItemInventory, battle.Capture().NativeItemInventory);
        Assert.Equal(JsonSerializer.Serialize(queued.Random), JsonSerializer.Serialize(battle.Capture().Random));
        Assert.Equal(1, Assert.Single(battle.Combatants[0].MagicStocks).Count);
    }

    private static BattleCombatantDefinition Combatant(string id)
    {
        return new() { Id = id, Name = id, CurrentHp = 500, MaximumHp = 500, Speed = 1, Attack = 40, Level = 10, Abilities = [BattleAbility.Attack] };
    }
}
