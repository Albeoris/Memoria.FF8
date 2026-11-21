using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayChoiceWorkflow
{
    internal void RecoverLostRestoredChoiceObservation()
    {
        if (nativeSession is null || state.PendingRestore is not null || state.Choice is not { } choice || Coverage(choice.Key).BeforeCheckpoint is not string path || !File.Exists(path))
        {
            return;
        }
        using FileStream input = File.OpenRead(path);
        Ff7SessionSave? before = CombinedSessionCodec.Read(input).Ff7;
        if (before is not null)
        {
            TryQueueLostChoiceObservationReplay(state, nativeSession.Capture(), before);
        }
    }

    internal static bool TryQueueLostChoiceObservationReplay(Ff7AutoPlayState state, Ff7SessionSave current, Ff7SessionSave before)
    {
        if (state.PendingRestore is not null || state.NativeRoute is not null || state.Choice is not { BeforeActive: { Accepted: true, Returned: false } recorded, NativeOwner: { } question } choice || state.Active is not { Accepted: true, Returned: false } active || current.World is not null || current.Battle is not null || before.World is not null || before.Battle is not null || current.FieldGeneration != before.FieldGeneration || current.FieldName != before.FieldName || current.Vm.ProgramIdentity != before.Vm.ProgramIdentity || !Ff7AutoPlayExecutionOwners.Matches(active, current.FieldGeneration, current.Vm.ProgramIdentity) || !Ff7AutoPlayExecutionOwners.Matches(recorded, before.FieldGeneration, before.Vm.ProgramIdentity) || active.EventId != recorded.EventId || active.VisitId != recorded.VisitId || active.ConditionFingerprint != recorded.ConditionFingerprint || active.ExecutionProvenance!.RootDispatchTick != recorded.ExecutionProvenance!.RootDispatchTick || current.Process.PlayerLocked || current.Process.MenuDisabled || current.Process.GlobalCommand != Ff7GlobalCommand.None || current.Process.Transition is not null || current.Process.Join is not null || current.Process.Movie is { Playing: true } || current.Process.Dialogues.Any(dialogue => !dialogue.Completed))
        {
            return false;
        }
        var coverage = state.Coverage.Choices.Single(item => item.Key == choice.Key);
        if (coverage.BeforeCheckpoint is not string path || coverage.ActiveOption is not int selected || coverage.CompletedOptions.Contains(selected) || question.Generation != before.FieldGeneration || question.ProgramIdentity != before.Vm.ProgramIdentity || before.Process.Dialogues.SingleOrDefault(dialogue => dialogue.RequestId == question.RequestId) is not { } prompt || ReadNativeChoiceOwner(before, prompt) is not { } actualQuestion || actualQuestion.Entity != question.Entity || actualQuestion.Priority != question.Priority || actualQuestion.ProgramCounter != question.ProgramCounter || !actualQuestion.SavedProgramCounters.SequenceEqual(question.SavedProgramCounters))
        {
            return false;
        }
        foreach (var owner in active.ExecutionProvenance.Owners)
        {
            if ((uint)owner.Entity >= current.Vm.Entities.Length || current.Vm.Entities[owner.Entity].Priority != 7 || current.Vm.Entities[owner.Entity].SavedProgramCounters.Any(pc => pc != 0) || current.Vm.Entities[owner.Entity].SyncStates.Any(sync => sync != FieldSyncState.None) || current.Vm.Entities[owner.Entity].SyncOwners.Any(source => source != -1) || (current.Vm.PendingRequests ?? []).Any(request => request.Entity == owner.Entity && request.Priority == owner.Priority))
            {
                return false;
            }
        }
        Ff7AutoPlayActiveEvent restored = Ff7AutoPlayExecutionOwners.NormalizeRestoredObservationCursor(recorded, before.Vm, before.FieldGeneration);
        if (ReferenceEquals(restored, recorded))
        {
            return false;
        }
        state.Choice = choice with { BeforeActive = restored };
        state.Active = restored;
        state.PendingRestore = new(path);
        state.Status = "Replaying the registered native choice with its restored observation cursor.";
        return true;
    }
}
