using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayChoiceWorkflow
{
    // Reviewed native Condor tutorial exit strategy (NG-118).
    private const string CondorTutorialProgram = "F42D2939525C9597ABE30149964C7EE8AEAFEB6B7A43E708A3F9E508122AE642";

    internal static bool IsNativeCondorTutorialExitPrompt(Ff7SessionSave source, Ff7AutoPlayState state, Ff7Dialogue dialogue)
    {
        if (source.Vm.ProgramIdentity != CondorTutorialProgram || source.World is not null || source.Battle is not null || ReadStory(source) != 1110 || state.Active is not { Accepted: true, Returned: false, Entity: 22, Script: 1, Kind: Ff7AutoPlayTargetKind.ActorTalk } active || !Ff7AutoPlayExecutionOwners.Matches(active, source.FieldGeneration, CondorTutorialProgram) || source.Vm.Entities.Length <= 22 || source.Process.GlobalCommand != Ff7GlobalCommand.None || source.Process.Transition is not null || source.Process.Join is not null || source.Process.Movie is { Playing: true } || dialogue is not { Window: 1, Text: 13, FirstChoice: 1, LastChoice: 6, Completed: false } || ReadNativeChoiceOwner(source, dialogue) is not { Entity: 10, Priority: 5, ProgramCounter: 0x0ACB })
        {
            return false;
        }
        FieldEntitySnapshot parent = source.Vm.Entities[22];
        FieldEntitySnapshot worker = source.Vm.Entities[10];
        return parent is { Priority: 1, ProgramCounter: 0x1D5C } && worker.SyncStates[5] == FieldSyncState.Waiting && worker.SyncOwners[5] == 22 && ReadOwnedServicePrompt(source, active, dialogue.RequestId) is { Entity: 10, Priority: 5, ProgramCounter: 0x0ACB } && active.ExecutionProvenance!.Owners.Any(owner => !owner.Queued && owner.Entity == 10 && owner.Priority == 5 && owner.Script == 1 && owner.Entry == 0x0AB2 && owner.ParentEntity == 22 && owner.ParentPriority == 1 && owner.SourceProgramCounter == 0x1D5C);
    }

    internal static bool CanAbandonNativeCondorTutorialCycle(Ff7SessionSave source, Ff7AutoPlayState state, Ff7Dialogue dialogue, Ff7AutoPlayChoiceState choice)
    {
        return IsNativeCondorTutorialExitPrompt(source, state, dialogue) && state.Active is { } active && MatchesChoiceSource(choice, active) && choice.NativeOwner is { Entity: 10, Priority: 5, ProgramCounter: 0x0ACB } previous && previous.Generation == source.FieldGeneration && previous.ProgramIdentity == CondorTutorialProgram && previous.RequestId != dialogue.RequestId && previous.SavedProgramCounters.SequenceEqual(source.Vm.Entities[10].SavedProgramCounters);
    }
}

