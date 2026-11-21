using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayChoiceWorkflow
{
    // Reviewed native Holzoff repeat-tutorial exit strategy (NG-108).
    private const string HolzoffProgram = "088F313665B33A89B5EF05D2704B3107817DC5C59D9BA77F21BCF7D55D6249C8";

    internal static bool IsNativeHolzoffExitPrompt(Ff7SessionSave source, Ff7AutoPlayState state, Ff7Dialogue dialogue)
    {
        if (source.Vm.ProgramIdentity != HolzoffProgram || source.World is not null || source.Battle is not null || state.PendingRestore is not null || state.Active is { Accepted: true } || source.Vm.Entities.Length <= 14 || !source.Process.PlayerLocked || !source.Process.MenuDisabled || source.Process.GlobalCommand != Ff7GlobalCommand.None || source.Process.Transition is not null || source.Process.Join is not null || source.Process.Movie is { Playing: true } || source.Process.Menu is { Completed: false } || dialogue is not { Window: 0, Text: 11, FirstChoice: 1, LastChoice: 2, Completed: false } || ReadNativeChoiceOwner(source, dialogue) is not { Entity: 1, Priority: 7, ProgramCounter: 0x0517 } || (source.Vm.PersistentVariables[0x83] & 0xC0) != 0x40)
        {
            return false;
        }
        return IsHolzoffDirector(source.Vm.Entities[1], 0x0517) && !source.Process.Dialogues.Any(item => !item.Completed && item.RequestId != dialogue.RequestId);
    }

    internal static bool CanAbandonNativeHolzoffCycle(Ff7SessionSave source, Ff7AutoPlayState state, Ff7Dialogue dialogue, Ff7AutoPlayChoiceState choice)
    {
        return IsNativeHolzoffExitPrompt(source, state, dialogue) && choice.NativeSourceOwned == false && choice.NativeOwner is { Entity: 1, Priority: 7, ProgramCounter: 0x0517 } previous && previous.Generation == source.FieldGeneration && previous.ProgramIdentity == HolzoffProgram && previous.RequestId != dialogue.RequestId && previous.SavedProgramCounters.SequenceEqual(source.Vm.Entities[1].SavedProgramCounters);
    }

    internal static bool HasNativeHolzoffControlReturn(Ff7SessionSave source, Ff7AutoPlayState state, Ff7AutoPlayChoiceState choice, int? selection)
    {
        if (selection != 2 || source.Vm.ProgramIdentity != HolzoffProgram || source.World is not null || source.Battle is not null || state.PendingRestore is not null || choice.NativeSourceOwned != false || choice.NativeOwner is not { Entity: 1, Priority: 7, ProgramCounter: 0x0517 } owner || owner.Generation != source.FieldGeneration || owner.ProgramIdentity != HolzoffProgram || source.Vm.Entities.Length <= 14 || !IsHolzoffDirector(source.Vm.Entities[1], 0x05C2) || !owner.SavedProgramCounters.SequenceEqual(source.Vm.Entities[1].SavedProgramCounters) || (source.Vm.PersistentVariables[0x83] & 0xC0) != 0xC0 || source.Process.PlayerLocked || source.Process.MenuDisabled || source.Process.GlobalCommand != Ff7GlobalCommand.None || source.Process.Transition is not null || source.Process.Join is not null || source.Process.Movie is { Playing: true } || source.Process.Menu is { Completed: false } || source.Process.Dialogues.Any(dialogue => !dialogue.Completed) || (source.Vm.PendingRequests ?? []).Any(request => request.Priority != 7 || request.Entity == 1) || source.Vm.Entities.Any(entity => entity.Priority != 7 || entity.SyncStates.Any(sync => sync != FieldSyncState.None) || entity.SyncOwners.Any(parent => parent != -1)))
        {
            return false;
        }
        return source.Process.Receipts.Any(receipt => receipt.Request.Id == owner.RequestId && receipt.Request.Entity == 1 && receipt.Request.Priority == 7 && receipt.Request.ProgramCounter == 0x0517 && receipt.Request.Command is FieldDialogueCommand { Window: 0, Text: 11, FirstChoice: 1, LastChoice: 2 } && receipt.Reply is { Completed: true, Value: 2 });
    }

    private static bool IsHolzoffDirector(FieldEntitySnapshot director, int programCounter)
    {
        return director.Priority == 7 && director.ProgramCounter == programCounter && director.SavedProgramCounters.All(pc => pc == 0) && director.SyncStates.All(sync => sync == FieldSyncState.None) && director.SyncOwners.All(owner => owner == -1);
    }
}
