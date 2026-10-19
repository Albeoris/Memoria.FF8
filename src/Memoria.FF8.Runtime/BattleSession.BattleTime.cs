namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private HashSet<string> AdvanceTurnBasedTime(bool explicitAdvance = false)
    {
        HashSet<string> becameReady = [];
        Frame = checked(Frame + 1);
        if (HasNativeUiServiceClock)
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
        AdvanceBattleTime(becameReady, !HasNativeUiServiceClock);
        DispatchNativeStatusTimers();
        DispatchNativePeriodicSupport(true, explicitAdvance);
        CancelDeadActions();
        CompleteBattleIfNeeded();
        return becameReady;
    }

    private void AdvanceBattleTime(HashSet<string> becameReady, bool advanceNativeGauge)
    {
        foreach (BattleCombatant actor in combatants.ToArray())
        {
            if (advanceNativeGauge)
            {
                AdvanceNativeAtbParticipant(actor, becameReady);
            }
            if (!IsNativeBattleCapable(actor) || actor.Suspended)
            {
                continue;
            }
            int ratePercent = 100;
            foreach (BattleStatusState effect in actor.Effects.ToArray())
            {
                IBattleStatusEffectHandler handler = handlers[effect.Definition.Kind];
                effect.UntilTickFrames--;
                bool periodicTick = effect.UntilTickFrames == 0;
                handler.Dispatch(new BattleEffectContext(actor, effect.Definition, Frame, periodicTick, amount => ApplyDamage(null, actor, amount, effect.Definition.Kind.ToString()), amount => ApplyHealing(actor, amount, effect.Definition.Kind.ToString())));
                ratePercent = checked(ratePercent * handler.AtbRatePercent / 100);
                if (periodicTick)
                {
                    effect.UntilTickFrames = effect.Definition.TickIntervalFrames;
                }
                if (!effect.Definition.Permanent)
                {
                    effect.RemainingFrames--;
                }
                if (!effect.Definition.Permanent && effect.RemainingFrames == 0)
                {
                    actor.Effects.Remove(effect);
                    Log("StatusExpired", actor.Id, actor.Id, 0, $"{actor.Name}: {effect.Definition.Kind} expired.");
                }
                if (!actor.IsAlive)
                {
                    break;
                }
            }
            if (actor.NativeAtb is null && CanNativeBattleActorAct(actor) && !actor.IsBusy && !actor.IsReady)
            {
                if ((actor.NativeStatus1 & 2) != 0)
                {
                    ratePercent = ratePercent * 3 / 2;
                }
                if ((actor.NativeStatus1 & 4) != 0)
                {
                    ratePercent /= 2;
                }
                // Scaffold gauge growth: Speed units per dispatched frame, modified by active statuses.
                int gain = CalculateAtbGain(actor.Speed, ratePercent);
                actor.Atb = Math.Min(AtbMaximum, actor.Atb + gain);
                if (actor.IsReady)
                {
                    becameReady.Add(actor.Id);
                    Log("Ready", actor.Id, null, 0, $"{actor.Name} is ready.");
                }
            }
        }
        if (advanceNativeGauge)
        {
            DispatchGuardianForceCasts();
        }
    }
}
