using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleSmartRecoveryTargetTests
{
    [Theory]
    [InlineData(BattleAbility.Magic)]
    [InlineData(BattleAbility.Item)]
    public void InitialCursorPrefersRemovableStatusAndManualMovementStaysSelected(BattleAbility ability)
    {
        BattleActionDefinition action = Action(ability, saved: 2);
        BattleSession session = Create(action, 2);
        string before = JsonSerializer.Serialize(session.Capture());
        BattleHudController hud = Open(session, ability);
        Assert.Equal("patient", hud.SelectedTarget!.Id);
        hud.SelectTarget("caster", false);
        Assert.Equal("caster", hud.SelectedTarget!.Id);
        Assert.Equal(before, JsonSerializer.Serialize(session.Capture()));
    }

    [Theory]
    [InlineData(8)]
    [InlineData(0)]
    public void UnrelatedOrAbsentStatusPreservesOriginalFallback(ushort status)
    {
        BattleHudController hud = Open(Create(Action(saved: 2), status), BattleAbility.Magic);
        Assert.Equal("caster", hud.SelectedTarget!.Id);
    }

    [Fact]
    public void RevivalChoosesDeadAllyRatherThanLivingZombie()
    {
        BattleActionDefinition action = Action(type: 5);
        BattleSession session = Create(action, 1, hp: 0, casterStatus: 0x40);
        BattleHudController hud = Open(session, BattleAbility.Magic);
        Assert.Equal("patient", hud.SelectedTarget!.Id);
        Assert.Equal(new BattleStatusPresentation(1, 0), session.ReadRemovableRecoveryStatuses("patient", action));
        Assert.Equal(default, session.ReadRemovableRecoveryStatuses("caster", action));
    }

    [Fact]
    public void RecoveryQueryFiltersPositiveFlagsAndPermanentTimedStatusesWithoutMutation()
    {
        BattleActionDefinition action = Action(saved: 0xFFFF, battle: uint.MaxValue);
        BattleSession session = Create(action, 0x380, battleStatus: 14);
        string before = JsonSerializer.Serialize(session.Capture());
        Assert.Equal(default, session.ReadRemovableRecoveryStatuses("patient", action));
        Assert.Equal(before, JsonSerializer.Serialize(session.Capture()));
    }

    [Fact]
    public void RecoveryQuerySeesProjectedStatusEffects()
    {
        BattleActionDefinition action = Action(battle: 4);
        BattleSession original = Create(action, 0);
        BattleDefinition definition = original.Capture().Definition;
        BattleSession session = new(definition with
        {
            Combatants = [definition.Combatants[0], definition.Combatants[1] with { StatusEffects = [new() { Kind = BattleStatusKind.Slow }] }, definition.Combatants[2]]
        }, BattleAtbMode.TurnBased);
        Assert.Equal(new BattleStatusPresentation(0, 4), session.ReadRemovableRecoveryStatuses("patient", action));
        Assert.Equal("patient", Open(session, BattleAbility.Magic).SelectedTarget!.Id);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void GroupOrOffensiveActionKeepsOriginalDefault(bool group, bool offensive)
    {
        BattleActionDefinition action = Action(saved: 2) with { AllTargets = group, NativeAction = Native(offensive ? (byte)2 : (byte)3, 2, 0) with { TargetFlags = group ? (byte)0x34 : (byte)0x14 } };
        Assert.Equal("caster", Open(Create(action, 2), BattleAbility.Magic).SelectedTarget!.Id);
    }

    [Fact]
    public void OffensiveHealingAgainstZombieIsNotPreferred()
    {
        BattleActionDefinition action = Action(saved: 0x40) with { NativeAction = Native(3, 0x40, 0) with { Power = 10 } };
        Assert.Equal(default, Create(action, 0x40).ReadRemovableRecoveryStatuses("patient", action));
    }

    [Theory]
    [InlineData(2, "patient")]
    [InlineData(0, "caster")]
    public void SmartRecoveryOverridesMemoryOnlyWhenThereIsARemovableStatus(ushort status, string expected)
    {
        BattleSession session = Create(Action(saved: 2), status);
        BattleHudController hud = new(session, (_, _) => { }, () => { }, () => { }, execute: _ => { }) { CursorMemory = true };
        hud.Choose(hud.Commands.ToList().IndexOf(BattleAbility.Magic));
        hud.Choose(0);
        hud.SelectTarget("caster", true);
        hud.Choose(hud.Commands.ToList().IndexOf(BattleAbility.Magic));
        hud.Choose(0);
        Assert.Equal(expected, hud.SelectedTarget!.Id);
    }

    [Theory]
    [InlineData(3, 3, 0u, 0)]
    [InlineData(21, 2, 0x200000u, 0)]
    [InlineData(3, 2, 0x2000u, 8)]
    public void MagicRecoveryQueryRespectsNativeImmunityAndExplicitBypass(byte type, ushort saved, uint battle, byte element)
    {
        BattleActionDefinition action = Action(type: type, saved: 2) with { NativeAction = Native(type, 2, 0) with { Element = element } };
        BattleSession session = Create(action, saved, battleStatus: battle);
        string before = JsonSerializer.Serialize(session.Capture());
        Assert.Equal(default, session.ReadRemovableRecoveryStatuses("patient", action));
        BattleActionDefinition bypass = action with { NativeAction = action.NativeAction! with { Statuses1 = 0x4000000 } };
        Assert.Equal(new BattleStatusPresentation(2, 0), session.ReadRemovableRecoveryStatuses("patient", bypass));
        Assert.Equal(before, JsonSerializer.Serialize(session.Capture()));
    }

    [Fact]
    public void InitialCursorSkipsImmuneAllyAndChoosesAllyWhoseStatusCanBeRemoved()
    {
        BattleActionDefinition action = Action(saved: 2);
        BattleDefinition definition = Create(action, 2, battleStatus: 0x200000).Capture().Definition;
        BattleSession session = new(definition with
        {
            Combatants = [definition.Combatants[0], definition.Combatants[1], definition.Combatants[2] with { Id = "curable", IsPlayer = true, NativeStatus0 = 2 }, definition.Combatants[2]]
        }, BattleAtbMode.TurnBased);
        Assert.Equal("curable", Open(session, BattleAbility.Magic).SelectedTarget!.Id);
    }

    [Theory]
    [InlineData(4, 0, 255)]
    [InlineData(4, 1, 255)]
    [InlineData(25, 255, 0)]
    [InlineData(32, 255, 1)]
    public void GuaranteedFailureOrZeroStatusChanceDoesNotPreferAnItemTarget(byte type, byte hit, byte statusChance)
    {
        BattleActionDefinition action = Action(BattleAbility.Item, type, 2) with { NativeAction = Native(type, 2, 0) with { Hit = hit, StatusAttack = statusChance } };
        BattleSession session = Create(action, 2);
        Assert.Equal(default, session.ReadRemovableRecoveryStatuses("patient", action));
        Assert.Equal("caster", Open(session, BattleAbility.Item).SelectedTarget!.Id);
    }

    [Fact]
    public void ItemWithPositiveStatusChanceSelectsAllyWithRemovableCondition()
    {
        BattleActionDefinition action = Action(BattleAbility.Item, 4, 2) with { NativeAction = Native(4, 2, 0) with { Hit = 255, StatusAttack = 255 } };
        BattleSession session = Create(action, 2);
        Assert.Equal(new BattleStatusPresentation(2, 0), session.ReadRemovableRecoveryStatuses("patient", action));
        Assert.Equal("patient", Open(session, BattleAbility.Item).SelectedTarget!.Id);
    }

    private static BattleHudController Open(BattleSession session, BattleAbility ability)
    {
        BattleHudController hud = new(session, (_, _) => { }, () => { }, () => { });
        hud.Choose(hud.Commands.ToList().IndexOf(ability));
        hud.Choose(0);
        Assert.True(hud.SelectingTarget);
        return hud;
    }

    private static BattleSession Create(BattleActionDefinition action, ushort status, int hp = 100, ushort casterStatus = 0, uint battleStatus = 0)
    {
        return new(new BattleDefinition
        {
            NativeFieldBattleRules = 0,
            Combatants =
            [
                new() { Id = "caster", Name = "Caster", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, Abilities = [action.Ability], MagicStocks = [new(action.Id, 10)], NativeStatus0 = casterStatus },
                new() { Id = "patient", Name = "Patient", IsPlayer = true, CurrentHp = hp, NativeStatus0 = status, NativeStatus1 = battleStatus },
                new() { Id = "enemy", Name = "Enemy" }
            ],
            ActionDefinitions = [action],
            NativeActions = [action.NativeAction!],
            Items = [new(action.Id, 10)]
        }, BattleAtbMode.TurnBased);
    }

    private static BattleActionDefinition Action(BattleAbility ability = BattleAbility.Magic, byte type = 3, ushort saved = 0, uint battle = 0)
    {
        return new(ability, 1, "Recovery", BattleActionEffect.Native, 0, true) { NativeAction = Native(type, saved, battle) };
    }

    private static BattleNativeAction Native(byte type, ushort saved, uint battle)
    {
        return new(2, 1, 1, 1)
        {
            MagicId = 1, AttackType = type, Power = 0, HitCount = 1, TargetFlags = 0x14, AttackFlags = 0, DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = saved, Statuses1 = battle, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = ImmutableArray<byte>.Empty, SupplementalRecord = ImmutableArray<byte>.Empty
        };
    }
}
