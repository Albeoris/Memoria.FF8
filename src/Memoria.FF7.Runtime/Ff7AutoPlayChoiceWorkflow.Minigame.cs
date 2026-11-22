using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayChoiceWorkflow
{
    internal static bool HasCompletedCommittedSource(Ff7AutoPlayState state)
    {
        if (state.PendingRestore is not null || state.SourceGoal is not { Completed: true, AcceptedEntry: int accepted } goal || accepted != goal.Entry || state.Choice is null)
        {
            return false;
        }
        Ff7AutoPlayActiveEvent? first = null;
        for (Ff7AutoPlayChoiceState? choice = state.Choice; choice is not null; choice = choice.Parent)
        {
            if (choice.NativeExecutionCommitted != true || choice.NativeSourceOwned != true || choice.BeforeActive is not { Accepted: true } source || source.VisitId != goal.VisitId || source.NavigationKey != goal.NavigationKey || source.Generation != goal.Generation || source.EventId != goal.EventId || source.ConditionFingerprint != goal.ExecutionConditionFingerprint || source.Kind != goal.Kind || source.Entity != goal.Entity || source.Script != goal.Script || !Ff7AutoPlayExecutionOwners.Matches(source, goal.Generation, goal.ProgramIdentity) || source.ExecutionProvenance is not { RootDispatchTick: >= 0 } provenance || choice.NativeOwner is not { } owner || owner.Generation != goal.Generation || owner.ProgramIdentity != goal.ProgramIdentity || first is not null && !MatchesChoiceSource(choice, first))
            {
                return false;
            }
            if (state.Active is { } current && (!current.CompletionRecorded || !MatchesChoiceSource(choice, current)))
            {
                return false;
            }
            first ??= source;
            if (!provenance.Owners.Any(owner => !owner.Queued && owner.Entity == goal.Entity && owner.Priority == 1 && owner.ParentEntity == -1 && owner.Entry == goal.Entry))
            {
                return false;
            }
            var coverage = state.Coverage.Choices.SingleOrDefault(item => item.Key == choice.Key);
            if (coverage?.ActiveOption is not int option || option < coverage.FirstOption || option >= coverage.FirstOption + coverage.OptionCount)
            {
                return false;
            }
        }
        return true;
    }

    internal static bool ObserveOwnedMinigameChoice(Ff7SessionSave source, Ff7AutoPlayState state)
    {
        if (state.PendingRestore is not null || state.Choice is null || source.World is not null || source.Battle is not null || state.Active is not { Accepted: true, Returned: false, ExecutionProvenance: { RootReturned: false } } active || !Ff7AutoPlayExecutionOwners.Matches(active, source.FieldGeneration, source.Vm.ProgramIdentity) || source.Process.GlobalCommand != Ff7GlobalCommand.Minigame || source.Process.GlobalRequestId is not long id)
        {
            return false;
        }
        Ff7HostReceipt? receipt = source.Process.Receipts.SingleOrDefault(item => item.Request.Id == id && item.Request.Command is FieldMinigameCommand && !item.Reply.Completed);
        if (receipt is null || !(source.Vm.PendingRequests ?? []).Contains(receipt.Request))
        {
            return false;
        }
        FieldHostRequest request = receipt.Request;
        if ((uint)request.Entity >= source.Vm.Entities.Length || source.Vm.Entities[request.Entity].Priority != request.Priority || source.Vm.Entities[request.Entity].ProgramCounter != request.ProgramCounter || !active.ExecutionProvenance!.Owners.Any(owner => !owner.Queued && owner.Entity == request.Entity && owner.Priority == request.Priority) || !Ff7AutoPlayScriptedInput.ReadInteractionOwners(source.Vm, active, source.FieldGeneration).TryGetValue(request.Entity, out int priority) || priority != request.Priority || source.Condor is { } condor && condor.Request != request || source.Submarine is { } submarine && submarine.Request != request)
        {
            return false;
        }
        return CommitOwnedSourceChoices(state, active, source.FieldGeneration, source.Vm.ProgramIdentity);
    }

    private static bool CommitOwnedSourceChoices(Ff7AutoPlayState state, Ff7AutoPlayActiveEvent active, long generation, string programIdentity, Func<Ff7AutoPlayChoiceState, bool>? eligible = null)
    {
        List<Ff7AutoPlayChoiceState> chain = [];
        for (Ff7AutoPlayChoiceState? choice = state.Choice; choice is not null; choice = choice.Parent)
        {
            var coverage = state.Coverage.Choices.SingleOrDefault(item => item.Key == choice.Key);
            if (!MatchesChoiceSource(choice, active) || choice.NativeOwner is not { } owner || owner.Generation != generation || owner.ProgramIdentity != programIdentity || eligible?.Invoke(choice) == false || coverage?.ActiveOption is not int option || option < coverage.FirstOption || option >= coverage.FirstOption + coverage.OptionCount)
            {
                return false;
            }
            chain.Add(choice);
        }
        Ff7AutoPlayChoiceState? parent = null;
        if (chain.All(choice => choice.NativeExecutionCommitted == true))
        {
            return true;
        }
        for (int index = chain.Count - 1; index >= 0; index--)
        {
            parent = chain[index] with { Parent = parent, NativeExecutionCommitted = true };
        }
        state.Choice = parent;
        return true;
    }
}
