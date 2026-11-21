using Memoria.AutoPlay;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed record Ff7AutoPlayChoiceNativeOwner(long Generation, string ProgramIdentity, long RequestId, int Entity, int Priority, int ProgramCounter, int[] SavedProgramCounters);

public sealed partial class Ff7AutoPlayChoiceWorkflow
{
    private readonly Ff7AutoPlayNativeReadiness choiceReadiness = new();

    internal static Ff7AutoPlayChoiceNativeOwner? ReadNativeChoiceOwner(Ff7SessionSave source, Ff7Dialogue dialogue)
    {
        FieldHostRequest? request = source.Vm.PendingRequests?.SingleOrDefault(request => request.Id == dialogue.RequestId && request.Command is FieldDialogueCommand command && command.FirstChoice is not null && command.LastChoice is not null && command.Window == dialogue.Window && command.Text == dialogue.Text && command.FirstChoice == dialogue.FirstChoice && command.LastChoice == dialogue.LastChoice);
        if (source.World is not null || request is null || (uint)request.Entity >= source.Vm.Entities.Length)
        {
            return null;
        }
        FieldEntitySnapshot owner = source.Vm.Entities[request.Entity];
        return owner.Priority == request.Priority && owner.ProgramCounter == request.ProgramCounter ? new(source.FieldGeneration, source.Vm.ProgramIdentity, request.Id, request.Entity, request.Priority, request.ProgramCounter, owner.SavedProgramCounters.ToArray()) : null;
    }

    internal static bool MatchesChoiceSource(Ff7AutoPlayChoiceState choice, Ff7AutoPlayActiveEvent active)
    {
        return choice.NativeSourceOwned == true && choice.BeforeActive is { Accepted: true } source && active.Accepted && source.Generation == active.Generation && source.VisitId == active.VisitId && source.EventId == active.EventId && source.Kind == active.Kind && source.Entity == active.Entity && source.Script == active.Script && source.ConditionFingerprint == active.ConditionFingerprint && source.ExecutionProvenance is { RootDispatchTick: >= 0 } before && active.ExecutionProvenance is { } current && before.RootDispatchTick == current.RootDispatchTick && before.ProgramIdentity == current.ProgramIdentity && Ff7AutoPlayExecutionOwners.Matches(source, source.Generation, before.ProgramIdentity) && Ff7AutoPlayExecutionOwners.Matches(active, active.Generation, current.ProgramIdentity);
    }

    internal IReadOnlyList<AutoPlayChoiceSelection> ReadSourceSelections(Ff7AutoPlayActiveEvent active)
    {
        if (active.Kind is Ff7AutoPlayTargetKind.WorldMesh or Ff7AutoPlayTargetKind.WorldModel or Ff7AutoPlayTargetKind.WorldControl)
        {
            return Selections;
        }
        List<AutoPlayChoiceSelection> result = [];
        for (Ff7AutoPlayChoiceState? choice = state.Choice; choice is not null; choice = choice.Parent)
        {
            if (MatchesChoiceSource(choice, active) && Coverage(choice.Key).ActiveOption is int selected)
            {
                result.Add(new(choice.Key, selected));
            }
        }
        result.Reverse();
        return result;
    }

    internal bool ShouldWaitForNativeContinuation(FieldVmSnapshot vm, Ff7FieldProcess process, ReadOnlySpan<byte> script)
    {
        if (nativeSession is null || nativeSession.World is not null || state.Choice is not { } choice)
        {
            return false;
        }
        Ff7AutoPlayChoiceNativeOwner? owner = choice.NativeOwner;
        if (owner is null && Coverage(choice.Key).BeforeCheckpoint is string path && File.Exists(path))
        {
            using FileStream input = File.OpenRead(path);
            Ff7SessionSave? before = CombinedSessionCodec.Read(input).Ff7;
            if (before is not null && before.FieldGeneration == nativeSession.FieldGeneration && before.Vm.ProgramIdentity == vm.ProgramIdentity)
            {
                foreach (Ff7Dialogue dialogue in before.Process.Dialogues.Where(dialogue => !dialogue.Completed && dialogue.FirstChoice is not null))
                {
                    if (ReadOwnerIdentity(before, dialogue, dialogue.FirstChoice!.Value, dialogue.LastChoice!.Value) == choice.OwnerIdentity)
                    {
                        owner = ReadNativeChoiceOwner(before, dialogue);
                        break;
                    }
                }
            }
            if (owner is not null)
            {
                state.Choice = choice with { NativeOwner = owner };
            }
        }
        return owner is not null && choiceReadiness.ShouldWaitForChoiceOwner(nativeSession.FieldGeneration, owner, vm, process, script);
    }
}
