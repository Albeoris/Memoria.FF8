using Memoria.FF7.Events;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayPolicy
{
    // Reviewed native Whirlwind tutorial prerequisite (NG-115).
    internal const string WhirlwindTutorialProgram = "F6C586EC658C8703F93488AA3A6F0C6A7F76D8C7C715D4E11C334EC224628DD9";

    internal static Ff7AutoPlayTarget? ReadNativeWhirlwindTutorialPrerequisite(Ff7AutoPlayState state, Ff7AutoPlayObservation observation, Ff7FieldProcess process, FieldVariables variables, IReadOnlyList<PcWalkmeshTriangle> mesh)
    {
        if (state.DestinationField != 704 || observation.ProgramIdentity != WhirlwindTutorialProgram || state.Choice is not null || state.PendingRestore is not null || state.Active is { Accepted: true } || !observation.CanAcceptInput || (variables.ReadByte(FieldBank.Persistent0Byte, 0x85) & 8) != 0 || process.PlayerLocked || process.MenuDisabled || process.GlobalCommand != Ff7GlobalCommand.None || process.Transition is not null || process.Join is not null || process.Movie is { Playing: true } || process.Menu is { Completed: false } || process.Dialogues.Any(dialogue => !dialogue.Completed) || process.Actors.SingleOrDefault(actor => actor.PlayerControlled) is not { EntityBound: true, Entity: 10, Ladder: null, Jump: null } player || process.Motions.Any(motion => motion.Entity == player.Entity && !motion.Completed))
        {
            return null;
        }
        System.Numerics.Vector3 position = new(player.X4096 / 4096f, player.Y4096 / 4096f, player.Z4096 / 4096f);
        return observation.Events.Where(target => target.Entity is >= 7 and <= 9 && target.Script == 2 && target.Kind == Ff7AutoPlayTargetKind.LinePush && target.AnalysisComplete && !target.ChangesField && !target.ChangesStory)
            .SelectMany(target => process.Lines.Where(line => line.Entity == target.Entity && line.Enabled == 1).SelectMany(line => Ff7AutoplayDriver.DiscoverLineContacts(mesh, process.BlockedTriangles, player, line)).Select(contact => (Target: target, Distance: System.Numerics.Vector3.DistanceSquared(position, contact.Point))))
            .OrderBy(item => item.Distance)
            .Select(item => item.Target)
            .FirstOrDefault();
    }

    internal static bool RetireUnacceptedWhirlwindTutorialPlan(Ff7AutoPlayState state, Ff7AutoPlayObservation observation, Ff7FieldProcess process, FieldVariables variables, IReadOnlyList<PcWalkmeshTriangle> mesh)
    {
        if (ReadNativeWhirlwindTutorialPrerequisite(state, observation, process, variables, mesh) is not { } preferred || state.NativeRoute is not null || state.NativeRevalidation is not null || state.Active is not { Accepted: false, Returned: false, TransitionOwned: false, CompletionRecorded: false, ExecutionProvenance: null } active || active.EventId == preferred.Id || active.Generation != observation.Generation || active.VisitId != observation.VisitId || active.NavigationKey != observation.NavigationKey || state.NavigationGoal is { } goal && (goal.NavigationKey != observation.NavigationKey || goal.EventId != active.EventId || goal.Conditions != active.ConditionFingerprint) || state.TravelCommitment is { } commitment && (commitment.DestinationField != 704 || commitment.NavigationKey != observation.NavigationKey || commitment.Goal.NavigationKey != observation.NavigationKey || commitment.Goal.EventId != active.EventId || commitment.Goal.Conditions != active.ConditionFingerprint))
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

    private Ff7AutoPlayTarget? ReadNativeWhirlwindTutorialPrerequisite(Ff7AutoPlayObservation observation)
    {
        return State.DestinationField == 704 && observation.ProgramIdentity == WhirlwindTutorialProgram ? ReadNativeWhirlwindTutorialPrerequisite(State, observation, session.Host.Process, session.Machine.Variables, session.Field.Walkmesh) : null;
    }
}
