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

    private void QueueNativeActionClockCompletion(uint callback)
    {
        if (nativeBattleClock is null)
        {
            return;
        }
        if (NativeGraphics is { } graphics)
        {
            NativeGraphics = QueueNativeDescriptor(graphics, 10, callback);
        }
        else
        {
            // TODO(TD-009): Components without the original descriptor bank retain the existing shared command completion boundary
            SetNativeBattleActionPause(false);
        }
    }

    private NativeBattleGraphicsState RunNativeDescriptorActionCallback(NativeBattleGraphicsState graphics, uint callback)
    {
        if (callback is not (0x47DDA0 or 0x47DE70))
        {
            throw new InvalidDataException("Native action clock callback differs from47DDA0/47DE70.");
        }
        // TODO(TD-009): Shared contact transactions currently consume inventory/poison/reactions once before this graphics completion
        // The original descriptor10 now owns the pause-DWORD release; do not duplicate those already-owned effects here.
        SetNativeBattleActionPause(false);
        nativeActionPackets = nativeActionPackets.ResetCounts();
        return graphics;
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
            QueueNativeActionClockCompletion(callback == 1 ? 0x47DE70u : 0x47DDA0u);
        }
    }
}
