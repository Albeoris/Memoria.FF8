using Memoria.FF7.Events;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayPolicy
{
    // Reviewed native Gaea ascent and movie prerequisite (NG-114).
    internal const string GaeaMovieProgram = "DFC7E66D342921243A62BD7F892060E8CE934B8E48F332324C12803BCFED0B8A";

    internal static Ff7AutoPlayTarget? ReadNativeGaeaMoviePrerequisite(Ff7AutoPlayState state, Ff7AutoPlayObservation observation, Ff7FieldProcess process, FieldVariables variables, IReadOnlyList<PcWalkmeshTriangle> mesh)
    {
        if (state.DestinationField != 700 || observation.ProgramIdentity != GaeaMovieProgram || state.Choice is not null || state.PendingRestore is not null || state.Active is { Accepted: true } || !observation.CanAcceptInput || (variables.ReadByte(FieldBank.Persistent0Byte, 0x83) & 1) != 0 || process.PlayerLocked || process.MenuDisabled || process.GlobalCommand != Ff7GlobalCommand.None || process.Transition is not null || process.Join is not null || process.Movie is { Playing: true } || process.Menu is { Completed: false } || process.Dialogues.Any(dialogue => !dialogue.Completed) || process.Actors.SingleOrDefault(actor => actor.PlayerControlled) is not { EntityBound: true, Entity: 8, Ladder: null, Jump: null } player || process.Motions.Any(motion => motion.Entity == player.Entity && !motion.Completed))
        {
            return null;
        }
        IReadOnlySet<int> reachable = Ff7AutoplayDriver.ReachableTriangles(mesh, process.BlockedTriangles, player.Triangle);
        int entity = reachable.Contains(31) && !reachable.Contains(2) ? 3 : reachable.Contains(2) && !reachable.Contains(31) ? 5 : -1;
        return observation.Events.SingleOrDefault(target => target.Entity == entity && target.Script == 4 && target.Kind == Ff7AutoPlayTargetKind.LineTouch && target.AnalysisComplete && !target.ChangesField && !target.ChangesStory && (entity != 3 || target.LadderLandings.Any(landing => landing.Entity == 8 && landing.Literal && landing.Command.Triangle == 2)) && process.Lines.Any(line => line.Entity == entity && line.Enabled == 1 && Ff7AutoplayDriver.DiscoverLineContacts(mesh, process.BlockedTriangles, player, line).Count != 0));
    }

    internal static bool RetireUnacceptedGaeaMoviePlan(Ff7AutoPlayState state, Ff7AutoPlayObservation observation, Ff7FieldProcess process, FieldVariables variables, IReadOnlyList<PcWalkmeshTriangle> mesh)
    {
        if (ReadNativeGaeaMoviePrerequisite(state, observation, process, variables, mesh) is not { } preferred || state.NativeRoute is not null || state.NativeRevalidation is not null || state.Active is not { Accepted: false, Returned: false, TransitionOwned: false, CompletionRecorded: false, ExecutionProvenance: null } active || active.EventId == preferred.Id || active.Generation != observation.Generation || active.VisitId != observation.VisitId || active.NavigationKey != observation.NavigationKey || state.NavigationGoal is { } goal && (goal.NavigationKey != observation.NavigationKey || goal.EventId != active.EventId || goal.Conditions != active.ConditionFingerprint) || state.TravelCommitment is { } commitment && (commitment.DestinationField != 700 || commitment.NavigationKey != observation.NavigationKey || commitment.Goal.NavigationKey != observation.NavigationKey || commitment.Goal.EventId != active.EventId || commitment.Goal.Conditions != active.ConditionFingerprint))
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

    private Ff7AutoPlayTarget? ReadNativeGaeaMoviePrerequisite(Ff7AutoPlayObservation observation)
    {
        return State.DestinationField == 700 && observation.ProgramIdentity == GaeaMovieProgram ? ReadNativeGaeaMoviePrerequisite(State, observation, session.Host.Process, session.Machine.Variables, session.Field.Walkmesh) : null;
    }
}
