using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayChoiceWorkflow
{
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
        if ((uint)request.Entity >= source.Vm.Entities.Length || source.Vm.Entities[request.Entity].Priority != request.Priority || source.Vm.Entities[request.Entity].ProgramCounter != request.ProgramCounter || !active.ExecutionProvenance!.Owners.Any(owner => !owner.Queued && owner.Entity == request.Entity && owner.Priority == request.Priority) || !Ff7AutoPlayScriptedInput.ReadInteractionOwners(source.Vm, active, source.FieldGeneration).TryGetValue(request.Entity, out int priority) || priority != request.Priority || source.Condor is { } condor && condor.Request != request)
        {
            return false;
        }
        List<Ff7AutoPlayChoiceState> chain = [];
        for (Ff7AutoPlayChoiceState? choice = state.Choice; choice is not null; choice = choice.Parent)
        {
            var coverage = state.Coverage.Choices.SingleOrDefault(item => item.Key == choice.Key);
            if (!MatchesChoiceSource(choice, active) || choice.NativeOwner is not { } owner || owner.Generation != source.FieldGeneration || owner.ProgramIdentity != source.Vm.ProgramIdentity || coverage?.ActiveOption is not int option || option < coverage.FirstOption || option >= coverage.FirstOption + coverage.OptionCount)
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
