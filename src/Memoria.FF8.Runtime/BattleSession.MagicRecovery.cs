using System.Numerics;

namespace Memoria.FF8.Runtime;

public sealed record BattleMagicRecoveryChoice(int ActionId, string TargetId, double ExpectedHealing = 0, double FullRecoveryProbability = 0);

public sealed partial class BattleSession
{
    /// <summary>Selects one useful restorative cast without changing stocks, reservations or random state.</summary>
    public BattleMagicRecoveryChoice? PreviewMagicRecovery(string actorId)
    {
        BattleCombatant actor = GetActor(actorId);
        if (Outcome != BattleOutcome.Active || !actor.IsPlayer || !actor.IsReady || !actor.Abilities.Contains(BattleAbility.Magic) || !CanNativeBattleActorAct(actor) || RequiresNativeCompelledCommand(actor) || (NativeSavedStatusBits(actor) & 0x10) != 0 || HasStatus(actor, BattleStatusKind.Silence))
        {
            return null;
        }
        BattleActionDefinition[] spells = definition.ActionDefinitions.Where(action => action.Ability == BattleAbility.Magic && action.Available && GetAvailableMagicCasts(actorId, action.Id) > 0).ToArray();
        List<(BattleActionDefinition Action, BattleCombatant Target, int Removed, double Revived, BattleDamageForecast? Healing)> candidates = [];
        foreach (BattleCombatant target in combatants.Where(target => target.IsPlayer == actor.IsPlayer).OrderBy(target => target.NativeIndex))
        {
            foreach (BattleActionDefinition action in spells.OrderBy(action => action.Id))
            {
                if (!IsValidSelectedActionTarget(actor, action, target) || action.NativeAction is { } native && NativeActionReflects(target, native))
                {
                    continue;
                }
                BattleStatusPresentation removable = ReadRemovableRecoveryStatuses(target.Id, action);
                if (!target.IsAlive && (removable.Saved & 1) != 0)
                {
                    double revived = action.NativeAction is { } revive ? revive.AttackType == 6 ? target.MaximumHp : Math.Max(1, target.MaximumHp / 8) : action.Power == 0 ? Math.Max(1, target.MaximumHp / 8) : target.MaximumHp;
                    candidates.Add((action, target, 0, revived, null));
                    continue;
                }
                int removed = BitOperations.PopCount((uint)(removable.Saved & ~1)) + BitOperations.PopCount(removable.Battle);
                BattleDamageForecast? healing = target.IsAlive && target.CurrentHp < target.MaximumHp ? ForecastRecoveryMagic(actor, target, action) : null;
                if (removed > 0 || healing?.ExpectedDamage > 0)
                {
                    candidates.Add((action, target, removed, 0, healing));
                }
            }
        }
        var revival = candidates.Where(candidate => candidate.Revived > 0).OrderByDescending(candidate => candidate.Revived).ThenBy(candidate => candidate.Target.NativeIndex).ThenBy(candidate => candidate.Action.Id).FirstOrDefault();
        if (revival.Action is not null)
        {
            return new(revival.Action.Id, revival.Target.Id, revival.Revived, 1);
        }
        var status = candidates.Where(candidate => candidate.Removed > 0).OrderByDescending(candidate => candidate.Removed).ThenBy(candidate => candidate.Target.NativeIndex).ThenBy(candidate => candidate.Action.Id).FirstOrDefault();
        if (status.Action is not null)
        {
            return new(status.Action.Id, status.Target.Id);
        }
        var injured = candidates.Where(candidate => candidate.Healing?.ExpectedDamage > 0).OrderByDescending(candidate => candidate.Target.MaximumHp - candidate.Target.CurrentHp).ThenBy(candidate => candidate.Target.NativeIndex).FirstOrDefault();
        if (injured.Action is null)
        {
            return null;
        }
        var heals = candidates.Where(candidate => candidate.Target.Id == injured.Target.Id && candidate.Healing?.ExpectedDamage > 0).ToArray();
        var reliable = heals.Where(candidate => candidate.Healing!.KillProbability >= 0.8).OrderBy(candidate => candidate.Healing!.ExpectedDamage).ThenBy(candidate => candidate.Action.Id).ToArray();
        var chosen = reliable.Length > 0 ? reliable[0] : heals.OrderByDescending(candidate => candidate.Healing!.ExpectedDamage).ThenBy(candidate => candidate.Action.Id).First();
        return new(chosen.Action.Id, chosen.Target.Id, chosen.Healing!.ExpectedDamage, chosen.Healing.KillProbability);
    }

    private BattleDamageForecast? ForecastRecoveryMagic(BattleCombatant actor, BattleCombatant target, BattleActionDefinition action)
    {
        if (action.NativeAction is not { } native)
        {
            if (action.Effect is not (BattleActionEffect.Heal or BattleActionEffect.FullHeal))
            {
                return null;
            }
            return BattleActionForecaster.FromNativePhases(draw => action.Effect == BattleActionEffect.FullHeal ? target.MaximumHp * action.Power / 16 : Math.Min(9_999, NativeBattleDamage.Cure(actor.Magic, action.Power, draw())), target.MaximumHp - target.CurrentHp);
        }
        if (native.AttackType is not (3 or 4 or 21 or 25 or 32))
        {
            return null;
        }
        ushort saved = NativeSavedStatusBits(target);
        uint statuses = NativeBattleStatusBits(target);
        bool bypass = (native.Statuses1 & 0x4000000) != 0;
        if ((saved & 0x40) != 0 || native.AttackType is 3 or 21 && ((saved & 4) != 0 || !bypass && ((saved & 1) != 0 || (statuses & 0x200000) != 0 || (native.Element & 8) != 0 && (statuses & 0x2000) != 0)))
        {
            return null;
        }
        return BattleActionForecaster.FromNativePhases(draw =>
        {
            int damage;
            if (native.AttackType is 4 or 25 or 32)
            {
                if ((native.Hit ?? 255) <= draw() % 100 + 1)
                {
                    return 0;
                }
                damage = native.AttackType == 32 ? -native.Power * target.MaximumHp / 16 : native.AttackType == 25 ? -(actor.MaximumHp - ReadNativeRecoverSourceHp(actor)) : -50 * native.Power;
                if ((native.AttackFlags & 3) == 2 && actor.NativeIndex < 3)
                {
                    uint passives = actor.NativePlayerStatRules?.NativeStatusPassiveFlags ?? throw new NotSupportedException("Native item healing requires the equipped character-ability flags.");
                    if ((passives & 2) != 0)
                    {
                        damage *= 2;
                    }
                }
                _ = draw();
            }
            else
            {
                damage = native.AttackType == 3 ? -NativeBattleDamage.Cure(actor.Magic, native.Power, draw()) : -native.Power * target.MaximumHp / 16;
            }
            if (native.AttackType is 3 or 21 && (statuses & 0x40) != 0)
            {
                damage >>= 1;
            }
            if (native.Command == 2 && (NativeBattleStatusBits(actor) & 0x2000000) != 0)
            {
                damage *= 5;
            }
            int maximum = (native.AttackFlags & 8) != 0 ? 60_000 : 9_999;
            return -Math.Clamp(damage, -maximum, NativePrimaryDamageLimit(native.AttackFlags));
        }, target.MaximumHp - target.CurrentHp);
    }
}
