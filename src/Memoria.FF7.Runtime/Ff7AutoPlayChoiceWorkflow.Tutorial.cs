using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayChoiceWorkflow
{
    // Reviewed native repeat-tutorial exit strategy (NG-105).
    private const string TutorialProgram = "A513104CED946295581FA2C752FA5E3804FC542FAF7DA93C594F3A47485F9658";

    internal static bool IsNativeTutorialExitPrompt(Ff7SessionSave source, Ff7AutoPlayState state, Ff7Dialogue dialogue)
    {
        if (source.Vm.ProgramIdentity != TutorialProgram || source.World is not null || source.Battle is not null || state.Active is not { Accepted: true, Entity: 10, Script: 2, Kind: Ff7AutoPlayTargetKind.LinePush } active || !Ff7AutoPlayExecutionOwners.Matches(active, source.FieldGeneration, TutorialProgram) || source.Vm.Entities.Length <= 25 || !source.Process.PlayerLocked || !source.Process.MenuDisabled || source.Process.GlobalCommand != Ff7GlobalCommand.None || source.Process.Transition is not null || source.Process.Join is not null || source.Process.Movie is { Playing: true } || dialogue is not { Window: 2, Text: 30, FirstChoice: 1, LastChoice: 2, Completed: false } || ReadNativeChoiceOwner(source, dialogue) is not { Entity: 8, Priority: 6, ProgramCounter: 0x0AD8 })
        {
            return false;
        }
        FieldEntitySnapshot root = source.Vm.Entities[10];
        FieldEntitySnapshot parent = source.Vm.Entities[25];
        FieldEntitySnapshot worker = source.Vm.Entities[8];
        return root is { Priority: 1, ProgramCounter: 0x0B6B } && parent is { Priority: 6, ProgramCounter: 0x156B } && parent.SyncStates[6] == FieldSyncState.Waiting && parent.SyncOwners[6] == 10 && worker.SyncStates[6] == FieldSyncState.Waiting && worker.SyncOwners[6] == 25 && active.ExecutionProvenance!.Owners.Any(owner => !owner.Queued && owner.Entity == 25 && owner.Priority == 6 && owner.Script == 3);
    }

    private static bool MatchesTutorialSource(Ff7AutoPlayChoiceState choice, Ff7AutoPlayActiveEvent active)
    {
        return choice.BeforeActive is { Accepted: true } before && Ff7AutoPlayExecutionOwners.Matches(before, active.Generation, TutorialProgram) && Ff7AutoPlayExecutionOwners.Matches(active, active.Generation, TutorialProgram) && before.VisitId == active.VisitId && before.EventId == active.EventId && before.Kind == active.Kind && before.Entity == active.Entity && before.Script == active.Script && before.ConditionFingerprint == active.ConditionFingerprint && before.ExecutionProvenance!.RootDispatchTick >= 0 && before.ExecutionProvenance.RootDispatchTick == active.ExecutionProvenance!.RootDispatchTick;
    }

    internal static bool CanAbandonNativeTutorialCycle(Ff7SessionSave source, Ff7AutoPlayState state, Ff7Dialogue dialogue, Ff7AutoPlayChoiceState choice)
    {
        return IsNativeTutorialExitPrompt(source, state, dialogue) && state.Active is { } active && MatchesTutorialSource(choice, active) && choice.NativeOwner is { Entity: 8, Priority: 6, ProgramCounter: 0x0AD8 } previous && previous.Generation == source.FieldGeneration && previous.ProgramIdentity == TutorialProgram && previous.RequestId != dialogue.RequestId && previous.SavedProgramCounters.SequenceEqual(source.Vm.Entities[8].SavedProgramCounters);
    }

    internal static bool HasNativeTutorialControlReturn(Ff7SessionSave source, Ff7AutoPlayState state, Ff7AutoPlayChoiceState choice, int? selection)
    {
        if (source.Vm.ProgramIdentity != TutorialProgram || source.World is not null || source.Battle is not null || state.Active is not { Accepted: true, Returned: true, Entity: 10, Script: 2, Kind: Ff7AutoPlayTargetKind.LinePush, ExecutionProvenance: { RootReturned: true, Owners.Length: 0 } } active || source.Vm.Entities.Length <= 25 || !Ff7AutoPlayExecutionOwners.Matches(active, source.FieldGeneration, TutorialProgram) || !MatchesTutorialSource(choice, active) || source.Process.PlayerLocked || source.Process.MenuDisabled || source.Process.GlobalCommand != Ff7GlobalCommand.None || source.Process.Transition is not null || source.Process.Join is not null || source.Process.Movie is { Playing: true } || source.Process.Dialogues.Any(dialogue => !dialogue.Completed) || (source.Vm.PendingRequests ?? []).Any(request => request.Entity is 8 or 10 or 25) || new[] { 8, 10, 25 }.Any(entity => source.Vm.Entities[entity].Priority != 7 || source.Vm.Entities[entity].SyncStates.Any(sync => sync != FieldSyncState.None) || source.Vm.Entities[entity].SyncOwners.Any(owner => owner != -1)))
        {
            return false;
        }
        return selection == 2 && choice.NativeOwner is { Entity: 8, Priority: 6, ProgramCounter: 0x0AD8 } owner && owner.Generation == source.FieldGeneration && owner.ProgramIdentity == TutorialProgram;
    }
}
