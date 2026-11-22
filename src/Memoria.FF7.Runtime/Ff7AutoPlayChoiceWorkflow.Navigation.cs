using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayChoiceWorkflow
{
    internal static bool CanSettleOwnedNavigationChoice(Ff7SessionSave current, Ff7AutoPlayState state, ushort actualMap)
    {
        if (current.TickContinuation is not null || state.Active is not { CompletionRecorded: true } || state.Choice is null)
        {
            return false;
        }
        for (Ff7AutoPlayChoiceState? choice = state.Choice; choice is not null; choice = choice.Parent)
        {
            if (choice.NativeExecutionCommitted != true)
            {
                return false;
            }
        }
        return ObserveOwnedFieldTransitionChoice(current, state, actualMap);
    }

    internal static bool ObserveOwnedFieldTransitionChoice(Ff7SessionSave current, Ff7AutoPlayState state, ushort actualMap)
    {
        if (state.PendingRestore is not null || state.Choice is null || state.Active is not { Accepted: true, TransitionOwned: true, ExecutionProvenance: { AcceptedTransition: { Command: FieldTransitionCommand command } request } provenance } active || !Ff7AutoPlayExecutionOwners.Matches(active, active.Generation, provenance.ProgramIdentity) || provenance.AcceptedTransitionTick < provenance.RootDispatchTick || !provenance.Owners.Any(owner => !owner.Queued && owner.Entity == request.Entity && owner.Priority == request.Priority))
        {
            return false;
        }
        if (current.World is { } world)
        {
            if (command.Map is < 1 or > 64 || world.EntrySelector != command.Map || world.WorldId != Ff7WorldRuntime.ResolveWorldId(command.Map) || current.FieldGeneration != active.Generation || current.Vm.ProgramIdentity != provenance.ProgramIdentity || current.Process.GlobalRequestId != request.Id || !(current.Vm.PendingRequests ?? []).Contains(request) || !current.Process.Receipts.Any(receipt => receipt.Request == request && receipt.Reply.Completed) || !Ff7AutoPlayPolicy.HasCompletedFieldWorldHandoff(world, current.Vm, current.Process))
            {
                return false;
            }
            return CommitOwnedSourceChoices(state, active, active.Generation, provenance.ProgramIdentity, choice => choice.NativeNavigationRequested == true);
        }
        if (command.Map != actualMap || active.Generation == long.MaxValue || current.FieldGeneration != active.Generation + 1)
        {
            return false;
        }
        bool LegacyPaidRoute(Ff7AutoPlayChoiceState choice)
        {
            return choice.NativeNavigationRequested is null && active.TransportEntry is { Cost: > 0 } paid && choice.BeforeActive?.TransportEntry == paid && paid.DestinationField == actualMap && paid.BeforeGil >= paid.Cost && current.Progress.Gil == paid.BeforeGil - paid.Cost && state.NavigationGoal is { } goal && goal.NavigationKey == active.NavigationKey && goal.EventId == active.EventId && goal.Conditions == active.ConditionFingerprint;
        }
        return CommitOwnedSourceChoices(state, active, active.Generation, provenance.ProgramIdentity, choice => choice.NativeNavigationRequested == true || LegacyPaidRoute(choice));
    }
}
