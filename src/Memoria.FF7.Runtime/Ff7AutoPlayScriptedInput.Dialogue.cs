using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayScriptedInput
{
    internal Ff7AutoplayInput? ChooseDialogueClosingInput()
    {
        CurrentDemand = null;
        Ff7FieldProcess process = session.Host.Process;
        if (session.World is not null || session.Battle is not null || session.HasPendingTick || session.AutoPlayState?.Active is not { Accepted: true, ExecutionProvenance: not null } || !process.Dialogues.Any(dialogue => !dialogue.Completed && dialogue.AwaitingInput && dialogue.Presentation is { NativeState: 6 }) || process.Dialogues.Any(dialogue => !dialogue.Completed && session.Host.CanConfirmDialogue(dialogue.RequestId)))
        {
            return null;
        }
        FieldVmSnapshot vm = session.Machine.Capture();
        IReadOnlyList<FieldScriptInputDemand> demands = session.Machine.ReadCurrentInputDemands();
        foreach (Ff7Dialogue dialogue in process.Dialogues)
        {
            foreach (FieldScriptInputDemand demand in demands)
            {
                if (!IsOwnedDialogueInput(vm, process, session.AutoPlayState?.Active, session.FieldGeneration, dialogue, demand))
                {
                    continue;
                }
                Ff7AutoplayInput action = SelectInput(demand, session.Machine.Input);
                FieldInputState input = ProjectSpatialInput(action.Held, session.Machine.Input);
                if (session.Machine.CanCloseDialogueFromCurrentInput(demand, dialogue.Window, input))
                {
                    CurrentDemand = demand;
                    return action;
                }
            }
        }
        return null;
    }

    internal static bool IsOwnedDialogueInput(FieldVmSnapshot vm, Ff7FieldProcess process, Ff7AutoPlayActiveEvent? active, long generation, Ff7Dialogue dialogue, FieldScriptInputDemand demand)
    {
        if (active is not { Accepted: true, ExecutionProvenance: { } provenance } || !Ff7AutoPlayExecutionOwners.Matches(active, generation, vm.ProgramIdentity) || process.GlobalCommand != Ff7GlobalCommand.None || process.Transition is not null || process.Join is not null || process.Menu is { Completed: false } || process.Movie is { Playing: true } || dialogue.Completed || !dialogue.AwaitingInput || dialogue.FirstChoice is not null || dialogue.LastChoice is not null || dialogue.Presentation is not { NativeState: 6, ConfirmPending: false } || !process.Dialogues.Contains(dialogue) || !process.Windows.Any(window => window.Id == dialogue.Window && (window.Flags & 1) != 0) || !demand.AwaitingInput || demand.Mask == 0 || demand.ProgramIdentity != vm.ProgramIdentity || !IsInteractionInputOwner(demand, vm, active, generation))
        {
            return false;
        }
        FieldHostRequest? request = vm.PendingRequests?.SingleOrDefault(request => request.Id == dialogue.RequestId && request.Command is FieldDialogueCommand { FirstChoice: null, LastChoice: null } command && command.Window == dialogue.Window && command.Text == dialogue.Text);
        return request is not null && (uint)request.Entity < vm.Entities.Length && vm.Entities[request.Entity].Priority == request.Priority && vm.Entities[request.Entity].ProgramCounter == request.ProgramCounter && provenance.Owners.Any(owner => !owner.Queued && owner.Entity == request.Entity && owner.Priority == request.Priority) && provenance.Owners.Any(owner => !owner.Queued && owner.Entity == demand.Entity && owner.Priority == demand.Priority && owner.ParentEntity == request.Entity && owner.ParentPriority == request.Priority);
    }
}
