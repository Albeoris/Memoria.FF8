using System.Numerics;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayPolicy
{
    // Reviewed native summit recovery priority (NG-112).
    internal const string GaeaSummitProgram = "F7A52553710150AA23F29CF56148D003AC63921F0EEA2AC178989F329961993B";
    private Ff7FieldHost? summitRecoveryHost;

    private void ObserveSummitRecoveryHost()
    {
        Ff7FieldHost? current = session.Machine.ProgramIdentity == GaeaSummitProgram ? session.Host : null;
        if (ReferenceEquals(current, summitRecoveryHost))
        {
            return;
        }
        if (summitRecoveryHost is not null)
        {
            summitRecoveryHost.NativeOperationExecuted -= ObserveSummitRecoveryOperation;
        }
        summitRecoveryHost = current;
        if (current is not null)
        {
            current.NativeOperationExecuted += ObserveSummitRecoveryOperation;
        }
    }

    private void ObserveSummitRecoveryOperation(FieldHostRequest request, FieldHostReply reply)
    {
        if (ReferenceEquals(summitRecoveryHost, session.Host) && RecordNativeSummitRecovery(State, session.FieldGeneration, session.Machine.ProgramIdentity, request, reply))
        {
            Publish();
        }
    }

    internal static bool RecordNativeSummitRecovery(Ff7AutoPlayState state, long generation, string program, FieldHostRequest request, FieldHostReply reply)
    {
        if (program != GaeaSummitProgram || !reply.Completed || request is not { Id: > 0, Entity: >= 3 and <= 5, Priority: 1, Command: FieldPartyRecovery { AllCharacters: true } } || request.ProgramCounter != 0x377 + (request.Entity - 3) * 0x32 || state.Active is not { Accepted: true, Returned: false, Script: 4, Kind: Ff7AutoPlayTargetKind.LineTouch, ExecutionProvenance: { RootReturned: false, RootDispatchTick: >= 0 } provenance } active || active.Entity != request.Entity || !Ff7AutoPlayExecutionOwners.Matches(active, generation, program) || !provenance.Owners.Any(owner => !owner.Queued && owner.Entity == active.Entity && owner.Priority == 1 && owner.Script == 4 && owner.DispatchTick == provenance.RootDispatchTick))
        {
            return false;
        }
        state.SummitRecovery = new(generation, program, active.VisitId, active.EventId, active.ConditionFingerprint, provenance.RootDispatchTick, request);
        return true;
    }

    internal static bool CompleteNativeSummitRecovery(Ff7AutoPlayState state, Ff7AutoPlayObservation observation, Ff7AutoPlayActiveEvent active, Ff7FieldProcess process)
    {
        if (state.SummitRecovery is not { Completed: false } receipt || receipt.Generation != observation.Generation || receipt.Program != observation.ProgramIdentity || receipt.VisitId != active.VisitId || receipt.EventId != active.EventId || receipt.Condition != active.ConditionFingerprint || active is not { Accepted: true, Returned: true, ExecutionProvenance: { RootReturned: true, Owners.Length: 0 } provenance } || provenance.RootDispatchTick != receipt.RootDispatchTick || !Ff7AutoPlayExecutionOwners.Matches(active, observation.Generation, observation.ProgramIdentity) || process.PlayerLocked || process.MenuDisabled || process.GlobalCommand != Ff7GlobalCommand.None || process.Transition is not null || process.Join is not null || process.Dialogues.Any(dialogue => !dialogue.Completed))
        {
            return false;
        }
        state.SummitRecovery = receipt with { Completed = true };
        return true;
    }

    internal static bool HasCompletedNativeSummitRecovery(Ff7AutoPlayState state, Ff7AutoPlayObservation observation, Ff7FieldProcess process)
    {
        if (observation.ProgramIdentity != GaeaSummitProgram || process.PlayerLocked || process.MenuDisabled || process.GlobalCommand != Ff7GlobalCommand.None || process.Dialogues.Any(dialogue => !dialogue.Completed) || state.Active is { Accepted: true })
        {
            return false;
        }
        return state.SummitRecovery is { Completed: true } receipt && receipt.Program == observation.ProgramIdentity && receipt.Generation == observation.Generation && receipt.VisitId == observation.VisitId;
    }

    internal static Ff7AutoPlayTarget? ReadNativeSummitRecovery(Ff7AutoPlayState state, Ff7AutoPlayObservation observation, Ff7FieldProcess process, IReadOnlyList<PcWalkmeshTriangle> mesh)
    {
        if (state.DestinationField is not (698 or 695) || observation.ProgramIdentity != GaeaSummitProgram || state.Choice is not null || state.PendingRestore is not null || state.Active is { Accepted: true } || !observation.CanAcceptInput || process.PlayerLocked || process.MenuDisabled || process.GlobalCommand != Ff7GlobalCommand.None || process.Transition is not null || process.Join is not null || process.Movie is { Playing: true } || process.Menu is { Completed: false } || process.Dialogues.Any(dialogue => !dialogue.Completed) || process.Actors.SingleOrDefault(actor => actor.PlayerControlled) is not { EntityBound: true, Entity: 9, Ladder: null, Jump: null } player || process.Motions.Any(motion => motion.Entity == player.Entity && !motion.Completed) || HasCompletedNativeSummitRecovery(state, observation, process))
        {
            return null;
        }
        Vector3 position = new(player.X4096 / 4096f, player.Y4096 / 4096f, player.Z4096 / 4096f);
        return observation.Events.Where(target => target.Entity is >= 3 and <= 5 && target.Script == 4 && target.Kind == Ff7AutoPlayTargetKind.LineTouch && target.AnalysisComplete && !target.ChangesField && !target.ChangesStory).Select(target => (Target: target, Contacts: process.Lines.Where(line => line.Entity == target.Entity && line.Enabled == 1).SelectMany(line => Ff7AutoplayDriver.DiscoverLineContacts(mesh, process.BlockedTriangles, player, line)).ToArray())).Where(candidate => candidate.Contacts.Length != 0).OrderBy(candidate => candidate.Contacts.Min(contact => Vector3.DistanceSquared(position, contact.Point))).ThenBy(candidate => candidate.Target.Entity).Select(candidate => candidate.Target).FirstOrDefault();
    }

    internal static bool RetireUnacceptedSummitPlan(Ff7AutoPlayState state, Ff7AutoPlayObservation observation, Ff7FieldProcess process, IReadOnlyList<PcWalkmeshTriangle> mesh)
    {
        if (ReadNativeSummitRecovery(state, observation, process, mesh) is not { } preferred || state.NativeRoute is not null || state.NativeRevalidation is not null || state.Active is not { Accepted: false, Returned: false, TransitionOwned: false, CompletionRecorded: false, ExecutionProvenance: null } active || active.EventId == preferred.Id || active.Generation != observation.Generation || active.VisitId != observation.VisitId || active.NavigationKey != observation.NavigationKey || state.NavigationGoal is { } goal && (goal.NavigationKey != observation.NavigationKey || goal.EventId != active.EventId || goal.Conditions != active.ConditionFingerprint) || state.TravelCommitment is { } commitment && (commitment.DestinationField != state.DestinationField || commitment.NavigationKey != observation.NavigationKey || commitment.Goal.NavigationKey != observation.NavigationKey || commitment.Goal.EventId != active.EventId || commitment.Goal.Conditions != active.ConditionFingerprint))
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

    private Ff7AutoPlayTarget? ReadNativeSummitRecovery(Ff7AutoPlayObservation observation)
    {
        return State.DestinationField is 698 or 695 && observation.ProgramIdentity == GaeaSummitProgram ? ReadNativeSummitRecovery(State, observation, session.Host.Process, session.Field.Walkmesh) : null;
    }
}
