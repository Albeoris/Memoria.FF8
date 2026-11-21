using Memoria.FF7.Events;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayPolicy
{
    // Reviewed native cave icicle prerequisite (NG-111).
    internal const string GaeaIcicleProgram = "1B72A6B868085A718BB72B39B6C756AD76FA71966D6C9B6DF1865A7A50851091";

    internal static Ff7AutoPlayTarget? ReadNativeIciclePrerequisite(Ff7AutoPlayState state, Ff7AutoPlayObservation observation, Ff7FieldProcess process, FieldVariables variables, IReadOnlySet<int> reachable, IReadOnlyList<PcWalkmeshTriangle>? mesh = null)
    {
        if (state.DestinationField != 694 || state.Choice is not null || state.PendingRestore is not null || state.Active is { Accepted: true } || !observation.CanAcceptInput || process.PlayerLocked || process.MenuDisabled || process.GlobalCommand != Ff7GlobalCommand.None || process.Transition is not null || process.Join is not null || process.Movie is { Playing: true } || process.Menu is { Completed: false } || process.Dialogues.Any(dialogue => !dialogue.Completed) || process.Actors.SingleOrDefault(actor => actor.PlayerControlled) is not { EntityBound: true } player || !reachable.Contains(player.Triangle))
        {
            return null;
        }
        int flags = variables.ReadByte(FieldBank.Persistent0Byte, 0x83);
        if (observation.ProgramIdentity == Ff7AutoPlayChoiceWorkflow.GaeaIcicleLowerProgram)
        {
            if ((flags & 0x1C) != 0x1C || player.Entity != 20 || mesh is null)
            {
                return null;
            }
            return observation.Events.FirstOrDefault(target => target.Entity is 5 or 7 or 9 or 11 or 13 && target.Script == 2 && target.Kind == Ff7AutoPlayTargetKind.LinePush && target.AnalysisComplete && target.MayChangeNavigation && !target.ChangesField && !target.ChangesStory && target.PossibleJumpLandings.Any(landing => landing.Entity == 20 && landing.Literal && landing.Command.Duration > 0) && process.Lines.Any(line => line.Entity == target.Entity && line.Enabled == 1 && Ff7AutoplayDriver.DiscoverLineContacts(mesh, process.BlockedTriangles, player, line).Count != 0));
        }
        if ((flags & 0x1C) == 0x1C || observation.ProgramIdentity != GaeaIcicleProgram || player.Entity != 10 || !reachable.Contains(39) || reachable.Contains(3))
        {
            return null;
        }
        int entity = (flags & 4) == 0 ? 3 : (flags & 8) == 0 ? 4 : 5;
        return observation.Events.SingleOrDefault(target => target.Entity == entity && target.Script == 5 && target.Kind == Ff7AutoPlayTargetKind.LineEnter && target.AnalysisComplete && target.MayChangeNavigation && !target.ChangesStory && target.DestinationField == 693);
    }

    internal static bool RetireUnacceptedIciclePlan(Ff7AutoPlayState state, Ff7AutoPlayObservation observation, Ff7FieldProcess process, FieldVariables variables, IReadOnlySet<int> reachable, IReadOnlyList<PcWalkmeshTriangle>? mesh = null)
    {
        if (ReadNativeIciclePrerequisite(state, observation, process, variables, reachable, mesh) is not { } preferred || state.NativeRoute is not null || state.NativeRevalidation is not null || state.Active is not { Accepted: false, Returned: false, TransitionOwned: false, CompletionRecorded: false, ExecutionProvenance: null } active || active.EventId == preferred.Id || active.Generation != observation.Generation || active.VisitId != observation.VisitId || active.NavigationKey != observation.NavigationKey || state.NavigationGoal is { } goal && (goal.NavigationKey != observation.NavigationKey || goal.EventId != active.EventId || goal.Conditions != active.ConditionFingerprint) || state.TravelCommitment is { } commitment && (commitment.DestinationField != 694 || commitment.NavigationKey != observation.NavigationKey || commitment.Goal.NavigationKey != observation.NavigationKey || commitment.Goal.EventId != active.EventId || commitment.Goal.Conditions != active.ConditionFingerprint))
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

    private Ff7AutoPlayTarget? ReadNativeIciclePrerequisite(Ff7AutoPlayObservation observation)
    {
        if (State.DestinationField != 694 || observation.ProgramIdentity is not (GaeaIcicleProgram or Ff7AutoPlayChoiceWorkflow.GaeaIcicleLowerProgram))
        {
            return null;
        }
        Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        return ReadNativeIciclePrerequisite(State, observation, session.Host.Process, session.Machine.Variables, Ff7AutoplayDriver.ReachableTriangles(session.Field.Walkmesh, session.Host.Process.BlockedTriangles, player.Triangle), session.Field.Walkmesh);
    }
}
