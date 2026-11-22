using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayScriptedInput
{
    internal Ff7AutoplayInput? ChooseCounterProgressInput(FieldVmSnapshot? observed = null)
    {
        Ff7AutoPlayActiveEvent? active = session.AutoPlayState?.Active;
        Ff7FieldProcess process = session.Host.Process;
        if (session.World is not null || session.Battle is not null || session.HasPendingTick || active is not { Accepted: true, ExecutionProvenance: not null } || process.GlobalCommand != Ff7GlobalCommand.None || process.Transition is not null || process.Join is not null || process.Menu is { Completed: false } || process.Movie is { Playing: true } || process.Dialogues.Any(dialogue => !dialogue.Completed && session.Host.CanConfirmDialogue(dialogue.RequestId)))
        {
            return null;
        }
        FieldVmSnapshot vm = observed ?? session.Machine.Capture();
        if (!Ff7AutoPlayExecutionOwners.Matches(active, session.FieldGeneration, vm.ProgramIdentity))
        {
            return null;
        }
        foreach (var owner in ReadInteractionOwners(vm, active, session.FieldGeneration))
        {
            int entity = owner.Key;
            int priority = owner.Value;
            int? window = null;
            Ff7Dialogue[] dialogues = process.Dialogues.Where(dialogue => !dialogue.Completed).ToArray();
            if (dialogues.Length != 0)
            {
                if (dialogues.Length != 1 || !IsOwnedCounterDialogue(vm, process, active, session.FieldGeneration, entity, priority, dialogues[0]))
                {
                    continue;
                }
                window = dialogues[0].Window;
            }
            if (session.Machine.ReadCurrentCounterProgressInput(entity, priority, window: window) is FieldScriptInputDemand progress && session.Machine.ReadCurrentInputDemands().SingleOrDefault(demand => demand.Identity == progress.Identity && demand.AwaitingInput) is FieldScriptInputDemand selected)
            {
                CurrentDemand = selected;
                return SelectInput(selected, session.Machine.Input);
            }
        }
        return null;
    }

    internal static bool IsOwnedCounterDialogue(FieldVmSnapshot vm, Ff7FieldProcess process, Ff7AutoPlayActiveEvent? active, long generation, int entity, int priority, Ff7Dialogue dialogue)
    {
        if (active is not { Accepted: true, ExecutionProvenance: { } provenance } || !Ff7AutoPlayExecutionOwners.Matches(active, generation, vm.ProgramIdentity) || !ReadInteractionOwners(vm, active, generation).TryGetValue(entity, out int current) || current != priority || process.GlobalCommand != Ff7GlobalCommand.None || process.Transition is not null || process.Join is not null || process.Menu is { Completed: false } || process.Movie is { Playing: true } || dialogue is not { Completed: false, AwaitingInput: true, FirstChoice: null, LastChoice: null, Presentation: { NativeState: 6, ConfirmPending: false } } || !process.Dialogues.Contains(dialogue) || process.Dialogues.Any(other => !other.Completed && other != dialogue) || !process.Windows.Any(item => item.Id == dialogue.Window && (item.Flags & 1) != 0))
        {
            return false;
        }
        FieldHostRequest? request = vm.PendingRequests?.SingleOrDefault(request => request.Id == dialogue.RequestId && request.Command is FieldDialogueCommand { FirstChoice: null, LastChoice: null } command && command.Window == dialogue.Window && command.Text == dialogue.Text);
        return request is not null && (uint)request.Entity < vm.Entities.Length && vm.Entities[request.Entity].Priority == request.Priority && vm.Entities[request.Entity].ProgramCounter == request.ProgramCounter && provenance.Owners.Any(child => !child.Queued && child.Entity == request.Entity && child.Priority == request.Priority && child.ParentEntity == entity && child.ParentPriority == priority);
    }
}
