namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private static bool IsDuelCallback(NativeLimitCallback? callback)
    {
        return callback is NativeLimitCallback.DuelOpening or NativeLimitCallback.DuelEffect or NativeLimitCallback.DuelFinisher;
    }

    private bool HasQueuedExpandedTimedLimit => actions.Any(command => command.NativeRenzokuken is not null || command.NativeDuel is not null);

    private static bool IsExpandedDuelTarget(BattleCombatant target)
    {
        return !target.IsPlayer && target.IsActive && target.IsAlive && !target.Untargetable;
    }

    private void QueueExpandedDuelInvocation(BattleCombatant actor, BattleCombatant target, byte crisis)
    {
        NativeLimitProgress progress = RequireNativeDuelProgress();
        if (!IsExpandedDuelTarget(target) || ExpandedTimedLimitOwnsInput || HasQueuedExpandedTimedLimit)
        {
            throw new InvalidOperationException("Duel requires a living enemy and no other timed Limit owner.");
        }
        if ((progress.Options978!.Value & 2) != 0 && nativeBattleUiRandom is null)
        {
            throw new NotSupportedException("Automatic Duel requires its actual4A7080 battle UI random owner.");
        }
        if (NativeBattleDuelRules.Timer(actor.NativeLimit!, crisis) <= 0)
        {
            throw new InvalidDataException("Native Duel timer cannot be zero.");
        }
        for (ushort id = 0; id < 10; id++)
        {
            BattleNativeAction move = FindInstalledNativeAction(id < 6 ? (byte)241 : (byte)239, id);
            if (!IsExpandedDuelMove(move))
            {
                throw new NotSupportedException("Duel requires its installed physical moves and hostile targets.");
            }
        }
        OnNativePlayerCommandSubmitted(actor, 11);
        actions.Enqueue(new(actor.Id, target.Id, SelectBattleTarget(target, eligibility: BattleTargetEligibility.Living), BattleAbility.LimitBreak, 11 << 16) { NativeQueuedCommand = 11, NativeDuel = new(crisis) });
        actor.Atb = 0;
        actor.IsBusy = true;
        Log("Queued", actor.Id, target.Id, 0, $"{actor.Name}: Duel queued.");
    }

    private static bool IsExpandedDuelMove(BattleNativeAction? action)
    {
        return action is { KernelSection: 22, HitCount: 1, AttackType: 1 or 7, TargetFlags: 0x40 or 0x50 or 0x58 } &&
            (action is { Command: 241, ActionId: < 6 } || action is { Command: 239, ActionId: >= 6 and <= 9 });
    }

    private static string ReadExpandedDuelTarget(BattleNativeDuelState state)
    {
        return state.TargetRequest.Selection is BattleTargetSelection.Explicit { Actors: [BattleEnemyActorReference target] }
            ? target.ActorId
            : throw new InvalidDataException("Expanded Duel requires its single bound enemy target.");
    }

    private bool ValidateExpandedDuelCallback(string actorId, BattleNativeDuelState duel, NativeLimitCallback callback, string targetId, BattleNativeAction? action)
    {
        BattleNativeDuelPhase phase = callback switch
        {
            NativeLimitCallback.DuelOpening => BattleNativeDuelPhase.Opening,
            NativeLimitCallback.DuelEffect => BattleNativeDuelPhase.AwaitingEffect,
            _ => BattleNativeDuelPhase.Finisher
        };
        return ReadExpandedDuelTarget(duel) == targetId && duel.Phase == phase && IsExpandedDuelMove(action) &&
            action!.ActionId == NativeBattleDuelRules.ActionId(GetActor(actorId).NativeLimit!, duel.MoveSetId) &&
            action.Command == (phase == BattleNativeDuelPhase.Finisher ? 239 : 241);
    }

    private void ValidateExpandedDuelOwners()
    {
        BattleAttackCommand[] queuedInvocations = actions.Where(command => command.NativeDuel is not null).ToArray();
        if (queuedInvocations.Length > 1 || queuedInvocations.Length == 1 &&
            (ExpandedTimedLimitOwnsInput || actions.Any(command => command.NativeRenzokuken is not null) ||
            queuedInvocations[0].NativeDuel!.CrisisLevel != nativeLimitLevels.GetValueOrDefault(queuedInvocations[0].ActorId)))
        {
            throw new InvalidDataException("Saved Duel invocation disagrees with its selected crisis or timed input owner.");
        }
        int queuedCallbacks = actions.Count(command => IsDuelCallback(command.NativeLimitCallback));
        int activeCallbacks = IsDuelCallback(ActiveAction?.NativeLimitCallback) ? 1 : 0;
        if (expandedHostedEvent is { Command.NativeDuel: not null } hosted)
        {
            if (queuedCallbacks + activeCallbacks != 0 || nativeDuelProcesses.Count != 1 || !nativeDuelProcesses.ContainsKey(hosted.Command.ActorId) ||
                !OwnsHostedDuelRestore(hosted, hosted.Command.ActorId))
            {
                throw new InvalidDataException("Hosted Duel recovery contradicts its stable event and single widget.");
            }
            return;
        }
        if (queuedCallbacks + activeCallbacks > 1 || nativeDuelProcesses.Count != 0 && ActiveAction is not null && activeCallbacks == 0)
        {
            throw new InvalidDataException("Saved Duel phase contradicts its single action callback owner.");
        }
    }

    private void StartExpandedDuelInvocation(BattleCombatant actor, BattleAttackCommand command)
    {
        BattlePresentationProcess process = Presentation ?? throw new InvalidOperationException("Duel requires its current presentation process.");
        PrepareAndPublishExpandedHostedDuel(actor, command, process);
    }

    private void ReconcileExpandedDuel()
    {
        if (nativeDuelProcesses.Count != 0 && !CanNativeBattleActorAct(GetActor(nativeDuelProcesses.Single().Key)))
        {
            QueueNativeDuelCloseSelection(nativeDuelProcesses.Single().Key);
        }
    }

}
