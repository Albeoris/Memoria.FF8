using System.Reflection;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class DiagnosticNativeStatusRemovalTests
{
    [Fact]
    public void NativeEjectionObservationPreservesZeroFormulaAndActualStatusDrivenHpRemoval()
    {
        BattleSession observed = Create(true, true);
        BattleSession unobserved = Create(true, true);
        List<BattleDiagnosticResolution> resolutions = [];
        observed.DiagnosticResolution += resolutions.Add;

        Apply(observed);
        Apply(unobserved);

        BattleDiagnosticResolution resolution = Assert.Single(resolutions);
        BattleDiagnosticNativeStatusRemoval proof = Assert.IsType<BattleDiagnosticNativeStatusRemoval>(resolution.NativeStatusRemoval);
        Assert.Equal(0, resolution.ResolvedAmount);
        Assert.Equal(100, resolution.ActualAmount);
        Assert.Equal(100, resolution.BeforeHp);
        Assert.Equal(0, resolution.AfterHp);
        Assert.Equal(0x494410u, proof.ProducerAddress);
        Assert.Equal((byte)3, proof.ActorIndex);
        Assert.Equal((byte)0, proof.RecipientIndex);
        Assert.Equal((byte)8, proof.NativeCommand);
        Assert.Equal((ushort)72, proof.NativeActionId);
        Assert.Equal(0x10000u, proof.ActionStatuses1);
        Assert.Equal(0x10000u, proof.AppliedBattleStatuses & 0x10000);
        Assert.Equal(JsonSerializer.Serialize(unobserved.Capture()), JsonSerializer.Serialize(observed.Capture()));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void OrdinaryZeroDamageAndUnattributedRemovalCannotAcquireNativeEjectionProof(bool applied, bool actionDeclaresEjection)
    {
        BattleSession battle = Create(applied, actionDeclaresEjection);
        List<BattleDiagnosticResolution> resolutions = [];
        battle.DiagnosticResolution += resolutions.Add;
        Apply(battle);
        Assert.Null(Assert.Single(resolutions).NativeStatusRemoval);
    }

    private static void Apply(BattleSession battle)
    {
        BattleCombatant actor = battle.Combatants.Single(candidate => candidate.Id == "enemy");
        BattleCombatant target = battle.Combatants.Single(candidate => candidate.Id == "party");
        typeof(BattleSession).GetMethod("ApplyDamageCore", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(battle, [actor, target, 0, "Native status removal", false, false]);
    }

    private static BattleSession Create(bool applied, bool actionDeclaresEjection)
    {
        BattleSession battle = new(new()
        {
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, CurrentHp = 100, MaximumHp = 500, NativeDeathCount = 0 },
                new() { Id = "survivor", Name = "Survivor", IsPlayer = true, CurrentHp = 100, MaximumHp = 500, NativeDeathCount = 0 },
                new() { Id = "enemy", Name = "Enemy", CurrentHp = 500, MaximumHp = 500 }
            ]
        }, BattleAtbMode.Normal);
        BattleCombatant target = battle.Combatants.Single(candidate => candidate.Id == "party");
        target.NativeStatus1 = applied ? 0x10000u : 0;
        BattleNativeAction native = new(8, 72, 3, 72)
        {
            MagicId = 174,
            AttackType = 2,
            Power = 0,
            HitCount = 1,
            TargetFlags = null,
            AttackFlags = 1,
            DamagePresentation = 0,
            Element = 0,
            ElementPercent = null,
            StatusAttack = 255,
            Statuses0 = 0,
            Statuses1 = actionDeclaresEjection ? 0x10000u : 0,
            Hit = 70,
            Critical = null,
            PowerModifier = null,
            LevelModifier = null,
            RawRecord = [],
            SupplementalRecord = []
        };
        typeof(BattleSession).GetProperty(nameof(BattleSession.ActiveAction))!.SetValue(battle, new BattleActionPresentation("enemy", "party", BattleActionPhase.Windup, 0, BattleAbility.EnemyAbility, 72) { NativeAction = native, NativeQueuedCommand = 8, NativeTargetMask = 1 });
        return battle;
    }
}
