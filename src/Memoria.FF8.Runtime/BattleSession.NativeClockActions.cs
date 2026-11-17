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

    private void RunNativeAi(BattleCombatant actor, int callback)
    {
        if (callback == 1)
        {
            actor.AiTurnCount = unchecked((byte)(actor.AiTurnCount + 1));
        }
        byte? previousStage = nativeAiCallbackStage;
        NativeBattleActionProductionContext? previousContext = nativeProductionContext;
        bool previousHostedProduction = producingExpandedAiCallback;
        if (callback == 4 && preparingExpandedHostedAction && producingExpandedAiInvocation is not null)
        {
            producingExpandedAiCallback = true;
        }
        nativeAiCallbackStage = checked((byte)callback);
        try
        {
            if (actor.NativeAi?.Scripts[callback].Length > 0 || callback == 1 && HasEncounterPolicy(actor))
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
    }
}
