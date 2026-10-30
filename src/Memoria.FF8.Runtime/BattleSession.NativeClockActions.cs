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

    private void QueueNativeActionClockCompletion(uint callback, string? eventActorId = null, byte? eventActorIndex = null)
    {
        if (NativeGraphics is { } graphics)
        {
            NativeGraphics = QueueNativeDescriptor(graphics, 10, callback);
        }
        else
        {
            nativeActionScratch = nativeActionScratch with { ComponentCompletion = callback };
            TryCompleteNativeComponentCommand(eventActorId, eventActorIndex);
        }
    }

    private NativeBattleGraphicsState RunNativeDescriptorActionCallback(NativeBattleGraphicsState graphics, uint callback)
    {
        if (callback is not (0x47DDA0 or 0x47DE70 or 0x47E120))
        {
            throw new InvalidDataException("Native action clock callback differs from47DDA0/47DE70.");
        }
        BattleActionPresentation? presentation = CaptureNativeEventPresentation();
        BattleAttackCommand[] queuedPresentations = CaptureNativeQueuedEventPresentations();
        DiagnosticInvocationCompletion? diagnosticCompletion = CaptureDiagnosticInvocationCompletion(graphics, callback);
        NativeGraphics = graphics;
        ReleaseNativeRawRenzokukenActionOwners(callback);
        bool completed = CompleteNativeActionEvent(callback);
        bool diagnosticAcknowledged = diagnosticCompletion is { } observed && completed && (presentation is not null && ReferenceEquals(ActiveAction, presentation) && presentation.ActorId == observed.Dispatch.ActorId && presentation.NativeHeaderIndex == observed.Dispatch.NativeHeaderIndex || queuedPresentations.Any(command => command.ActorId == observed.Dispatch.ActorId && command.NativeHeaderIndex == observed.Dispatch.NativeHeaderIndex));
        if (completed)
        {
            AcknowledgeNativeEventPresentations(presentation, queuedPresentations);
        }
        ObserveDiagnosticInvocationCompletion(diagnosticCompletion, completed, diagnosticAcknowledged);
        return NativeGraphics ?? throw new InvalidDataException("Native completion lost its graphical descriptor owner.");
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
        if (!actions.TryPeek(out BattleAttackCommand? command) || command.NativeHeaderIndex is null)
        {
            return false;
        }
        ValidateCurrentNativeQueuedHeader(command);
        return true;
    }

    private void ValidateCurrentNativeQueuedHeader(BattleAttackCommand command)
    {
        int header = command.NativeHeaderIndex ?? throw new InvalidDataException("Native queued presentation lacks its factual header.");
        ValidateNativeHeaderReference(GetActor(command.ActorId), header);
        //47DD90 resets only the counts; a queued104 still owns its physical prepared slot.
    }

    private BattleAttackCommand[] CaptureNativeQueuedEventPresentations()
    {
        List<BattleAttackCommand> captured = [];
        byte? eventActor = null;
        foreach (BattleAttackCommand command in actions)
        {
            if (command.NativeHeaderIndex is null)
            {
                continue;
            }
            eventActor ??= nativeActionScratch.EventActorB0F ?? throw new NotSupportedException("Native queued completion requires its actual event actor BYTE B0F.");
            if (GetActor(command.ActorId).NativeIndex != eventActor)
            {
                continue;
            }
            //47DD90 may already have cleared C0/C1; the final10 still owns these retained physical header mirrors.
            ValidateNativeHeaderReference(GetActor(command.ActorId), command.NativeHeaderIndex);
            captured.Add(command);
        }
        return captured.ToArray();
    }

    private BattleActionPresentation? CaptureNativeEventPresentation()
    {
        if (ActiveAction is not { NativeHeaderIndex: int headerIndex } action)
        {
            return null;
        }
        BattleCombatant actor = GetActor(action.ActorId);
        ValidateNativeHeaderReference(actor, headerIndex);
        byte eventActor = nativeActionScratch.EventActorB0F ?? throw new NotSupportedException("Native event presentation completion requires its actual event actor BYTE B0F.");
        return actor.NativeIndex == eventActor ? action : null;
    }

    private void RunNativePlayerReaction(BattleCombatant actor, int stage)
    {
        if (nativeActionCommitDepth == 0 && !flushingNativeReactions)
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

    private void RunNativeAi(BattleCombatant actor, int callback)
    {
        byte eventActorIndex = checked((byte)actor.NativeIndex);
        if (callback == 1)
        {
            actor.AiTurnCount = unchecked((byte)(actor.AiTurnCount + 1));
        }
        if (callback is >= 0 and <= 3 && nativeActionCommitDepth == 0 && !flushingNativeReactions)
        {
            BeginNativeReactionBatch();
        }
        bool paused = actor.NativeAi is not null && actor.IsActive && callback is >= 0 and <= 3 && (callback != 2 || (NativeSavedStatusBits(actor) & 1) == 0);
        if (paused)
        {
            BeginNativeBattleActionPause();
        }
        byte? previousStage = nativeAiCallbackStage;
        NativeBattleActionProductionContext? previousContext = nativeProductionContext;
        nativeProductionContext ??= new(0, 0, false, callback == 1 ? 0x47DE70u : 0x47DDA0u);
        nativeAiCallbackStage = checked((byte)callback);
        try
        {
            InterpretNativeAi(actor, callback);
        }
        finally
        {
            nativeAiCallbackStage = previousStage;
            nativeProductionContext = previousContext;
        }
        if (paused)
        {
            QueueNativeActionClockCompletion(callback == 1 ? 0x47DE70u : 0x47DDA0u, actor.Id, eventActorIndex);
        }
    }
}
