namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private sealed record EncounterImmediateCounter(BattleCombatant Actor, NativeContactWorking? Contact, BattleActionPresentation? Action, bool Suppressed, bool Death);
    private sealed record EncounterCounterCommand(BattleCombatant Actor, BattleAttackCommand Command, string Name, bool PlayerInput, ushort? DiagnosticMask, BattleActionHeaderReference? SourceHeader, NativeBattleActionProductionContext? Context);
    private readonly Queue<EncounterCounterCommand> encounterCounterCommands = [];
    private bool capturingEncounterCounterCommands;
    private bool flushingEncounterCounterCommands;
    private BattleActionHeaderReference? encounterProducingHeaderIndex;
    private BattleActionHeaderReference? encounterProducingCommandFirstHeader;

    private void RunEncounterImmediateCounter(BattleCombatant actor, bool death = false)
    {
        if (encounterHitTransaction is { } transaction && committingEncounterHp)
        {
            transaction.Counters.Add(new(actor, nativeContactWorking, resolvingNativeAction, suppressNativeImmediateCounter, death));
            return;
        }
        RunNativeAi(actor, 4);
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
                try
                {
                    RunNativeAi(invocation.Actor, 4);
                }
                finally
                {
                    if (invocation.Death)
                    {
                        invocation.Actor.NativeStatus0 &= 0xFFFE;
                    }
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

    private bool CaptureEncounterCounterCommand(BattleCombatant actor, BattleAttackCommand command, string name, bool playerInput, ushort? diagnosticMask)
    {
        if (!capturingEncounterCounterCommands)
        {
            return false;
        }
        encounterCounterCommands.Enqueue(new(actor, command, name, playerInput, diagnosticMask, encounterProducingCommandFirstHeader ?? encounterProducingHeaderIndex ?? resolvingNativeAction?.NativeHeader, nativeProductionContext));
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
                if (pending.SourceHeader is BattleActionHeaderReference header && (!nativeActionPackets.HasPublishedHeader(header) || ActiveAction?.NativeHeader != header && !actions.Any(command => command.NativeHeader == header)))
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
                nativeProductionContext = pending.Context;
                nativeAiCallbackStage = pending.Command.NativeAiCallbackStage;
                try
                {
                    BattleAttackCommand command = pending.Command;
                    if (pending.SourceHeader is not null)
                    {
                        byte sourceIndex = nativeActionScratch.EventActorB0F ?? throw new InvalidDataException("Deferred counter lacks its native source-event actor.");
                        BattleCombatant sourceActor = combatants.Single(actor => actor.EncodeNativeSlot() == sourceIndex);
                        BattleModelOwner sourceOwner = CreatePresentationModelOwner(sourceActor);
                        BattleActionHeaderReference? sourceHeader = nativeActionPackets.PublishedHeaders().Where(header => header.Slot.Source == sourceOwner).Select(header => (BattleActionHeaderReference?)header.Reference).FirstOrDefault();
                        if (sourceHeader is null)
                        {
                            throw new InvalidDataException("Deferred counter lacks its accepted source-event header.");
                        }
                        command = command with { NativeEventOwner = new(sourceActor.Id, sourceHeader.Value) };
                    }
                    EnqueueNativePacket(pending.Actor, command, pending.Name, pending.PlayerInput, pending.DiagnosticMask);
                }
                finally
                {
                    nativeProductionContext = previousContext;
                    nativeAiCallbackStage = previousStage;
                }
            }
        }
        finally
        {
            flushingEncounterCounterCommands = false;
        }
    }
}
