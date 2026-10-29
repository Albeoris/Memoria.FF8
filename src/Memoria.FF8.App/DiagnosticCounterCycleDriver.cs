using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed class DiagnosticCounterCycleDriver
{
    private readonly BattleInput input;
    private bool retryRearmed;
    private bool failed;
    internal DiagnosticCounterCycleReceipt Receipt { get; private set; }
    internal bool Completed => Receipt.CompletedCallback.HasValue;
    internal bool Observing => !failed && !Completed && Receipt.Attempts.LastOrDefault() is { DrainedCallback: null };
    internal bool NeedsRearm => !failed && !Completed && Receipt.Attempts.LastOrDefault() is { DrainedCallback: not null } && !retryRearmed;
    internal bool CanSubmit => !failed && !Completed && (Receipt.Attempts.IsEmpty || retryRearmed);

    internal DiagnosticCounterCycleDriver(DiagnosticCounterCycleReceipt receipt, BattleInput input)
    {
        receipt.Declaration.Validate(input, 178, 230, 8);
        if (!receipt.Attempts.IsEmpty || receipt.CompletedCallback is not null)
        {
            throw new InvalidDataException("Counter coordinator requires a new actual cycle boundary.");
        }
        Receipt = receipt;
        this.input = input;
    }

    internal void Submitted(long callback, BattleDiagnosticRepairReceipt? preparationRepair = null)
    {
        if (!CanSubmit || Receipt.Attempts.Length >= Receipt.Declaration.MaxTriggerAttempts || callback < Receipt.StartedCallback || (Receipt.Attempts.LastOrDefault()?.DrainedCallback is long drained && drained > callback))
        {
            throw new InvalidOperationException("Counter retry requires full prior drain, declared rearm and a bounded ordinary submission.");
        }
        Receipt = Receipt with { Attempts = Receipt.Attempts.Add(new(Receipt.Attempts.Length + 1, callback) { PreparationRepair = preparationRepair }) };
        retryRearmed = false;
    }

    internal void ObserveContact(BattleDiagnosticCounterContact contact)
    {
        RequireObserving();
        Update(Current with { Contacts = Current.Contacts.Add(contact) });
    }

    internal void ObserveCallback(BattleDiagnosticCounterCallbackObservation callback)
    {
        RequireObserving();
        Update(Current with { Callbacks = Current.Callbacks.Add(callback) });
    }

    internal void ObserveCommand(BattleDiagnosticCounterCommand command)
    {
        RequireObserving();
        Update(Current with { Commands = Current.Commands.Add(command) });
    }

    internal void ObserveDispatch(BattleDiagnosticEffectDispatch dispatch, DiagnosticCounterDispatchPackets packets)
    {
        RequireObserving();
        BattleDiagnosticSupportingInvocation focal = Receipt.Declaration.Focal;
        if (Current.Dispatch is not null || dispatch.ActorId != focal.ActorId || dispatch.NativeActorIndex != Receipt.TargetIndex || dispatch.Ability != BattleAbility.EnemyAbility || dispatch.NativeCommand != focal.NativeCommand || dispatch.ActionId != focal.ActionId || dispatch.RequestedEffect != focal.EffectId || dispatch.ExecutedEffect != focal.EffectId || dispatch.NativeAiCallbackStage != focal.CallbackStage || !string.Equals(dispatch.NativeAiScriptSha256, focal.ScriptSha256, StringComparison.OrdinalIgnoreCase) || dispatch.Reflection is not null)
        {
            throw new InvalidDataException("Counter focal dispatch differs from its exact original reaction actor, stage, script or invocation.");
        }
        BattleDiagnosticCounterCommand[] queued = Current.Commands.Where(command => command.HeaderIndex == dispatch.NativeHeaderIndex && command.HeaderGeneration == dispatch.NativeHeaderGeneration && command.ActorId == focal.ActorId && command.Command == focal.NativeCommand && command.ActionId == focal.ActionId && command.EffectId == focal.EffectId).ToArray();
        if (queued.Length != 1)
        {
            throw new InvalidDataException("Counter focal dispatch lacks exactly one observed native queued command owner.");
        }
        packets.NormalizeTargetMask(dispatch, queued[0]);
        Update(Current with { Dispatch = dispatch, DispatchPackets = packets });
    }

    internal void Drained(long callback)
    {
        RequireObserving();
        if (callback < Current.SubmittedCallback)
        {
            throw new InvalidDataException("Counter drain crossed its actual submission callback.");
        }
        Update(Current with { DrainedCallback = callback });
        try
        {
            if (Current.Dispatch is not null)
            {
                DiagnosticCounterCycleReceipt completed = Receipt with { CompletedCallback = callback };
                DiagnosticCounterCycleValidation.Validate(completed, input);
                Receipt = completed;
            }
            else
            {
                DiagnosticCounterCycleValidation.Validate(Receipt, input, requireCompleted: false);
                if (Receipt.Attempts.Length >= Receipt.Declaration.MaxTriggerAttempts)
                {
                    throw new NotSupportedException("The bounded genuine attack attempts did not produce the declared counter.");
                }
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException or InvalidOperationException)
        {
            failed = true;
            throw;
        }
    }

    internal void Rearmed()
    {
        if (!NeedsRearm)
        {
            throw new InvalidOperationException("Counter rearm requires a completed nonfocal attempt at full drain.");
        }
        retryRearmed = true;
    }

    private DiagnosticCounterAttemptReceipt Current => Receipt.Attempts[^1];
    private void Update(DiagnosticCounterAttemptReceipt attempt) => Receipt = Receipt with { Attempts = Receipt.Attempts.SetItem(Receipt.Attempts.Length - 1, attempt) };

    private void RequireObserving()
    {
        if (!Observing)
        {
            throw new InvalidOperationException("Counter observation requires its actual outstanding ordinary trigger attempt.");
        }
    }
}
