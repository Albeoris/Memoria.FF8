namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private byte ExpandedAngeloLearned(BattleCombatant actor)
    {
        // 48FE20 LABEL83 divides by the acquired-GF count before dispatching any 240 formula.
        return actor is { IsPlayer: true, ModelId: 4, IsActive: true } && HasAcquiredGuardianForce &&
            (definition.NativeSavedBattleFlagsKnownMask & 0x10) != 0 && !NativeAngeloDisabled()
            ? (byte)((definition.LimitProgress?.AngeloFlags ?? 0) & 15) : (byte)0;
    }

    private static bool IsExpandedAngeloAction(BattleNativeAction? action)
    {
        return action is { Command: 240, KernelSection: 9, HitCount: 1 } && (action switch
        {
            { ActionId: 11, AttackType: 1, TargetFlags: 0x59 } => true,
            { ActionId: 12, AttackType: 32, TargetFlags: 0x19 } => true,
            { ActionId: 13, AttackType: 5, TargetFlags: 0x19 } => true,
            { ActionId: 14, AttackType: 23, TargetFlags: 0x19 } => true,
            _ => false
        });
    }

    private static bool IsExpandedIndependentSupport(BattleNativeAction? action, BattleNativePlayerReaction? reaction)
    {
        return IsExpandedAutomaticGuardian(action, reaction) || IsExpandedAngeloAction(action) && reaction is
            BattleNativePlayerReaction.AngeloRush or BattleNativePlayerReaction.AngeloRecover or BattleNativePlayerReaction.AngeloReverse or BattleNativePlayerReaction.AngeloPeriodic;
    }

    private static bool IsExpandedAngeloTarget(BattleCombatant target, BattleNativeAction action)
    {
        return target.IsActive && !target.Untargetable && (action.ActionId == 11 ? !target.IsPlayer : target.IsPlayer) && target.IsAlive == (action.ActionId != 13);
    }

    private void RunExpandedAngeloReaction(BattleCombatant actor, byte stage)
    {
        if (stage == 3)
        {
            QueueExpandedAngelo(actor, actor, BattleNativePlayerReaction.AngeloReverse, 13);
            return;
        }
        if ((NativeSavedStatusBits(actor) & 5) != 0 || (NativeBattleStatusBits(actor) & 0x4009) != 0 || (actor.NativeCombatantFlags & 0x4000) != 0 ||
            actor.LastNativeHealing != 0 || actor.LastAttackerId is not string attackerId || GetActor(attackerId) is not { IsPlayer: false } attacker)
        {
            return;
        }
        byte learned = ExpandedAngeloLearned(actor);
        if ((learned & 2) != 0 && (NativeSavedStatusBits(actor) & 0x100) != 0 && NextRandomByte() <= 16)
        {
            QueueExpandedAngelo(actor, actor, BattleNativePlayerReaction.AngeloRecover, 12);
        }
        else if ((learned & 1) != 0 && NextRandomByte() <= 16)
        {
            QueueExpandedAngelo(actor, attacker, BattleNativePlayerReaction.AngeloRush, 11);
        }
    }

    private void QueueExpandedAngelo(BattleCombatant actor, BattleCombatant target, BattleNativePlayerReaction reaction, byte id)
    {
        BattleNativeAction action = FindInstalledNativeAction(240, id);
        if (!IsExpandedAngeloAction(action))
        {
            throw new NotSupportedException("The installed Angelo reaction lacks an expanded contact contract.");
        }
        if (!IsExpandedAngeloTarget(target, action))
        {
            return;
        }
        actions.Enqueue(new(actor.Id, target.Id, SelectBattleTarget(target, allowsKnockedOutAllies: id == 13), BattleAbility.EnemyAbility, id)
        {
            NativePlayerReaction = reaction,
            NativeQueuedCommand = 240,
            NativeAction = action,
            NativeSequence = NativeBattleActionGraphics.ResolvePlayerSequence(240, 0, id, definition.NativeCommandSequences)
        });
        actor.IsBusy = true;
        Log("Queued", actor.Id, target.Id, id, $"{actor.Name}: {action.Name} queued.");
    }

    private void RunExpandedPeriodicAngelo()
    {
        BattleCombatant? rinoa = allies.FirstOrDefault(actor => actor is { IsActive: true, ModelId: 4 });
        if (rinoa is null)
        {
            return;
        }
        byte learned = ExpandedAngeloLearned(rinoa);
        if ((learned & 2) != 0 && allies.Any(actor => actor != rinoa && actor.IsActive && !actor.Untargetable && (NativeSavedStatusBits(actor) & 0x101) == 0x100) && NextRandomByte() <= 8)
        {
            Queue(12, PickTarget(8));
            return;
        }
        if ((learned & 4) != 0)
        {
            if (!rinoa.Untargetable && (NativeSavedStatusBits(rinoa) & 1) != 0 && NextRandomByte() <= 8)
            {
                Queue(13, rinoa);
                return;
            }
            if (allies.Any(actor => actor != rinoa && actor.IsActive && !actor.Untargetable && (NativeSavedStatusBits(actor) & 1) != 0) && NextRandomByte() <= 2)
            {
                Queue(13, PickTarget(0));
                return;
            }
        }
        if ((learned & 8) != 0 && !rinoa.Untargetable && (NativeSavedStatusBits(rinoa) & 5) == 0 && (NativeBattleStatusBits(rinoa) & 0x4009) == 0 && NextRandomByte() <= 8)
        {
            Queue(14, rinoa);
        }

        BattleCombatant PickTarget(byte criterion)
        {
            var filter = NativeAiTargetFilter(allies.ToArray(), 200, 0, criterion);
            while (true)
            {
                // 486E70 rejection-samples the side's slots, including ineligible entries.
                BattleCombatant candidate = allies[NextRandomByte() % allies.Count];
                if (!candidate.Untargetable && filter.Eligible(candidate))
                {
                    return candidate;
                }
            }
        }

        void Queue(byte id, BattleCombatant target)
        {
            nativeSupport = nativeSupport! with { SelectedAction = id };
            QueueExpandedAngelo(NativeSupportActor(), target, BattleNativePlayerReaction.AngeloPeriodic, id);
        }
    }
}
