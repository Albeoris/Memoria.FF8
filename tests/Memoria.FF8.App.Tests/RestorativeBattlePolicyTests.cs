using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class RestorativeBattlePolicyTests
{
    [Fact]
    public void RestorativeBoundRequiresGuaranteedNativeHpDamageInsteadOfAssumingHealingIsOffense()
    {
        Ff7BattleAttack record = Record();
        Ff7BattleDamageAction action = Ff7BattleDamageAction.FromAttack(record) with { IsItem = true, Affinity = Ff7BattleDamageAffinity.Absorb };
        Ff7BattleDamageActor target = Target();
        Assert.Equal(5500u, Ff7BattleSession.RestorativeDamageBound(action, target, record).GuaranteedHpDamage);
        Assert.Equal(2750u, Ff7BattleSession.RestorativeDamageBound(action with { Power = 16 }, target, record).GuaranteedHpDamage);
        Assert.Equal(9999u, Ff7BattleSession.RestorativeDamageBound(action, target with { MaximumHitPoints = 20000 }, record).GuaranteedHpDamage);
        Assert.Equal(0u, Ff7BattleSession.RestorativeDamageBound(action with { Affinity = Ff7BattleDamageAffinity.Normal }, target, record).GuaranteedHpDamage);
        Assert.Equal(0u, Ff7BattleSession.RestorativeDamageBound(action with { Affinity = Ff7BattleDamageAffinity.Absorb | Ff7BattleDamageAffinity.Nullify }, target, record).GuaranteedHpDamage);
        Assert.Equal(0u, Ff7BattleSession.RestorativeDamageBound(action with { Accuracy = 254 }, target, record).GuaranteedHpDamage);
        Assert.Equal(0u, Ff7BattleSession.RestorativeDamageBound(action with { CanBeBlinded = true }, target, record).GuaranteedHpDamage);
        Assert.Equal(0u, Ff7BattleSession.RestorativeDamageBound(action with { Formula = 0x25 }, target, record).GuaranteedHpDamage);
        Assert.Equal(0u, Ff7BattleSession.RestorativeDamageBound(action with { IsItem = false, Reflectable = true }, target with { Statuses = Ff7NativeBattleStatuses.Reflect }, record).GuaranteedHpDamage);
    }

    [Theory]
    [InlineData(Ff7NativeBattleStatuses.Peerless)]
    [InlineData(Ff7NativeBattleStatuses.Barrier)]
    [InlineData(Ff7NativeBattleStatuses.MagicBarrier)]
    [InlineData(Ff7NativeBattleActionResolver.Death)]
    public void ProtectedOrInapplicableTargetsHaveNoGuaranteedRestorativeDamage(uint statuses)
    {
        Ff7BattleAttack record = Record();
        Ff7BattleDamageAction action = Ff7BattleDamageAction.FromAttack(record) with { IsItem = true, Affinity = Ff7BattleDamageAffinity.Absorb };
        Assert.Equal(0u, Ff7BattleSession.RestorativeDamageBound(action, Target() with { Statuses = statuses }, record).GuaranteedHpDamage);
        Assert.Equal(0u, Ff7BattleSession.RestorativeDamageBound(action, Target() with { NativeConditions = 0x200 }, record).GuaranteedHpDamage);
        Assert.Equal(0u, Ff7BattleSession.RestorativeDamageBound(action with { DamageScalePercent = 50 }, Target(), record).GuaranteedHpDamage);
    }

    [Fact]
    public void LegalGuaranteedKnockoutPrecedesNonurgentMaintenanceAndPhysicalActorOrder()
    {
        Ff7BattleCommandOption attack = new(Ff7BattleCommand.Attack, 0, 0x70, true, true);
        Ff7BattleCommandOption item = new(Ff7BattleCommand.Item, 41, 0x77, true, false, 1);
        Ff7BattleActorState[] actors = Actors(40);
        var choice = Ff7Scene.PlanAutoPlayBattleCommand([attack, item], actors, recoveryKind: (_, _) => Ff7BattleRecoveryKind.HitPoints, restorativeBenefit: (_, target) => new(target == 6 ? 5500u : 0))!.Value;
        Assert.Same(item, choice.Command);
        Assert.Equal(6, choice.TargetSlot);
        Assert.Equal((ushort)0x40, Ff7Scene.AutoPlayBattleTargetMask(choice.Command, choice.TargetSlot));
        Assert.Equal(1, item.Cost);
        Assert.Equal(5500u, actors.Single(actor => actor.Actor.Slot == 6).Actor.HitPoints);
    }

    [Fact]
    public void CriticalHealingAndRevivalRemainAheadOfOffensiveRestoration()
    {
        Ff7BattleCommandOption attack = new(Ff7BattleCommand.Attack, 0, 0x70, true, true);
        Ff7BattleCommandOption item = new(Ff7BattleCommand.Item, 41, 0x77, true, false, 1);
        var critical = Ff7Scene.PlanAutoPlayBattleCommand([attack, item], Actors(20), recoveryKind: (_, _) => Ff7BattleRecoveryKind.HitPoints, restorativeBenefit: (_, target) => new(target == 6 ? 5500u : 0))!.Value;
        Assert.Equal(1, critical.TargetSlot);
        Ff7BattleCommandOption revive = new(Ff7BattleCommand.Magic, 91, 2, true, false, 5);
        var fallen = Ff7Scene.PlanAutoPlayBattleCommand([attack, item, revive], Actors(0), recoveryKind: (command, _) => command.Command == Ff7BattleCommand.Magic ? Ff7BattleRecoveryKind.Revive : Ff7BattleRecoveryKind.HitPoints, restorativeBenefit: (_, target) => new(target == 6 ? 5500u : 0))!.Value;
        Assert.Same(revive, fallen.Command);
        Assert.Equal(1, fallen.TargetSlot);
    }

    [Fact]
    public void InvalidTargetsUnknownBoundsAndUnavailableCommandsDoNotBecomeOffensiveCandidates()
    {
        Ff7BattleCommandOption attack = new(Ff7BattleCommand.Attack, 0, 0x70, true, true);
        Ff7BattleCommandOption item = new(Ff7BattleCommand.Item, 41, 0x77, true, false, 1);
        Ff7BattleActorState[] actors = Actors(40);
        foreach (Ff7BattleCommandOption invalid in new[] { item with { ValidTargets = 7 }, item with { UnsupportedReason = "Unavailable" }, item with { SelectSingle = false }, item with { AllTargets = true } })
        {
            var choice = Ff7Scene.PlanAutoPlayBattleCommand([attack, invalid], actors, restorativeBenefit: (_, target) => new(target == 6 ? 5500u : 0))!.Value;
            Assert.Same(attack, choice.Command);
        }
        Assert.Same(attack, Ff7Scene.PlanAutoPlayBattleCommand([attack, item], actors, (_, target) => target != 6, restorativeBenefit: (_, target) => new(target == 6 ? 5500u : 0))!.Value.Command);
        Assert.Same(attack, Ff7Scene.PlanAutoPlayBattleCommand([attack, item], actors, restorativeBenefit: (_, _) => default)!.Value.Command);
        var insufficient = Ff7Scene.PlanAutoPlayBattleCommand([attack, item], actors, recoveryKind: (_, _) => Ff7BattleRecoveryKind.HitPoints, restorativeBenefit: (_, target) => new(target == 6 ? 5499u : 0))!.Value;
        Assert.Equal(1, insufficient.TargetSlot);
    }

    private static Ff7BattleAttack Record()
    {
        return new(255, 0, 0, 0, 0, 0, 0, 1, 0, 0x24, 32, 0, 255, 255, 0, 0, 0x200, ushort.MaxValue);
    }

    private static Ff7BattleDamageActor Target()
    {
        return new(1, 1, 1, 1, 1, 1, 1, 1, 1, IsEnemy: true, CurrentHitPoints: 5500, MaximumHitPoints: 5500);
    }

    private static Ff7BattleActorState[] Actors(uint injured)
    {
        return [Actor(0, 80, 100), Actor(1, injured, 100), Actor(2, 80, 100), Actor(4, 1300, 1300), Actor(5, 1300, 1300), Actor(6, 5500, 5500)];
    }

    private static Ff7BattleActorState Actor(int slot, uint hp, uint maximum)
    {
        return new(slot, new(slot, hp == 0 ? 1u : 0u, 8, 0, 0, hp, maximum), 0, []);
    }
}
