using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayChoiceWorkflow
{
    // Reviewed native Glacier Map acquisition strategy (NG-106).
    private const string GlacierMapProgram = "22963A38445DD562AE4218135B8F54F47CC4296895D72BAD22BE9DBEC9ADADC1";

    internal static bool IsNativeGlacierMapPrompt(Ff7SessionSave source, Ff7AutoPlayState state, Ff7Dialogue dialogue)
    {
        return source.Vm.ProgramIdentity == GlacierMapProgram && source.World is null && source.Battle is null && state.Active is { Accepted: true, Returned: false, Entity: 3, Script: 4, Kind: Ff7AutoPlayTargetKind.LineTouch } active && Ff7AutoPlayExecutionOwners.Matches(active, source.FieldGeneration, GlacierMapProgram) && source.Process.PlayerLocked && source.Process.MenuDisabled && source.Process.GlobalCommand == Ff7GlobalCommand.None && source.Process.Transition is null && source.Process.Join is null && source.Process.Movie is not { Playing: true } && dialogue is { Window: 0, Text: 12, FirstChoice: 0, LastChoice: 1, Completed: false } && ReadOwnedServicePrompt(source, active, dialogue.RequestId) is { Entity: 3, Priority: 1, ProgramCounter: 0x0498 } && ReadNativeChoiceOwner(source, dialogue) is not null;
    }

    internal static bool HasNativeGlacierMapControlReturn(Ff7SessionSave source, Ff7AutoPlayState state, Ff7AutoPlayChoiceState choice, int? selection, ReadOnlySpan<byte> script)
    {
        if (selection != 1 || source.Vm.ProgramIdentity != GlacierMapProgram || source.World is not null || source.Battle is not null || state.Active is not { Accepted: true, Returned: true, Entity: 3, Script: 4, Kind: Ff7AutoPlayTargetKind.LineTouch, ExecutionProvenance.RootReturned: true } active || !Ff7AutoPlayExecutionOwners.Matches(active, source.FieldGeneration, GlacierMapProgram) || !MatchesChoiceSource(choice, active) || choice.NativeOwner is not { Entity: 3, Priority: 1, ProgramCounter: 0x0498 } question || question.Generation != source.FieldGeneration || question.ProgramIdentity != GlacierMapProgram || source.Process.PlayerLocked || source.Process.MenuDisabled || source.Process.GlobalCommand != Ff7GlobalCommand.None || source.Process.Transition is not null || source.Process.Join is not null || source.Process.Movie is { Playing: true } || source.Process.Menu is { Completed: false } || source.Process.Dialogues.Any(dialogue => !dialogue.Completed) || source.Vm.Entities.Length <= 4 || source.Vm.Entities[3].Priority != 7 || source.Vm.Entities[3].SavedProgramCounters.Any(pc => pc != 0) || source.Vm.Entities[3].SyncStates.Any(sync => sync != FieldSyncState.None) || source.Vm.Entities[3].SyncOwners.Any(owner => owner != -1))
        {
            return false;
        }
        foreach (Ff7AutoPlayExecutionOwner owner in active.ExecutionProvenance.Owners)
        {
            if (!Ff7AutoPlayPolicy.CanSettleSupersededPlayerAnimation(active, source.FieldGeneration, source.Vm, source.Process, owner, script))
            {
                return false;
            }
        }
        var variables = new FieldVariables(source.Vm.PersistentVariables, source.Vm.MapVariables);
        return variables.ReadByte(FieldBank.MapByte, 0x0A) == 1 && (variables.ReadByte(FieldBank.Persistent0Byte, 0x45) & 0x10) != 0 && (variables.ReadByte(FieldBank.Persistent0Byte, 0x82) & 0x40) != 0;
    }
}
