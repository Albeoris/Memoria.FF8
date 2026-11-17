using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class DiagnosticGuardianForceChargeTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    public void Ordinary_charge_keeps_its_original_wait_and_native_decrement(bool? instant)
    {
        BattleSession battle = Create(instant);
        QueueCharge(battle);
        BattleGuardianForceCast cast = Assert.Single(battle.GuardianForceCasts);
        Assert.Equal((ushort)1060, cast.InitialTicks);
        Assert.Equal(cast.InitialTicks, cast.RemainingTicks);
        Assert.DoesNotContain(battle.Events, entry => entry.Kind == "DiagnosticGuardianForceChargeSkipped");
        Method("DispatchGuardianForceCasts").Invoke(battle, null);
        Assert.Equal((ushort)1058, Assert.Single(battle.GuardianForceCasts).RemainingTicks);
    }

    [Theory]
    [InlineData((ushort)3100, (ushort)1060)]
    [InlineData((ushort)0, (ushort)0)]
    public void Diagnostic_charge_completes_only_wait_and_records_original_ticks(ushort compatibility, ushort originalTicks)
    {
        BattleSession battle = Create(true, compatibility);
        List<BattleEvent> observed = [];
        battle.DiagnosticEventPublished += observed.Add;
        QueueCharge(battle);
        BattleGuardianForceCast cast = Assert.Single(battle.GuardianForceCasts);
        Assert.Equal(originalTicks, cast.InitialTicks);
        Assert.Equal((ushort)0, cast.RemainingTicks);
        Assert.Null(cast.Command.NativeAction);
        BattleEvent skipped = Assert.Single(observed, entry => entry.Kind == "DiagnosticGuardianForceChargeSkipped");
        Assert.Equal(originalTicks, skipped.Amount);
        Assert.Equal("party", skipped.ActorId);
        Assert.Empty(battle.QueuedActions);
        Assert.Empty(battle.Presentation!.ActionPackets.PublishedHeaders());
        Assert.Null(battle.ActiveAction);
        Method("DispatchGuardianForceCasts").Invoke(battle, null);
        Assert.Equal((ushort)0, Assert.Single(battle.GuardianForceCasts).RemainingTicks);
        Assert.Equal(0, battle.Frame);
        Assert.Equal(0, battle.Presentation!.Graphics.Callback);
        Assert.Equal(0, battle.DiagnosticRestoreCount);
        Assert.Equal(0, battle.DiagnosticResetCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Scheduling_cannot_be_changed_after_command_or_charge_admission(bool dispatch)
    {
        BattleSession battle = Create(null);
        battle.QueueAction("party", "enemy", BattleAbility.GuardianForce, 0);
        if (dispatch)
        {
            Method("DispatchAction").Invoke(battle, null);
        }
        Assert.Throws<InvalidOperationException>(() => battle.ConfigureDiagnosticScheduling(false, true));
    }

    [Fact]
    public void Scheduling_cannot_be_changed_after_the_first_callback()
    {
        BattleSession battle = Create(null);
        typeof(BattleSession).GetProperty(nameof(BattleSession.Frame))!.SetValue(battle, 1L);
        Assert.Throws<InvalidOperationException>(() => battle.ConfigureDiagnosticScheduling(false, true));
    }

    private static BattleSession Create(bool? instant, ushort compatibility = 3100)
    {
        BattleNativeAction native = new(254, 64, 2, 0) { Name = "Thunder Storm", MagicId = 116, AttackType = 11, Power = 44, HitCount = 1, TargetFlags = 0, AttackFlags = 0, DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = [], SupplementalRecord = [] };
        BattleSession battle = new(new BattleDefinition
        {
            NativeClockRules = new(10),
            NativeBattleSpeed = 2,
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            GuardianForces = [new(0, 300, 300)],
            ActionDefinitions = [new(BattleAbility.GuardianForce, 0, "Thunder Storm", BattleActionEffect.Native, 0, false) { NativeAction = native }],
            Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, Abilities = [BattleAbility.GuardianForce], EquipmentIds = [0], GuardianForceCompatibility = Enumerable.Repeat(compatibility, 16).ToArray(), Speed = 21 }, new() { Id = "enemy", Name = "Enemy" }]
        }, BattleAtbMode.Normal);
        if (instant is { } enabled)
        {
            battle.ConfigureDiagnosticScheduling(false, enabled);
        }
        var (roster, aliases) = battle.ReadBattlePresentationPacketBindings();
        NativeBattleGraphicsState graphics = NativeBattleHostedTestFixture.Graphics(roster, aliases, _ => new(0)) with { EntryReady = true, EntryLifecycle = 4 };
        NativeBattleHostedTestFixture.BindProcess(battle, graphics);
        Method("InitializeExpandedPartyPresentation").Invoke(battle, null);
        return battle;
    }

    private static void QueueCharge(BattleSession battle)
    {
        battle.QueueAction("party", "enemy", BattleAbility.GuardianForce, 0);
    }

    private static MethodInfo Method(string name)
    {
        return typeof(BattleSession).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    }
}
