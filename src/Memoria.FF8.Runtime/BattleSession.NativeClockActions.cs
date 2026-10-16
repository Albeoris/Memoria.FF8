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
        if (NativeGraphics is { } graphics)
        {
            NativeGraphics = QueueNativeDescriptor(graphics, 10, callback);
        }
        else
        {
            nativeActionScratch = nativeActionScratch with { ComponentCompletion = callback };
            TryCompleteNativeComponentCommand();
        }
    }

    private NativeBattleGraphicsState RunNativeDescriptorActionCallback(NativeBattleGraphicsState graphics, uint callback)
    {
        if (callback is not (0x47DDA0 or 0x47DE70 or 0x47E120))
        {
            throw new InvalidDataException("Native action clock callback differs from47DDA0/47DE70.");
        }
        NativeGraphics = graphics;
        CompleteNativeActionEvent(callback);
        return NativeGraphics ?? throw new InvalidDataException("Native completion lost its graphical descriptor owner.");
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
            QueueNativeActionClockCompletion(callback == 1 ? 0x47DE70u : 0x47DDA0u);
        }
    }
}
