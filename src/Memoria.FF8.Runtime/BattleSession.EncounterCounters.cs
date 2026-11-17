namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private sealed record EncounterImmediateCounter(BattleCombatant Actor, NativeContactWorking? Contact, BattleActionPresentation? Action, bool Suppressed, bool Death, bool Secondary);
    private sealed record EncounterCounterCommand(BattleAttackCommand Command, NativeBattleActionProductionContext? Context, ExpandedAiInvocationReservation Source);
    private readonly Queue<EncounterCounterCommand> encounterCounterCommands = [];
    private bool capturingEncounterCounterCommands;
    private bool flushingEncounterCounterCommands;

    private void RunEncounterImmediateCounter(BattleCombatant actor, bool death = false)
    {
        if (encounterHitTransaction is { } transaction && committingEncounterHp)
        {
            transaction.Counters.Add(new(actor, nativeContactWorking, resolvingNativeAction, suppressNativeImmediateCounter, death, nativeContactWorking?.CommittingSecondary == true));
            return;
        }
        RunNativeAi(actor, 4);
        if (death)
        {
            MarkExpandedDeathContactMirror(actor, nativeContactWorking?.CommittingSecondary == true);
        }
    }

    private void MarkExpandedDeathContactMirror(BattleCombatant actor, bool secondary)
    {
        // 494410 clears KO after stage 4 and before 47E2D0 mirrors the contact.
        // The attached scheduler retains logical KO until the deferred stage 3 handoff.
        if (nativeContactWorking is { } contact)
        {
            if (secondary && actor == contact.Actor)
            {
                contact.SecondaryDeathCallbackClearsKoMirror = true;
            }
            else if (!secondary && actor == contact.Recipient)
            {
                contact.PrimaryDeathCallbackClearsKoMirror = true;
            }
        }
    }

    private void RunDeferredEncounterCounters(EncounterHitTransaction transaction)
    {
        NativeContactWorking? previousContact = nativeContactWorking;
        BattleActionPresentation? previousAction = resolvingNativeAction;
        bool previousSuppression = suppressNativeImmediateCounter;
        bool previousCapture = capturingEncounterCounterCommands;
        capturingEncounterCounterCommands = true;
        try
        {
            foreach (EncounterImmediateCounter invocation in transaction.Counters)
            {
                nativeContactWorking = invocation.Contact;
                resolvingNativeAction = invocation.Action;
                suppressNativeImmediateCounter = invocation.Suppressed;
                if (invocation.Death)
                {
                    invocation.Actor.NativeStatus0 |= 1;
                }
                RunNativeAi(invocation.Actor, 4);
                if (invocation.Death)
                {
                    MarkExpandedDeathContactMirror(invocation.Actor, invocation.Secondary);
                }
                if (invocation.Contact is { EncounterRedirectedDamage: true } zero)
                {
                    zero.Sequence = 0;
                    zero.Flags = 0;
                    zero.HitFlags = 0;
                }
            }
        }
        finally
        {
            nativeContactWorking = previousContact;
            resolvingNativeAction = previousAction;
            suppressNativeImmediateCounter = previousSuppression;
            capturingEncounterCounterCommands = previousCapture;
        }
    }

    private bool CaptureEncounterCounterCommand(BattleAttackCommand command)
    {
        if (!capturingEncounterCounterCommands)
        {
            return false;
        }
        ExpandedAiInvocationReservation source = producingExpandedAiInvocation
            ?? throw new InvalidDataException("A captured encounter counter requires its current typed invocation reservation.");
        encounterCounterCommands.Enqueue(new(command, nativeProductionContext, source));
        return true;
    }

    private void FlushEncounterCounterCommands()
    {
        if (capturingEncounterCounterCommands || flushingEncounterCounterCommands || encounterHitTransaction is not null || committingEncounterHp)
        {
            return;
        }
        flushingEncounterCounterCommands = true;
        int produced = 0;
        try
        {
            while (encounterCounterCommands.TryPeek(out EncounterCounterCommand? pending))
            {
                if (!IsStagedExpandedAiInvocation(pending.Source))
                {
                    return;
                }
                if (++produced > 16)
                {
                    throw new NotSupportedException("Encounter immediate-counter commands exceed the native bounded callback stack.");
                }
                encounterCounterCommands.Dequeue();
                NativeBattleActionProductionContext? previousContext = nativeProductionContext;
                byte? previousStage = nativeAiCallbackStage;
                bool previousExpandedProduction = producingExpandedAiCallback;
                nativeProductionContext = pending.Context;
                nativeAiCallbackStage = pending.Command.NativeAiCallbackStage;
                try
                {
                    producingExpandedAiCallback = true;
                    ProduceExpandedAiCallbackCommand(pending.Command);
                }
                finally
                {
                    nativeProductionContext = previousContext;
                    nativeAiCallbackStage = previousStage;
                    producingExpandedAiCallback = previousExpandedProduction;
                }
            }
        }
        finally
        {
            flushingEncounterCounterCommands = false;
        }
    }
}
