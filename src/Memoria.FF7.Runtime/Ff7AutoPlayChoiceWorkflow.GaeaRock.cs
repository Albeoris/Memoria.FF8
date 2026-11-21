using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayChoiceWorkflow
{
    // Reviewed native cave rock prerequisite choice (NG-110).
    internal static bool IsNativeRockPrompt(Ff7SessionSave source, Ff7AutoPlayState state, Ff7Dialogue dialogue)
    {
        return state.DestinationField == 692 && source.Vm.ProgramIdentity == Ff7AutoPlayPolicy.GaeaRockProgram && source.World is null && source.Battle is null && state.PendingRestore is null && state.Active is { Accepted: true, Returned: false, Entity: 10, Script: 1, Kind: Ff7AutoPlayTargetKind.ActorTalk } active && Ff7AutoPlayExecutionOwners.Matches(active, source.FieldGeneration, source.Vm.ProgramIdentity) && source.Process.GlobalCommand == Ff7GlobalCommand.None && source.Process.Transition is null && source.Process.Join is null && source.Process.Movie is not { Playing: true } && dialogue is { Window: 0, Text: 1, FirstChoice: 1, LastChoice: 2, Completed: false } && ReadOwnedServicePrompt(source, active, dialogue.RequestId) is { Entity: 10, Priority: 1, ProgramCounter: 0x628 } && ReadNativeChoiceOwner(source, dialogue) is not null && (new FieldVariables(source.Vm.PersistentVariables, source.Vm.MapVariables).ReadByte(FieldBank.Persistent0Byte, 0x83) & 2) == 0;
    }

    internal static bool HasNativeRockControlReturn(Ff7SessionSave source, Ff7AutoPlayState state, Ff7AutoPlayChoiceState choice, int? selection)
    {
        if (selection != 1 || state.DestinationField != 692 || source.Vm.ProgramIdentity != Ff7AutoPlayPolicy.GaeaRockProgram || source.World is not null || source.Battle is not null || state.PendingRestore is not null || choice.NativeSourceOwned != true || state.Active is not { Accepted: true, Returned: true, Entity: 10, Script: 1, Kind: Ff7AutoPlayTargetKind.ActorTalk, ExecutionProvenance: { RootReturned: true, Owners.Length: 0 } } active || !Ff7AutoPlayExecutionOwners.Matches(active, source.FieldGeneration, source.Vm.ProgramIdentity) || !MatchesChoiceSource(choice, active) || choice.NativeOwner is not { Entity: 10, Priority: 1, ProgramCounter: 0x628 } owner || owner.Generation != source.FieldGeneration || owner.ProgramIdentity != source.Vm.ProgramIdentity || source.Process.PlayerLocked || source.Process.MenuDisabled || source.Process.BattlesDisabled || source.Process.GlobalCommand != Ff7GlobalCommand.None || source.Process.Transition is not null || source.Process.Join is not null || source.Process.Movie is { Playing: true } || source.Process.Menu is { Completed: false } || source.Process.Dialogues.Any(dialogue => !dialogue.Completed) || (source.Vm.PendingRequests ?? []).Any(request => request.Priority != 7) || source.Vm.Entities.Length <= 14 || source.Vm.Entities[10].Priority != 7 || source.Vm.Entities[10].SavedProgramCounters.Any(pc => pc != 0) || source.Vm.Entities[10].SyncStates.Any(sync => sync != FieldSyncState.None) || source.Vm.Entities[10].SyncOwners.Any(parent => parent != -1))
        {
            return false;
        }
        if ((new FieldVariables(source.Vm.PersistentVariables, source.Vm.MapVariables).ReadByte(FieldBank.Persistent0Byte, 0x83) & 2) == 0 || Enumerable.Range(10, 5).Any(entity => !source.Process.Actors.Any(actor => actor.EntityBound && actor.Entity == entity && !actor.CollisionEnabled && !actor.Visible)))
        {
            return false;
        }
        return source.Process.Receipts.Any(receipt => receipt.Request.Id == owner.RequestId && receipt.Request.Entity == 10 && receipt.Request.Priority == 1 && receipt.Request.ProgramCounter == 0x628 && receipt.Request.Command is FieldDialogueCommand { Window: 0, Text: 1, FirstChoice: 1, LastChoice: 2 } && receipt.Reply is { Completed: true, Value: 1 });
    }
}
