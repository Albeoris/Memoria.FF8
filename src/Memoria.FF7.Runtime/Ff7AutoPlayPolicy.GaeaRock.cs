using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayPolicy
{
    // Reviewed native cave rock prerequisite (NG-110).
    internal const string GaeaRockProgram = "4155CB665FC5C08C49E5C0EC460727D2313FDAA236B8F631B71F9809968037E5";
    private const string GaeaRockOuterProgram = "9F55F9FE23216E5B695E5522BC8C6B7375882C5016AF3B187F927024EB1A9083";

    internal static Ff7AutoPlayTarget? ReadNativeRockPrerequisite(Ff7AutoPlayState state, Ff7AutoPlayObservation observation, Ff7FieldProcess process, FieldVariables variables, IReadOnlySet<int> reachable)
    {
        if (state.DestinationField != 692 || state.Choice is not null || state.PendingRestore is not null || state.Active is { Accepted: true } || !observation.CanAcceptInput || process.PlayerLocked || process.MenuDisabled || process.GlobalCommand != Ff7GlobalCommand.None || process.Transition is not null || process.Join is not null || process.Movie is { Playing: true } || process.Menu is { Completed: false } || process.Dialogues.Any(dialogue => !dialogue.Completed) || (variables.ReadByte(FieldBank.Persistent0Byte, 0x83) & 2) != 0 || process.Actors.SingleOrDefault(actor => actor.PlayerControlled) is not { EntityBound: true } player || !reachable.Contains(player.Triangle))
        {
            return null;
        }
        string? id = null;
        if (observation.ProgramIdentity == GaeaRockProgram && player.Entity == 8)
        {
            id = reachable.Contains(85) && !reachable.Contains(163) ? "actor:10:script:1" : reachable.Contains(163) && !reachable.Contains(85) ? "gateway:3" : null;
        }
        else if (observation.ProgramIdentity == GaeaRockOuterProgram && player.Entity == 5 && reachable.Contains(0) && reachable.Contains(79) && !reachable.Contains(153) && !reachable.Contains(238))
        {
            id = "gateway:2";
        }
        return observation.Events.SingleOrDefault(target => target.Id == id && target.AnalysisComplete && !target.ChangesStory && (target.Kind == Ff7AutoPlayTargetKind.Gateway && target.DestinationField == (observation.ProgramIdentity == GaeaRockProgram ? 690 : 691) || target is { Entity: 10, Script: 1, Kind: Ff7AutoPlayTargetKind.ActorTalk, ChangesField: false, MayChangeNavigation: true }));
    }

    internal static bool RetireUnacceptedRockPlan(Ff7AutoPlayState state, Ff7AutoPlayObservation observation, Ff7FieldProcess process, FieldVariables variables, IReadOnlySet<int> reachable)
    {
        if (ReadNativeRockPrerequisite(state, observation, process, variables, reachable) is not { } preferred || state.NativeRoute is not null || state.NativeRevalidation is not null || state.Active is not { Accepted: false, Returned: false, TransitionOwned: false, CompletionRecorded: false, ExecutionProvenance: null } active || active.EventId == preferred.Id || active.Generation != observation.Generation || active.VisitId != observation.VisitId || active.NavigationKey != observation.NavigationKey || state.NavigationGoal is { } goal && (goal.NavigationKey != observation.NavigationKey || goal.EventId != active.EventId || goal.Conditions != active.ConditionFingerprint) || state.TravelCommitment is { } commitment && (commitment.DestinationField != 692 || commitment.NavigationKey != observation.NavigationKey || commitment.Goal.NavigationKey != observation.NavigationKey || commitment.Goal.EventId != active.EventId || commitment.Goal.Conditions != active.ConditionFingerprint))
        {
            return false;
        }
        var coverage = state.Coverage.Visits.SingleOrDefault(visit => visit.Visit.Key == active.VisitId)?.Events.SingleOrDefault(item => item.Event.Id == active.EventId);
        if (coverage?.Conditions?.ActiveEntry == active.ConditionFingerprint)
        {
            coverage.Conditions.ActiveEntry = null;
        }
        state.Active = null;
        state.NavigationGoal = null;
        state.TravelCommitment = null;
        return true;
    }

    private Ff7AutoPlayTarget? ReadNativeRockPrerequisite(Ff7AutoPlayObservation observation)
    {
        if (State.DestinationField != 692 || observation.ProgramIdentity is not (GaeaRockProgram or GaeaRockOuterProgram) || (session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0x83) & 2) != 0)
        {
            return null;
        }
        Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        return ReadNativeRockPrerequisite(State, observation, session.Host.Process, session.Machine.Variables, Ff7AutoplayDriver.ReachableTriangles(session.Field.Walkmesh, session.Host.Process.BlockedTriangles, player.Triangle));
    }
}
