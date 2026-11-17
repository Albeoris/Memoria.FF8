namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private HashSet<string> AdvanceTurnBasedTime(bool explicitAdvance = false)
    {
        HashSet<string> becameReady = [];
        Frame = checked(Frame + 1);
        if (HasPolledNativeBattleClock)
        {
            AdvanceNativeHostTime();
            // The native host polls ATB/GF four times per status/support update.
            // These logical polls never advance models, packets, input or UI animation.
            for (int poll = 0; poll < 4; poll++)
            {
                NativeBattleClockState clock = nativeBattleClock!;
                // Explicit pass advances combat time during an action without releasing its presentation pause owner.
                NativeBattleClockState admitted = NativeBattleClockRules.Admit(explicitAdvance ? clock with { ActionPause = 0 } : clock, true, false, NativeScriptAllocationPaused);
                nativeBattleClock = clock with { AtbAdmitted = admitted.AtbAdmitted };
                if (admitted.AtbAdmitted == 0)
                {
                    break;
                }
                foreach (BattleCombatant actor in combatants)
                {
                    AdvanceNativeAtbParticipant(actor, becameReady);
                }
                DispatchGuardianForceCasts();
                if (becameReady.Count != 0 || !explicitAdvance && HasPendingTurn || HasGuardianForceTimeBoundary)
                {
                    break;
                }
            }
        }
        AdvanceBattleTime(becameReady, !HasPolledNativeBattleClock);
        DispatchNativeStatusTimers();
        DispatchNativePeriodicSupport(true, explicitAdvance);
        CancelDeadActions();
        CompleteBattleIfNeeded();
        return becameReady;
    }

    private void AdvanceBattleTime(HashSet<string> becameReady, bool advanceNativeGauge)
    {
        AdvanceGuardianFormTime();
        if (advanceNativeGauge)
        {
            foreach (BattleCombatant actor in combatants)
            {
                AdvanceNativeAtbParticipant(actor, becameReady);
            }
        }
    }
}
