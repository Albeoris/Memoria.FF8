using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private BattleDiagnosticRepairReceipt? diagnosticCounterPreparationRepair;

    private void SubscribeDiagnosticCounter(BattleSession active)
    {
        if (diagnosticDriverRecipe!.CounterCycle is null)
        {
            return;
        }
        if (world!.DiagnosticReceipt!.Preparation.Prelude is not null)
        {
            throw new NotSupportedException("Counter diagnostics require ordinary initial entry without a Prelude action.");
        }
        active.DiagnosticCounterContactPublished += ObserveDiagnosticCounterContact;
        active.DiagnosticCounterCallbackObserved += ObserveDiagnosticCounterCallback;
        active.DiagnosticCounterCommandQueued += ObserveDiagnosticCounterCommand;
    }

    private void UnsubscribeDiagnosticCounter(BattleSession active)
    {
        active.DiagnosticCounterContactPublished -= ObserveDiagnosticCounterContact;
        active.DiagnosticCounterCallbackObserved -= ObserveDiagnosticCounterCallback;
        active.DiagnosticCounterCommandQueued -= ObserveDiagnosticCounterCommand;
    }

    private void ObserveDiagnosticCounterContact(BattleDiagnosticCounterContact contact)
    {
        if (diagnosticDriver is { Finished: false, CounterCycle: { Observing: true } cycle } && contact.Primary.ActorId == cycle.Receipt.ActorId && contact.Primary.RecipientId == cycle.Receipt.TargetId && contact.Primary.QueuedCommand == 1)
        {
            cycle.ObserveContact(contact);
        }
    }

    private void ObserveDiagnosticCounterCallback(BattleDiagnosticCounterCallbackObservation callback)
    {
        if (diagnosticDriver is { Finished: false, CounterCycle: { Observing: true } cycle })
        {
            cycle.ObserveCallback(callback);
        }
    }

    private void ObserveDiagnosticCounterCommand(BattleDiagnosticCounterCommand command)
    {
        if (diagnosticDriver is { Finished: false, CounterCycle: { Observing: true } cycle } && command.ActorId == cycle.Receipt.TargetId && command.AiStage == 2)
        {
            cycle.ObserveCommand(command);
        }
    }

    private void BeginDiagnosticCounter(BattleSession active, DiagnosticBattleDriver driver)
    {
        DiagnosticCounterCycleDeclaration declaration = diagnosticDriverRecipe!.CounterCycle!;
        BattleDiagnosticCombatant[] states = active.ObserveDiagnosticCombatants().ToArray();
        BattleDiagnosticCombatant actorState = states.Single(state => state.Id == diagnosticDriverRecipe.Input.ActorId);
        BattleDiagnosticCombatant targetState = states.Single(state => state.Id == declaration.Focal.ActorId);
        BattleCombatant actor = active.Combatants.Single(combatant => combatant.Id == actorState.Id);
        BattleCombatant target = active.Combatants.Single(combatant => combatant.Id == targetState.Id);
        if (!actor.IsPlayer || !actor.IsAlive || !target.IsAlive || target.IsPlayer || target.ModelId != 84 || actorState.NativeIndex is not int actorIndex || actorIndex is < 0 or >= 3 || targetState.NativeIndex is not int targetIndex || targetIndex is < 3 or >= 7 || actor.NativeCommandSlots.IsDefaultOrEmpty || actor.NativeCommandSlots[0] != 1 || targetState.NativeAiScriptSha256 is not { Length: > 2 } scripts || !string.Equals(scripts[2], declaration.Focal.ScriptSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("Counter diagnostics require the actual ordinary Attack owner and living original model84 callback2 target.");
        }
        foreach (BattleDiagnosticCombatRepair repair in diagnosticDriverRecipe.Repairs)
        {
            BattleDiagnosticCombatant owner = states.Single(state => state.Id == repair.ActorId);
            if (owner.NativeIndex is not (>= 0 and < 3))
            {
                throw new NotSupportedException("Counter attempt repairs are restricted to declared actual party owners.");
            }
        }
        driver.BeginCounterCycle(new(driver.Execution, declaration, actorState.Id, checked((byte)actorIndex), targetState.Id, checked((byte)targetIndex), active.NativeHostCallbacks ?? throw new NotSupportedException("Counter diagnostics require the native callback owner.")));
        SubmitDiagnosticCounterAttempt(active, driver.CounterCycle!);
    }

    private void SubmitDiagnosticCounterAttempt(BattleSession active, DiagnosticCounterCycleDriver cycle)
    {
        if (!DiagnosticCommandReady(active, diagnosticDriverRecipe!))
        {
            throw new InvalidOperationException("Counter trigger requires the real party command readiness boundary.");
        }
        cycle.Submitted(active.NativeHostCallbacks ?? throw new NotSupportedException("Counter trigger requires its native callback owner."), diagnosticCounterPreparationRepair);
        diagnosticCounterPreparationRepair = null;
        ApplyBattleCommand(diagnosticDriverRecipe!.Input);
    }

    private void PrepareDiagnosticCounterRetry(BattleSession active)
    {
        diagnosticCounterPreparationRepair = null;
        if (diagnosticDriverRecipe!.Repairs.Length > 0)
        {
            diagnosticCounterPreparationRepair = active.ApplyDiagnosticCombatRepairs(diagnosticDriverRecipe.Repairs, active.NativeHostCallbacks ?? active.Frame);
            diagnosticRepairs.Add(diagnosticCounterPreparationRepair);
        }
        if (active.DiagnosticTerminalHold?.Phase == BattleDiagnosticTerminalHoldPhase.Held)
        {
            active.RearmDiagnosticTerminalHold(active.NativeHostCallbacks ?? active.Frame);
        }
    }

    private void AdvanceDiagnosticCounter(BattleSession active, DiagnosticBattleDriver driver, bool drained)
    {
        if (diagnosticDriverRecipe!.CounterCycle is null || driver.Stage is not (DiagnosticDriverStage.AwaitDispatch or DiagnosticDriverStage.Drain) || driver.CounterCycle is not { } cycle || cycle.Completed)
        {
            return;
        }
        if (cycle.Observing && drained)
        {
            cycle.Drained(active.NativeHostCallbacks ?? throw new NotSupportedException("Counter drain requires its native callback owner."));
        }
        if (cycle.Completed)
        {
            return;
        }
        if (cycle.NeedsRearm)
        {
            PrepareDiagnosticCounterRetry(active);
            cycle.Rearmed();
        }
        if (cycle.CanSubmit && drained && DiagnosticCommandReady(active, diagnosticDriverRecipe))
        {
            SubmitDiagnosticCounterAttempt(active, cycle);
        }
    }

    private void ObserveDiagnosticCounterDispatch(BattleDiagnosticEffectDispatch dispatch)
    {
        DiagnosticBattleDriver driver = diagnosticDriver!;
        if (driver.Finished)
        {
            return;
        }
        if (dispatch.ActorId != diagnosticDriverRecipe!.CounterCycle!.Focal.ActorId)
        {
            driver.ObserveDispatch(dispatch);
            return;
        }
        try
        {
            if (dispatch.NativeHeaderIndex is not int header || dispatch.NativeHeaderGeneration is not long generation)
            {
                throw new InvalidDataException("Counter dispatch lacks its actual header generation.");
            }
            NativeBattleActionPacketsState packets = diagnosticDriverBattle!.ObserveDiagnosticActionPackets(dispatch);
            driver.ObserveDispatch(dispatch, null, new(dispatch.Serial, dispatch.Callback, header, generation, packets));
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException or InvalidOperationException or ArgumentException)
        {
            driver.Fail("COUNTER_PACKET_PROOF_INVALID: " + exception.Message);
        }
    }
}
