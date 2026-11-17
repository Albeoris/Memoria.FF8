using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public bool CanSelectExpandedGuardianForce(string actorId, int guardianForceId)
    {
        BattleCombatant actor = GetActor(actorId);
        BattleActionDefinition action = FindAction(BattleAbility.GuardianForce, guardianForceId);
        return actor.IsPlayer && actor.EquipmentIds.Contains(guardianForceId) && action.Available &&
            SupportsExpandedAction(action) && actor.GuardianForceCompatibility is { Count: 16 } && definition.NativeBattleSpeed is not null &&
            CanNativeBattleActorAct(actor) && (actor.NativeStatus0 & 0x35) == 0 && (actor.NativeStatus1 & 0x14000) == 0 &&
            guardianForces.GetValueOrDefault(guardianForceId) is { Acquired: true, HitPoints: > 0 } &&
            !(guardianForms?.Values.Any(form => form.GuardianForceId == guardianForceId) ?? false) &&
            !guardianForceCasts.Values.Any(cast => cast.Command.ActionId == guardianForceId) &&
            !actions.Any(command => command.Ability == BattleAbility.GuardianForce && command.ActionId == guardianForceId) &&
            !(ActiveAction is { Ability: BattleAbility.GuardianForce } active && active.ActionId == guardianForceId);
    }

    private void DispatchExpandedGuardianForceCharges()
    {
        foreach (BattleGuardianForceCast cast in guardianForceCasts.Values.ToArray())
        {
            BattleCombatant actor = GetActor(cast.ActorId);
            if ((actor.NativeCombatantFlags & 0x400) != 0)
            {
                continue;
            }
            bool summoning = (actor.NativeStatus1 & 0x80000000) != 0;
            if (summoning && cast.RemainingTicks != 0)
            {
                continue;
            }
            BattleActionDefinition action = FindAction(BattleAbility.GuardianForce, cast.Command.ActionId);
            if (summoning && cast.Command.GuardianFormInvocation is not null)
            {
                if (ActiveAction is null && !ExpandedTimedLimitOwnsInput && actions.Count == 0)
                {
                    if (expandedPresentationProcess is { } process)
                    {
                        BeginExpandedHostedGuardianFormEntry(process, actor, cast);
                    }
                    else
                    {
                        ReleaseGuardianForceCharge(actor);
                        BeginCompletedGuardianFormCharge(actor, cast);
                    }
                }
                continue;
            }
            BattleCombatant[] candidates = summoning ? ExpandedGuardianForceTargetGroup(action) : [];
            if (summoning && candidates.Length != 0)
            {
                BattleCombatant target = candidates[0];
                actions.Enqueue(cast.Command with { TargetId = target.Id });
                Log("GuardianForceReady", actor.Id, target.Id, cast.Command.ActionId, "GF charge completed.");
            }
            else
            {
                CancelGuardianForceCharge(actor, cast);
            }
            ReleaseGuardianForceCharge(actor);
        }
    }

    private BattleCombatant[] ExpandedGuardianForceTargetGroup(BattleActionDefinition action)
    {
        byte flags = action.NativeAction!.TargetFlags!.Value;
        IReadOnlyList<BattleCombatant> candidates = Roster((flags & 0x40) == 0 ? BattleSide.Allies : BattleSide.Enemies);
        return candidates.Where(candidate => candidate.IsActive && !candidate.Untargetable && HasValidTargetLifeState(action, candidate)).ToArray();
    }

    private void ValidateExpandedGuardianForceCharge(BattleCombatant actor, BattleGuardianForceCast cast)
    {
        BattleAttackCommand command = cast.Command;
        BattleActionDefinition action = FindAction(BattleAbility.GuardianForce, command.ActionId);
        if (!actor.IsPlayer || !actor.Abilities.Contains(BattleAbility.GuardianForce) || actor.GuardianForceCompatibility is not { Count: 16 } ||
            definition.NativeBattleSpeed is null || !SupportsExpandedAction(action) || !byId.ContainsKey(command.TargetId) ||
            command.NativeQueuedCommand != 254 ||
            guardianForceCasts.Values.Count(other => other.Command.ActionId == command.ActionId) != 1)
        {
            throw new InvalidDataException("Saved expanded GF charge lacks its logical caster, target or original GF action.");
        }
    }
}
