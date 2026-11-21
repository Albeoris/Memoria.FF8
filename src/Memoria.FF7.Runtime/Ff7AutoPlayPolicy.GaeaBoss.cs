using Memoria.FF7.Events;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayPolicy
{
    // Reviewed native Gaea boss-line prerequisite (NG-113).
    internal const string GaeaBossProgram = "279765F81E2101F56D4334DD43D09E2A8D1687D658C4DF90C4AAD939F79DC67C";

    internal static Ff7AutoPlayTarget? ReadNativeGaeaBossPrerequisite(Ff7AutoPlayState state, Ff7AutoPlayObservation observation, Ff7FieldProcess process, FieldVariables variables, IReadOnlyList<PcWalkmeshTriangle> mesh)
    {
        if (state.DestinationField != 695 || observation.ProgramIdentity != GaeaBossProgram || state.Choice is not null || state.PendingRestore is not null || state.Active is { Accepted: true } || !observation.CanAcceptInput || (variables.ReadByte(FieldBank.Persistent0Byte, 0x86) & 4) != 0 || process.PlayerLocked || process.MenuDisabled || process.GlobalCommand != Ff7GlobalCommand.None || process.Transition is not null || process.Join is not null || process.Movie is { Playing: true } || process.Menu is { Completed: false } || process.Dialogues.Any(dialogue => !dialogue.Completed) || process.Actors.SingleOrDefault(actor => actor.PlayerControlled) is not { EntityBound: true, Entity: 3, Ladder: null, Jump: null } player || process.Motions.Any(motion => motion.Entity == player.Entity && !motion.Completed) || !process.Lines.Any(line => line.Entity == 2 && line.Enabled == 1 && Ff7AutoplayDriver.DiscoverLineContacts(mesh, process.BlockedTriangles, player, line).Count != 0))
        {
            return null;
        }
        return observation.Events.SingleOrDefault(target => target.Entity == 2 && target.Script == 2 && target.Kind == Ff7AutoPlayTargetKind.LinePush && target.AnalysisComplete && !target.ChangesField && !target.ChangesStory);
    }

    internal static bool RetireUnacceptedGaeaBossPlan(Ff7AutoPlayState state, Ff7AutoPlayObservation observation, Ff7FieldProcess process, FieldVariables variables, IReadOnlyList<PcWalkmeshTriangle> mesh)
    {
        if (ReadNativeGaeaBossPrerequisite(state, observation, process, variables, mesh) is not { } preferred || state.NativeRoute is not null || state.NativeRevalidation is not null || state.Active is not { Accepted: false, Returned: false, TransitionOwned: false, CompletionRecorded: false, ExecutionProvenance: null } active || active.EventId == preferred.Id || active.Generation != observation.Generation || active.VisitId != observation.VisitId || active.NavigationKey != observation.NavigationKey || state.NavigationGoal is { } goal && (goal.NavigationKey != observation.NavigationKey || goal.EventId != active.EventId || goal.Conditions != active.ConditionFingerprint) || state.TravelCommitment is { } commitment && (commitment.DestinationField != 695 || commitment.NavigationKey != observation.NavigationKey || commitment.Goal.NavigationKey != observation.NavigationKey || commitment.Goal.EventId != active.EventId || commitment.Goal.Conditions != active.ConditionFingerprint))
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

    private Ff7AutoPlayTarget? ReadNativeGaeaBossPrerequisite(Ff7AutoPlayObservation observation)
    {
        return State.DestinationField == 695 && observation.ProgramIdentity == GaeaBossProgram ? ReadNativeGaeaBossPrerequisite(State, observation, session.Host.Process, session.Machine.Variables, session.Field.Walkmesh) : null;
    }
}
