using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayChoiceWorkflow
{
    // Reviewed native icicle prerequisite and lower-room handoff (NG-111).
    internal const string GaeaIcicleLowerProgram = "FD5DA773333F35F7357BEAE4010D2C1E1B4703692232C1924BF54D6EB20B8E28";

    internal static int? ReadNativeIciclePreference(Ff7SessionSave source, Ff7AutoPlayState state, Ff7Dialogue dialogue)
    {
        if (state.DestinationField != 694 || source.Vm.ProgramIdentity != Ff7AutoPlayPolicy.GaeaIcicleProgram || source.World is not null || source.Battle is not null || state.PendingRestore is not null || state.Active is not { Accepted: true, Returned: false, Entity: >= 3 and <= 5, Script: 5, Kind: Ff7AutoPlayTargetKind.LineEnter } active || !Ff7AutoPlayExecutionOwners.Matches(active, source.FieldGeneration, source.Vm.ProgramIdentity) || source.Process.GlobalCommand != Ff7GlobalCommand.None || source.Process.Transition is not null || source.Process.Join is not null || source.Process.Movie is { Playing: true } || dialogue is not { Window: 0, Text: 3, FirstChoice: 1, LastChoice: 2, Completed: false } || ReadOwnedServicePrompt(source, active, dialogue.RequestId) is not { Entity: 10, Priority: 6, ProgramCounter: 0x6BD } || ReadNativeChoiceOwner(source, dialogue) is null)
        {
            return null;
        }
        int flags = new FieldVariables(source.Vm.PersistentVariables, source.Vm.MapVariables).ReadByte(FieldBank.Persistent0Byte, 0x83);
        return (flags & 1 << (active.Entity - 1)) == 0 ? null : (flags & 0x1C) == 0x1C ? 2 : 1;
    }

    internal static bool HasNativeIcicleControlReturn(Ff7SessionSave source, Ff7AutoPlayState state, Ff7AutoPlayChoiceState choice, int? selection)
    {
        if (state.DestinationField != 694 || selection is not (1 or 2) || source.World is not null || source.Battle is not null || state.PendingRestore is not null || choice.NativeSourceOwned != true || choice.NativeOwner is not { Entity: 10, Priority: 6, ProgramCounter: 0x6BD } owner || owner.ProgramIdentity != Ff7AutoPlayPolicy.GaeaIcicleProgram || state.Active is not { Accepted: true, Entity: >= 3 and <= 5, Script: 5, Kind: Ff7AutoPlayTargetKind.LineEnter } active || owner.Generation != active.Generation || !Ff7AutoPlayExecutionOwners.Matches(active, active.Generation, Ff7AutoPlayPolicy.GaeaIcicleProgram) || !MatchesChoiceSource(choice, active) || source.Process.PlayerLocked || source.Process.MenuDisabled || source.Process.GlobalCommand != Ff7GlobalCommand.None || source.Process.Transition is not null || source.Process.Join is not null || source.Process.Movie is { Playing: true } || source.Process.Menu is { Completed: false } || source.Process.Dialogues.Any(dialogue => !dialogue.Completed))
        {
            return false;
        }
        int flags = new FieldVariables(source.Vm.PersistentVariables, source.Vm.MapVariables).ReadByte(FieldBank.Persistent0Byte, 0x83);
        if (selection == 2)
        {
            // The native MAP unloads the synchronously waiting source; its RET cannot execute.
            return (flags & 0x1C) == 0x1C && active.TransitionOwned && active.DestinationField == 693 && source.FieldGeneration > active.Generation && source.Vm.ProgramIdentity == GaeaIcicleLowerProgram && source.Process.PreviousMap == 697 && source.Process.Actors.Any(actor => actor.EntityBound && actor.PlayerControlled && actor.Entity == 20 && actor.Triangle == 106 && actor.Ladder is null && actor.Jump is null);
        }
        return (flags & 0x1C) != 0x1C && (flags & 1 << (active.Entity - 1)) != 0 && source.Vm.ProgramIdentity == Ff7AutoPlayPolicy.GaeaIcicleProgram && source.FieldGeneration == active.Generation && active is { Returned: true, ExecutionProvenance: { RootReturned: true, Owners.Length: 0 } } && (source.Vm.PendingRequests ?? []).Length == 0 && source.Process.Receipts.Any(receipt => receipt.Request.Id == owner.RequestId && receipt.Request.Entity == 10 && receipt.Request.Priority == 6 && receipt.Request.ProgramCounter == 0x6BD && receipt.Request.Command is FieldDialogueCommand { Window: 0, Text: 3, FirstChoice: 1, LastChoice: 2 } && receipt.Reply is { Completed: true, Value: 1 });
    }
}
