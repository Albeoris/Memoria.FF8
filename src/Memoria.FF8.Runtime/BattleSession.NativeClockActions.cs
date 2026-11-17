namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void BeginNativeBattleActionPause()
    {
        if (nativeBattleClock is not null)
        {
            SetNativeBattleActionPause(true);
        }
    }

    private bool OwnsExpandedBattleClock => OwnsExpandedHostedInvocation || expandedTurnClockObservation is not null;

    private void CompleteExpandedHostedActionPause()
    {
        if (OwnsExpandedBattleClock && nativeBattleClock is not null)
        {
            SetNativeBattleActionPause(false);
        }
    }

    internal void ActivateExpandedHostedClock()
    {
        if (nativeBattleClock is not null && Outcome == BattleOutcome.Active)
        {
            SetNativeBattleClockActive(true);
        }
    }

    internal static bool UsesNativeOrdinaryCompletion(BattleActionPresentation action)
    {
        if (action.ExpandedAiCallback is { } callback)
        {
            return callback.Stage == 1;
        }
        return action.NativeAiCallbackStage == 1 || action.NativeAiCallbackStage is null &&
            action.NativePlayerReaction is null && action.NativeQueuedCommand is not (0 or 246 or 247);
    }

    private void CompleteExpandedHostedEventGauge(BattleActionPresentation action)
    {
        BattleCombatant actor = GetActor(action.ActorId);
        if (nativeBattleClock is not null && actor.NativeAtb is not null && UsesNativeOrdinaryCompletion(action))
        {
            actor.Atb = 0;
            actor.NativeCombatantFlags &= ~0xCu;
        }
    }

    private void QueueNativeActionClockCompletion(uint callback, string? eventActorId = null, byte? eventActorIndex = null)
    {
            nativeActionScratch = nativeActionScratch with { ComponentCompletion = callback };
            TryCompleteNativeComponentCommand(eventActorId, eventActorIndex);
    }



    private void AcknowledgeNativeEventPresentations(BattleActionPresentation? presentation, BattleAttackCommand[] queuedPresentations)
    {
        //One original event can publish several104 headers before its single final10.
        //Remove only those current mirrors, before computing the actor's remaining busy owner.
        if (queuedPresentations.Length != 0)
        {
            HashSet<BattleAttackCommand> acknowledged = new(queuedPresentations, ReferenceEqualityComparer.Instance);
            BattleAttackCommand[] remaining = actions.Where(command => !acknowledged.Contains(command)).ToArray();
            actions.Clear();
            foreach (BattleAttackCommand command in remaining)
            {
                actions.Enqueue(command);
            }
        }
        if (presentation is not null && ReferenceEquals(ActiveAction, presentation))
        {
            CompleteActionPresentation(presentation, "Native action event completed.");
        }
        foreach (BattleAttackCommand command in queuedPresentations)
        {
            CompleteActionPresentation(NativeCommandPresentation(command), "Native action event completed.", false);
        }
    }

    private bool HasCurrentNativeQueuedPresentation()
    {
        if (!actions.TryPeek(out BattleAttackCommand? command) || command.NativeHeader is null)
        {
            return false;
        }
        ValidateCurrentNativeQueuedHeader(command);
        return true;
    }

    private void ValidateCurrentNativeQueuedHeader(BattleAttackCommand command)
    {
        BattleActionHeaderReference header = command.NativeHeader ?? throw new InvalidDataException("Native queued presentation lacks its factual header.");
        ValidateNativeHeaderReference(GetActor(command.ActorId), header);
        //47DD90 resets only the counts; a queued104 still owns its physical prepared slot.
    }

    private BattleAttackCommand[] CaptureNativeQueuedEventPresentations()
    {
        List<BattleAttackCommand> captured = [];
        byte? eventActor = null;
        foreach (BattleAttackCommand command in actions)
        {
            if (command.NativeHeader is null)
            {
                continue;
            }
            eventActor ??= nativeActionScratch.EventActorB0F ?? throw new NotSupportedException("Native queued completion requires its actual event actor BYTE B0F.");
            if (GetActor(command.NativeEventOwner?.ActorId ?? command.ActorId).NativeActorIndex != eventActor)
            {
                continue;
            }
            //47DD90 may already have cleared C0/C1; the final10 still owns these retained physical header mirrors.
            ValidateNativeHeaderReference(GetActor(command.ActorId), command.NativeHeader);
            captured.Add(command);
        }
        return captured.ToArray();
    }

    private BattleActionPresentation? CaptureNativeEventPresentation()
    {
        if (ActiveAction is not { NativeHeader: BattleActionHeaderReference headerIndex } action)
        {
            return null;
        }
        BattleCombatant actor = GetActor(action.ActorId);
        ValidateNativeHeaderReference(actor, headerIndex);
        byte eventActor = nativeActionScratch.EventActorB0F ?? throw new NotSupportedException("Native event presentation completion requires its actual event actor BYTE B0F.");
        return GetActor(action.NativeEventOwner?.ActorId ?? action.ActorId).NativeActorIndex == eventActor ? action : null;
    }

    private void RunNativePlayerReaction(BattleCombatant actor, int stage)
    {
        if (nativeActionCommitDepth == 0)
        {
            BeginNativeReactionBatch();
        }
        bool paused = actor.IsActive && actor.NativePlayerStatRules is not null && stage is 2 or 3 && (stage == 3 || (NativeSavedStatusBits(actor) & 1) == 0);
        if (paused)
        {
            BeginNativeBattleActionPause();
        }
        InterpretNativePlayerReaction(actor, stage);
        if (paused)
        {
            QueueNativeActionClockCompletion(0x47DDA0);
        }
    }

    private void RunNativeAi(BattleCombatant actor, int callback, Action? interpret = null)
    {
        byte? physicalEventActor = null;
        if (callback == 1)
        {
            actor.AiTurnCount = unchecked((byte)(actor.AiTurnCount + 1));
        }
        if (physicalEventActor is not null && callback is >= 0 and <= 3 && nativeActionCommitDepth == 0)
        {
            BeginNativeReactionBatch();
        }
        bool paused = physicalEventActor is not null && (actor.NativeAi is not null || HasEncounterPolicy(actor)) && actor.IsActive && callback is >= 0 and <= 3 && (callback != 2 || (NativeSavedStatusBits(actor) & 1) == 0);
        if (paused)
        {
            BeginNativeBattleActionPause();
        }
        byte? previousStage = nativeAiCallbackStage;
        NativeBattleActionProductionContext? previousContext = nativeProductionContext;
        bool previousHostedProduction = producingExpandedAiCallback;
        if (physicalEventActor is not null)
        {
            nativeProductionContext ??= new(0, 0, false, callback == 1 ? 0x47DE70u : 0x47DDA0u);
        }
        else if (callback == 4 && preparingExpandedHostedAction && producingExpandedAiInvocation is not null)
        {
            producingExpandedAiCallback = true;
        }
        nativeAiCallbackStage = checked((byte)callback);
        try
        {
            if (interpret is not null)
            {
                interpret();
            }
            else if (physicalEventActor is not null || actor.NativeAi?.Scripts[callback].Length > 0 || callback == 1 && HasEncounterPolicy(actor))
            {
                InterpretNativeAi(actor, callback);
            }
        }
        finally
        {
            nativeAiCallbackStage = previousStage;
            nativeProductionContext = previousContext;
            producingExpandedAiCallback = previousHostedProduction;
        }
        if (paused)
        {
            QueueNativeActionClockCompletion(callback == 1 ? 0x47DE70u : 0x47DDA0u, actor.Id, physicalEventActor);
        }
    }
}
