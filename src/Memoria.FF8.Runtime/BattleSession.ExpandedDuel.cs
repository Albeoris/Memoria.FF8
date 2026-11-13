namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private static bool IsDuelCallback(NativeLimitCallback? callback)
    {
        return callback is NativeLimitCallback.DuelOpening or NativeLimitCallback.DuelEffect or NativeLimitCallback.DuelFinisher;
    }

    private bool HasQueuedExpandedTimedLimit => UsesExpandedRoster && actions.Any(command => command.NativeRenzokuken is not null || command.NativeDuel is not null);

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
        actions.Enqueue(new(actor.Id, target.Id, BattleAbility.LimitBreak, 11 << 16) { NativeQueuedCommand = 11, NativeDuel = new(crisis) });
        actor.Atb = 0;
        actor.IsBusy = true;
        Log("Queued", actor.Id, target.Id, 0, $"{actor.Name}: Duel queued.");
    }

    private static bool IsExpandedDuelMove(BattleNativeAction? action)
    {
        return action is { KernelSection: 22, HitCount: 1, AttackType: 1 or 7, TargetFlags: 0x40 or 0x50 or 0x58 } &&
            (action is { Command: 241, ActionId: < 6 } || action is { Command: 239, ActionId: >= 6 and <= 9 });
    }

    private bool ValidateExpandedDuelCallback(string actorId, BattleNativeDuelState duel, NativeLimitCallback callback, string targetId, BattleNativeAction? action)
    {
        BattleNativeDuelPhase phase = callback switch
        {
            NativeLimitCallback.DuelOpening => BattleNativeDuelPhase.Opening,
            NativeLimitCallback.DuelEffect => BattleNativeDuelPhase.AwaitingEffect,
            _ => BattleNativeDuelPhase.Finisher
        };
        return duel.ExpandedTargetId == targetId && duel.Phase == phase && IsExpandedDuelMove(action) &&
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
        if (queuedCallbacks + activeCallbacks > 1 || nativeDuelProcesses.Count != 0 && ActiveAction is not null && activeCallbacks == 0)
        {
            throw new InvalidDataException("Saved Duel phase contradicts its single action callback owner.");
        }
    }

    private void StartExpandedDuelInvocation(BattleCombatant actor, BattleAttackCommand command)
    {
        BattleCombatant target = GetActor(command.TargetId);
        if (!CanNativeBattleActorAct(actor) || !IsExpandedDuelTarget(target))
        {
            actor.IsBusy = false;
            nativeLimitLevels.Remove(actor.Id);
            Log("Cancelled", actor.Id, target.Id, 0, "Duel has no available original target.");
            return;
        }
        byte crisis = command.NativeDuel!.CrisisLevel;
        BattleNativeLimitDefinition rules = actor.NativeLimit!;
        byte moveSet = NativeBattleDuelRules.InitialMoveSet(rules, crisis);
        short timer = NativeBattleDuelRules.Timer(rules, crisis);
        BattleNativeAction opening = FindInstalledNativeAction(241, NativeBattleDuelRules.ActionId(rules, moveSet));
        nativeDuelProcesses.Add(actor.Id, new(moveSet, 0, timer, timer, BattleNativeDuelPhase.Opening) { ExpandedTargetId = target.Id, Ui = new(BattleTimedInputUiPhase.Opening) });
        BeginNativeReactionBatch();
        StartExpandedDuelMove(ExpandedDuelMove(actor.Id, target.Id, opening, NativeLimitCallback.DuelOpening));
    }

    private static BattleAttackCommand ExpandedDuelMove(string actorId, string targetId, BattleNativeAction move, NativeLimitCallback callback)
    {
        return new(actorId, targetId, BattleAbility.LimitBreak, move.Command << 16 | move.ActionId)
        {
            NativeQueuedCommand = move.Command,
            NativeAction = move,
            NativeLimitCallback = callback
        };
    }

    private void QueueExpandedDuelMove(BattleAttackCommand move)
    {
        BattleAttackCommand[] waiting = actions.ToArray();
        actions.Clear();
        actions.Enqueue(move);
        foreach (BattleAttackCommand command in waiting)
        {
            actions.Enqueue(command);
        }
    }

    private void StartExpandedDuelMove(BattleAttackCommand command)
    {
        ActiveAction = NativeCommandPresentation(command) with { RemainingFrames = NativeActionWindupFrames(GetActor(command.ActorId), command.NativeSequence) };
        Log("Windup", command.ActorId, command.TargetId, 0, command.NativeAction!.Name ?? "Duel");
    }

    private void CompleteExpandedDuelMove(BattleActionPresentation command)
    {
        BattleCombatant actor = GetActor(command.ActorId);
        BattleCombatant target = GetActor(command.TargetId);
        BattleNativeAction move = command.NativeAction!;
        bool finisher = command.NativeLimitCallback == NativeLimitCallback.DuelFinisher;
        if (finisher)
        {
            ApplyExpandedDuelControl(actor, target, command, 65530, move.ActionId);
        }
        if (CanNativeBattleActorAct(actor))
        {
            BattleCombatant[] recipients = (move.TargetFlags!.Value & 0x10) == 0 ? enemies.Where(IsExpandedDuelTarget).ToArray() : IsExpandedDuelTarget(target) ? [target] : [];
            foreach (BattleCombatant recipient in recipients)
            {
                ApplyExpandedDuelContact(actor, recipient, command, move, deferred: !finisher, publish: true);
            }
        }
        if (finisher)
        {
            CompleteExpandedDuelOwners(actor);
        }
        CompleteActionPresentation(command, finisher ? "Duel finished." : "Duel move completed.");
    }

    private void CompleteExpandedDuelTimeout(string actorId)
    {
        BattleCombatant actor = GetActor(actorId);
        BattleCombatant target = GetActor(nativeDuelProcesses[actorId].ExpandedTargetId!);
        BattleActionPresentation command = new(actorId, target.Id, BattleActionPhase.Recovery, 0, BattleAbility.LimitBreak, 11 << 16);
        ApplyExpandedDuelControl(actor, target, command, 65532, 0);
        CompleteExpandedDuelOwners(actor);
        EndNativeDuel(actorId);
        CompleteBattleIfNeeded();
    }

    private void ReconcileExpandedDuel()
    {
        if (!UsesExpandedRoster || nativeDuelProcesses.Count == 0)
        {
            return;
        }
        string actorId = nativeDuelProcesses.Single().Key;
        if (CanNativeBattleActorAct(GetActor(actorId)))
        {
            return;
        }
        if (ActiveAction?.ActorId == actorId && IsDuelCallback(ActiveAction.NativeLimitCallback))
        {
            ActiveAction = null;
        }
        BattleAttackCommand[] waiting = actions.Where(command => command.ActorId != actorId || !IsDuelCallback(command.NativeLimitCallback)).ToArray();
        actions.Clear();
        foreach (BattleAttackCommand command in waiting)
        {
            actions.Enqueue(command);
        }
        CompleteExpandedDuelTimeout(actorId);
    }

    private void ApplyExpandedDuelControl(BattleCombatant actor, BattleCombatant target, BattleActionPresentation command, ushort action, ushort effect)
    {
        if (IsExpandedDuelTarget(target))
        {
            ApplyExpandedDuelContact(actor, target, command with { NativeQueuedCommand = 241 }, CreateNativeDuelControl241(action, effect), deferred: action == 65530, publish: false);
        }
    }

    private void ApplyExpandedDuelContact(BattleCombatant actor, BattleCombatant target, BattleActionPresentation command, BattleNativeAction move, bool deferred, bool publish)
    {
        BattleActionPresentation? previous = resolvingNativeAction;
        bool previousSuppression = suppressNativeImmediateCounter;
        nativeActionScratch = nativeActionScratch with { Command = move.Command, Action = move.ActionId, EventLane = 1, DeferredEffectsE00 = deferred };
        BattleActionPresentation contact = command with { NativeAction = move };
        BattleActionDefinition action = new(command.Ability, command.ActionId, move.Name ?? "Duel", BattleActionEffect.Native, move.Power, false) { NativeAction = move };
        resolvingNativeAction = contact;
        suppressNativeImmediateCounter = deferred;
        target.NativeRemainingHits = 1;
        nativeActionCommitDepth++;
        try
        {
            NativeBattleContactPacket packet = ResolveInstalledNativeHitWithContact(actor, target, contact, action, move, 255, presentationRow: 0);
            if (publish)
            {
                PublishExpandedImpacts(contact, [new(target.Id, packet)]);
            }
        }
        finally
        {
            nativeActionCommitDepth--;
            suppressNativeImmediateCounter = previousSuppression;
            resolvingNativeAction = previous;
        }
    }

    private void CompleteExpandedDuelOwners(BattleCombatant actor)
    {
        nativeActionScratch = nativeActionScratch with { DeferredEffectsE00 = false };
        nativeActionCommitDepth++;
        try
        {
            CompleteNativeActionStatuses(actor);
        }
        finally
        {
            nativeActionCommitDepth--;
        }
        FlushExpandedEnemyReactions();
    }
}
