namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeLimitGraphicsCallback(NativeBattleGraphicsState graphics, uint callback)
    {
        if (callback is 0x48D1A0 or 0x4AD7C0)
        {
            if (nativeShotProcesses.Count != 1)
            {
                throw new InvalidDataException("Native Shot readiness requires its single current widget owner.");
            }
            (string actorId, BattleNativeShotState state) = nativeShotProcesses.Single();
            if (state.Phase != BattleNativeShotPhase.Ending)
            {
                nativeShotProcesses[actorId] = state with { Phase = BattleNativeShotPhase.Ready };
            }
            return graphics;
        }
        if (callback is not (0x48E5A0 or 0x48E5E0) || nativeDuelProcesses.Count != 1)
        {
            throw new InvalidDataException("Native Duel readiness requires its single current widget owner.");
        }
        (string duelActorId, BattleNativeDuelState duel) = nativeDuelProcesses.Single();
        nativeDuelProcesses[duelActorId] = ResetNativeDuelInput(duel);
        return graphics;
    }

    private static BattleNativeDuelState ResetNativeDuelInput(BattleNativeDuelState state)
    {
        return state with { Phase = BattleNativeDuelPhase.Ready, InputRing = System.Collections.Immutable.ImmutableArray.Create<ushort>(0, 0, 0, 0, 0, 0, 0, 0), InputCursor = 0, AutomaticCountdown = 120, PendingPulse = 0 };
    }
}
