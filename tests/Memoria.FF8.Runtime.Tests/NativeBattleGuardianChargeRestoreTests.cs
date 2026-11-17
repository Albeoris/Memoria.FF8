using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGuardianChargeRestoreTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [Fact]
    public void Current_charge_and_growing_native_gauge_remain_independent_across_fork()
    {
        BattleSession source = CreateCharge();
        BattleGuardianForceCast original = Assert.Single(source.GuardianForceCasts);
        BattleSession copy = source.ForkForContinuation();
        Assert.Equal((ushort)1060, original.InitialTicks);
        Assert.Equal((ushort)1058, original.RemainingTicks);
        Assert.Equal((byte?)254, original.Command.NativeQueuedCommand);
        Assert.Equal(original, Assert.Single(copy.GuardianForceCasts));
        Assert.Equal(new NativeBattleAtbState(12000, 204), copy.Combatants[0].NativeAtb);
        Assert.True(copy.Combatants[0].IsBusy);

        Invoke(copy, "DispatchGuardianForceCasts");

        Assert.Equal((ushort)1056, Assert.Single(copy.GuardianForceCasts).RemainingTicks);
        Assert.Equal(original, Assert.Single(source.GuardianForceCasts));
        Assert.Equal(source.Combatants[0].NativeAtb, copy.Combatants[0].NativeAtb);
        Assert.Null(copy.ActiveAction);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Current_charge_rejects_missing_raw_gauge_and_wrong_display_projection(bool missing)
    {
        BattleSnapshot snapshot = CreateCharge().Capture();
        BattleCombatantSnapshot caster = snapshot.Combatants[0];
        snapshot.Combatants[0] = missing ? caster with { NativeAtb = null } : caster with { Atb = caster.Atb + 1 };

        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Current_charge_rejects_another_actor_wrong_command_or_excess_ticks(int corruption)
    {
        BattleSnapshot snapshot = CreateCharge().Capture();
        BattleGuardianForceCast cast = Assert.Single(snapshot.GuardianForceCasts);
        BattleGuardianForceCast corrupt = corruption switch
        {
            0 => cast with { Command = cast.Command with { ActorId = "enemy" } },
            1 => cast with { Command = cast.Command with { NativeQueuedCommand = 3 } },
            _ => cast with { RemainingTicks = checked((ushort)(cast.InitialTicks + 1)) }
        };

        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot with { GuardianForceCasts = [corrupt] }));
    }

    private static BattleSession CreateCharge()
    {
        BattleNativeAction native = new(254, 64, 2, 0) { Name = "Thunder Storm", MagicId = 116, AttackType = 11, Power = 44, HitCount = 1, TargetFlags = 0x48, AttackFlags = 0, DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = null, Critical = null, PowerModifier = null, LevelModifier = 0, RawRecord = [], SupplementalRecord = [] };
        BattleSession battle = new(new BattleDefinition
        {
            NativeClockRules = new(10),
            NativeBattleSpeed = 2,
            GuardianForces = [new(0, 300, 300)],
            ActionDefinitions = [new(BattleAbility.GuardianForce, 0, "Thunder Storm", BattleActionEffect.Native, 0, false, AllTargets: true) { NativeAction = native }],
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, Abilities = [BattleAbility.GuardianForce], EquipmentIds = [0], GuardianForceCompatibility = Enumerable.Repeat((ushort)3100, 16).ToArray(), Speed = 21 },
                new() { Id = "enemy", Name = "Enemy" }
            ]
        }, BattleAtbMode.Normal);
        BattleTargetRequest request = new(new BattleTargetSelection.Explicit([new BattleEnemyActorReference("enemy")]), BattleTargetEligibility.Living, false, BattleTargetReplacement.None);
        BattleAttackCommand command = new("party", "enemy", request, BattleAbility.GuardianForce) { NativeAction = native, NativeQueuedCommand = 254 };
        BattleSnapshot seed = battle.Capture();
        var gauge = new NativeBattleAtbState(12000, 204);
        seed.Combatants[0] = seed.Combatants[0] with { NativeAtb = gauge, Atb = gauge.Display };
        battle = BattleSession.Restore(seed);
        BattleCombatant actor = battle.Combatants[0];

        Invoke(battle, "InitializeExpandedPartyPresentation");
        Invoke(battle, "QueueGuardianForce", actor, command);
        Invoke(battle, "DispatchGuardianForceCasts");
        return battle;
    }

    private static void Invoke(BattleSession battle, string name, params object?[] arguments)
    {
        typeof(BattleSession).GetMethod(name, PrivateInstance)!.Invoke(battle, arguments);
    }
}
