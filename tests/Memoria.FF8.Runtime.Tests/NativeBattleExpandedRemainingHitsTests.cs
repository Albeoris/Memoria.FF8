using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleExpandedRemainingHitsTests
{
    [Fact]
    public void Allocation_initializes_nonrecipients_to_zero_and_preserves_unspent_hits_after_KO_abort()
    {
        BattleSession session = new(new()
        {
            Combatants =
            [
                new() { Id = "caster", Name = "Caster", IsPlayer = true },
                new() { Id = "observer", Name = "Observer", IsPlayer = true },
                new() { Id = "knocked-out", Name = "Knocked out", CurrentHp = 0, NativeStatus0 = 1 },
                new() { Id = "unreached", Name = "Unreached" }
            ]
        }, BattleAtbMode.TurnBased);
        var actors = session.Combatants.ToDictionary(actor => actor.Id);
        BattleCombatant caster = actors["caster"];
        BattleCombatant observer = actors["observer"];
        BattleCombatant knockedOut = actors["knocked-out"];
        BattleCombatant unreached = actors["unreached"];
        Assert.All(actors.Values, actor =>
        {
            Assert.NotNull(actor.Slot);
            actor.NativeRemainingHits = 9;
        });
        Assert.False(knockedOut.IsAlive);
        BattleNativeAction native = new(254, 64, 2, 0) { MagicId = 116, AttackType = 11, Power = 44, HitCount = 2, TargetFlags = 72, AttackFlags = 33, DamagePresentation = 5, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = null, Critical = null, PowerModifier = 0, LevelModifier = 60, RawRecord = new byte[132].ToImmutableArray(), SupplementalRecord = [] };
        BattleActionDefinition action = new(BattleAbility.GuardianForce, 0, "Summon fixture", BattleActionEffect.Native, 44, true) { NativeAction = native };
        BattleActionPresentation command = new(caster.Id, knockedOut.Id, BattleActionPhase.Recovery, 1, BattleTargetRequest.None, BattleAbility.GuardianForce);
        BattleCombatant[][] plan = [[knockedOut, unreached], [unreached]];
        MethodInfo resolve = typeof(BattleSession).GetMethod("ResolveExpandedHitPlan", BindingFlags.Instance | BindingFlags.NonPublic)!;

        var result = Assert.IsType<ValueTuple<ImmutableArray<ExpandedBattleContact>, BattleCombatant?>>(resolve.Invoke(session, [caster, command, action, native, plan, true]));

        Assert.Empty(result.Item1);
        Assert.Null(result.Item2);
        Assert.Equal(0, caster.NativeRemainingHits);
        Assert.Equal(0, observer.NativeRemainingHits);
        Assert.Equal(1, knockedOut.NativeRemainingHits);
        Assert.Equal(2, unreached.NativeRemainingHits);
    }
}
