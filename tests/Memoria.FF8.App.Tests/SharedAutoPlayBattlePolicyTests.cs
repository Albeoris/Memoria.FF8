using Memoria.AutoPlay;
using Memoria.FF7.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class SharedAutoPlayBattlePolicyTests
{
    [Fact]
    public void RetreatPreservesRecoveryAndNativeEscapePermissions()
    {
        AutoPlayBattleRecoveryTarget[] critical = [new(10, 100, false), new(25, 100, false), new(0, 100, true)];
        Assert.True(AutoPlayBattlePolicy.ShouldRetreat(true, false, critical));
        Assert.False(AutoPlayBattlePolicy.ShouldRetreat(false, false, critical));
        Assert.False(AutoPlayBattlePolicy.ShouldRetreat(true, true, critical));
        Assert.False(AutoPlayBattlePolicy.ShouldRetreat(true, false, [new(26, 100, false)]));
        Assert.False(AutoPlayBattlePolicy.ShouldRetreat(true, false, [new(0, 100, true)]));
        Assert.False(AutoPlayBattlePolicy.ShouldRetreat(true, false, [new(1, 0, false)]));
        Assert.False(AutoPlayBattlePolicy.ShouldRetreat(true, false, [new(uint.MaxValue, uint.MaxValue, false)]));
    }

    [Fact]
    public void OrdinaryAttackPreservesReadyActorAndCombatantOrder()
    {
        AutoPlayBattleActor[] actors =
        [
            new(false, true, true, false, true),
            new(true, false, true, false, true),
            new(true, true, false, false, true),
            new(true, true, true, true, true),
            new(true, true, true, false, false),
            new(true, true, true, false, true),
            new(true, true, true, false, true)
        ];
        AutoPlayBattleTarget[] targets =
        [
            new(false, true, true, false),
            new(true, false, true, false),
            new(true, true, false, false),
            new(true, true, true, true),
            new(true, true, true, false),
            new(true, true, true, false)
        ];

        Assert.Equal(new(AutoPlayBattleDecisionKind.Attack, 5, 4), Choose(actors, targets));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void NativeEntryAndPauseGateAttack(bool entryReady, bool paused)
    {
        Assert.Equal(AutoPlayBattleDecisionKind.Tick, AutoPlayBattlePolicy.Choose(false, true, "Active", entryReady, paused, [new(true, true, true, false, true)], [new(true, true, true, false)]).Kind);
    }

    [Fact]
    public void ReturnPrecedesTerminalFailureAndMissingTargetWaits()
    {
        Assert.Equal(AutoPlayBattleDecisionKind.Return, AutoPlayBattlePolicy.Choose(true, false, "Victory", false, true, [], []).Kind);
        Assert.Equal("Automatic combat stopped at its actual Defeat outcome.", Assert.Throws<NotSupportedException>(() => AutoPlayBattlePolicy.Choose(false, false, "Defeat", true, false, [], [])).Message);
        Assert.Equal(AutoPlayBattleDecisionKind.Tick, Choose([new(true, true, true, false, true)], []).Kind);
    }

    [Fact]
    public void Ff7UsesNativeAttackArgumentAndValidTargetsInActorOrder()
    {
        Ff7BattleCommandOption attack = new(Ff7BattleCommand.Attack, 17, 0xC0, true, true) { CandidateTargets = 0xF0 };
        var selected = Ff7Scene.PlanAutoPlayBattleCommand([attack], [Actor(4), Actor(7), Actor(6)]);

        Assert.NotNull(selected);
        Assert.Same(attack, selected.Value.Command);
        Assert.Equal(7, selected.Value.TargetSlot);
        Assert.Equal((ushort)0x80, Ff7Scene.AutoPlayBattleTargetMask(selected.Value.Command, selected.Value.TargetSlot));
    }

    [Fact]
    public void Ff7SkipsDeadAndUnsupportedTargetsWithoutSelectingFriendlyActors()
    {
        Ff7BattleCommandOption attack = new(Ff7BattleCommand.Attack, 0, 0xF1, true, true);
        var selected = Ff7Scene.PlanAutoPlayBattleCommand([attack], [Actor(0), Actor(4, 0), Actor(5), Actor(6), Actor(7)], (_, slot) => slot != 5);

        Assert.Equal(6, selected!.Value.TargetSlot);
    }

    [Fact]
    public void Ff7PrefersAttackAndUsesAvailableLimitWhenNativeMenuReplacesAttack()
    {
        Ff7BattleCommandOption limit = new(Ff7BattleCommand.Limit, 0x87, 0x30, false, true);
        Ff7BattleCommandOption attack = new(Ff7BattleCommand.Attack, 0, 0x10, true, true);
        Assert.Same(attack, Ff7Scene.PlanAutoPlayBattleCommand([limit, attack], [Actor(4), Actor(5)])!.Value.Command);
        var selected = Ff7Scene.PlanAutoPlayBattleCommand([limit], [Actor(4), Actor(5)])!.Value;
        Assert.Same(limit, selected.Command);
        Assert.Equal((ushort)0x30, Ff7Scene.AutoPlayBattleTargetMask(selected.Command, selected.TargetSlot));
        Assert.Throws<NotSupportedException>(() => Ff7Scene.PlanAutoPlayBattleCommand([limit with { UnsupportedReason = "Unavailable native contract" }], [Actor(4)]));
    }

    [Fact]
    public void Ff7WaitsWhenNoLivingSupportedNativeTargetExists()
    {
        Ff7BattleCommandOption attack = new(Ff7BattleCommand.Attack, 0, 0x10, true, true);
        Assert.Null(Ff7Scene.PlanAutoPlayBattleCommand([attack], [Actor(4, 0)]));
        Assert.Null(Ff7Scene.PlanAutoPlayBattleCommand([attack], [Actor(4)], (_, _) => false));
    }

    [Fact]
    public void Ff7ValidatesEveryTargetOfAnAutomaticGroupCommand()
    {
        Ff7BattleCommandOption limit = new(Ff7BattleCommand.Limit, 0x87, 0x30, false, true);
        Assert.Throws<NotSupportedException>(() => Ff7Scene.PlanAutoPlayBattleCommand([limit], [Actor(4), Actor(5)], (_, slot) => slot == 4));
    }

    [Fact]
    public void RecoveryStabilizesCriticalAllyBeforeRevivalThenUsesLowestHpRatio()
    {
        AutoPlayBattleRecoveryTarget[] targets = [new(70, 100, false), new(10, 100, false), new(0, 100, true)];
        AutoPlayBattleRecoveryOption[] options = [new(0, 0, false), new(0, 1, false), new(1, 2, true)];
        Assert.Equal(options[1], AutoPlayBattlePolicy.ChooseRecovery(targets, options));
        targets[1] = new(30, 100, false);
        Assert.Equal(options[2], AutoPlayBattlePolicy.ChooseRecovery(targets, options));
        Assert.Equal(options[1], AutoPlayBattlePolicy.ChooseRecovery(targets, options[..2]));
        targets[1] = new(51, 100, false);
        Assert.Null(AutoPlayBattlePolicy.ChooseRecovery(targets, options[..2]));
    }

    [Fact]
    public void RecoveryMetadataDistinguishesHpMpAndKnockedOutTargetOperations()
    {
        Ff7BattleDamageAction healing = new(0x22, 8, 255, Healing: true);
        Assert.Equal(Ff7BattleRecoveryKind.HitPoints, Ff7BattleSession.ClassifyRecovery(healing));
        Assert.Equal(Ff7BattleRecoveryKind.None, Ff7BattleSession.ClassifyRecovery(healing with { TargetsMagicPoints = true }));
        Assert.Equal(Ff7BattleRecoveryKind.None, Ff7BattleSession.ClassifyRecovery(healing with { Healing = false }));
        Assert.Equal(Ff7BattleRecoveryKind.Revive, Ff7BattleSession.ClassifyRecovery(healing with { RequiresKnockedOutTarget = true }));
    }

    [Fact]
    public void RecoveryMetadataRejectsInvulnerabilityAbsorptionReflectionAndWrongLifeState()
    {
        Ff7BattleDamageAction healing = new(0x22, 8, 255, Healing: true);
        Ff7BattleDamageActor ally = new(1, 1, 1, 1, 1, 1, 1, 1, 1, CurrentHitPoints: 10, MaximumHitPoints: 100);
        Assert.True(Ff7BattleSession.CanRecoverTarget(Ff7BattleRecoveryKind.HitPoints, healing, ally));
        Assert.False(Ff7BattleSession.CanRecoverTarget(Ff7BattleRecoveryKind.HitPoints, healing with { Affinity = Ff7BattleDamageAffinity.Absorb }, ally));
        Assert.False(Ff7BattleSession.CanRecoverTarget(Ff7BattleRecoveryKind.HitPoints, healing with { Affinity = Ff7BattleDamageAffinity.Nullify }, ally));
        Assert.False(Ff7BattleSession.CanRecoverTarget(Ff7BattleRecoveryKind.HitPoints, healing, ally with { NativeConditions = 0x200 }));
        Assert.False(Ff7BattleSession.CanRecoverTarget(Ff7BattleRecoveryKind.HitPoints, healing, ally with { Statuses = Ff7NativeBattleStatuses.Peerless }));
        Assert.False(Ff7BattleSession.CanRecoverTarget(Ff7BattleRecoveryKind.HitPoints, healing with { Reflectable = true }, ally with { Statuses = Ff7NativeBattleStatuses.Reflect }));
        Assert.False(Ff7BattleSession.CanRecoverTarget(Ff7BattleRecoveryKind.HitPoints, healing, ally with { OriginSavedStatuses = Ff7OriginSavedStatusRules.Zombie }));
        Assert.False(Ff7BattleSession.CanRecoverTarget(Ff7BattleRecoveryKind.HitPoints, healing, ally with { Statuses = Ff7NativeBattleActionResolver.Death }));
        Assert.True(Ff7BattleSession.CanRecoverTarget(Ff7BattleRecoveryKind.Revive, healing with { RequiresKnockedOutTarget = true }, ally with { Statuses = Ff7NativeBattleActionResolver.Death, CurrentHitPoints = 0 }));
    }

    [Fact]
    public void Ff7UsesOnlyAvailableSupportedRecoveryBeforeOrdinaryAttack()
    {
        Ff7BattleCommandOption attack = new(Ff7BattleCommand.Attack, 0, 0x10, true, true);
        Ff7BattleCommandOption healing = new(Ff7BattleCommand.Item, 101, 3, true, false, 1);
        Ff7BattleActorState[] actors = [Actor(0, 80), Actor(1, 10), Actor(4)];
        var choice = Ff7Scene.PlanAutoPlayBattleCommand([attack, healing], actors, recoveryKind: (_, _) => Ff7BattleRecoveryKind.HitPoints)!.Value;
        Assert.Same(healing, choice.Command);
        Assert.Equal(1, choice.TargetSlot);
        Assert.Same(attack, Ff7Scene.PlanAutoPlayBattleCommand([attack], actors, recoveryKind: (_, _) => Ff7BattleRecoveryKind.HitPoints)!.Value.Command);
        Assert.Same(attack, Ff7Scene.PlanAutoPlayBattleCommand([attack, healing with { UnsupportedReason = "Unavailable" }], actors, recoveryKind: (_, _) => Ff7BattleRecoveryKind.HitPoints)!.Value.Command);
        Assert.Same(attack, Ff7Scene.PlanAutoPlayBattleCommand([attack, healing], actors, (command, _) => command.Command != Ff7BattleCommand.Item, (_, _) => Ff7BattleRecoveryKind.HitPoints)!.Value.Command);
        Assert.Equal((uint)10, actors[1].Actor.HitPoints);
        Assert.Equal(1, healing.Cost);
    }

    private static AutoPlayBattleDecision Choose(AutoPlayBattleActor[] actors, AutoPlayBattleTarget[] targets)
    {
        return AutoPlayBattlePolicy.Choose(false, true, "Active", true, false, actors, targets);
    }

    private static Ff7BattleActorState Actor(int slot, uint hp = 100)
    {
        return new(slot, new(slot, 0, 8, 0, 0, hp, 100), 0, []);
    }
}
