using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayChoiceWorkflow
{
    // TODO(TD-037): Replace the reviewed native tutorial-cycle strategy (NG-119) with a proved general branch classifier.
    private const string SubmarineManualProgram = "56FA50A22C8DFDA10D2E5000DE0C55A8246523A36D1D437B61809FB7D502D019";

    internal static bool IsNativeSubmarineManualPrompt(Ff7SessionSave source, Ff7AutoPlayState state, Ff7Dialogue dialogue)
    {
        if (source.Vm.ProgramIdentity != SubmarineManualProgram || source.World is not null || source.Battle is not null || ReadStory(source) != 1283 || state.Active is not { Accepted: true, Returned: false, Entity: 17, Script: 4, Kind: Ff7AutoPlayTargetKind.LineTouch } active || !Ff7AutoPlayExecutionOwners.Matches(active, source.FieldGeneration, SubmarineManualProgram) || source.Vm.Entities.Length <= 19 || !source.Process.PlayerLocked || !source.Process.MenuDisabled || source.Process.GlobalCommand != Ff7GlobalCommand.None || source.Process.Transition is not null || source.Process.Join is not null || source.Process.Movie is { Playing: true } || dialogue is not { Window: 0, Text: 37, FirstChoice: 2, LastChoice: 3, Completed: false } || ReadNativeChoiceOwner(source, dialogue) is not { Entity: 19, Priority: 6, ProgramCounter: 0x147D } || source.Vm.MapVariables[0x17] != 2)
        {
            return false;
        }
        FieldEntitySnapshot root = source.Vm.Entities[17];
        FieldEntitySnapshot worker = source.Vm.Entities[19];
        FieldEntitySnapshot manual = source.Vm.Entities[9];
        return root is { Priority: 1, ProgramCounter: 0x10EE } && worker.SyncStates[6] == FieldSyncState.Waiting && worker.SyncOwners[6] == 17 && ReadOwnedServicePrompt(source, active, dialogue.RequestId) is { Entity: 19, Priority: 6, ProgramCounter: 0x147D } && active.ExecutionProvenance!.Owners.Any(owner => !owner.Queued && owner.Entity == 19 && owner.Priority == 6 && owner.Script == 14 && owner.Entry == 0x13BF && owner.ParentEntity == 17 && owner.ParentPriority == 1 && owner.SourceProgramCounter == 0x10EE) && !active.ExecutionProvenance.Owners.Any(owner => owner.Entity == 9) && manual.Priority == 7 && manual.SyncStates.All(sync => sync == FieldSyncState.None) && !(source.Vm.PendingRequests ?? []).Any(request => request.Entity == 9) && !source.Process.Dialogues.Any(current => current.Window == 1 && !current.Completed) && !source.Process.Motions.Any(motion => motion.Entity == 9 && !motion.Completed);
    }

    internal static bool CanAbandonNativeSubmarineManualCycle(Ff7SessionSave source, Ff7AutoPlayState state, Ff7Dialogue dialogue, Ff7AutoPlayChoiceState choice)
    {
        return IsNativeSubmarineManualPrompt(source, state, dialogue) && state.Active is { } active && MatchesChoiceSource(choice, active) && choice.NativeOwner is { Entity: 19, Priority: 6, ProgramCounter: 0x147D } previous && previous.Generation == source.FieldGeneration && previous.ProgramIdentity == SubmarineManualProgram && previous.RequestId != dialogue.RequestId && previous.SavedProgramCounters.SequenceEqual(source.Vm.Entities[19].SavedProgramCounters);
    }
}
