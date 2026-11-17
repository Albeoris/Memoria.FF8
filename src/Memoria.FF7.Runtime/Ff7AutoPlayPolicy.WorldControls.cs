using System.Security.Cryptography;
using System.Text;
using Memoria.AutoPlay;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayPolicy
{
    private readonly Dictionary<string, Ff7AutoPlayWorldControlObservation> worldControlCatalog = new(StringComparer.Ordinal);

    private void ReplanConsumedWorldProbeForControl()
    {
        if (State.Active is not { Kind: Ff7AutoPlayTargetKind.WorldMesh or Ff7AutoPlayTargetKind.WorldModel, Accepted: false, Returned: false, TransitionOwned: false, CompletionRecorded: false } active || State.Choice is not null || State.PendingRestore is not null || active.TravelRoute is not null || State.NavigationGoal is not null)
        {
            return;
        }
        AutoPlayVisitCoverage<Ff7AutoPlayDeferral> visit = State.Coverage.Visits.Single(item => item.Visit.Key == active.VisitId);
        if (!HasConsumedWorldEntry(visit.Events.Single(item => item.Event.Id == active.EventId), active.ConditionFingerprint))
        {
            return;
        }
        if (worldControlCatalog.Values.Any(control => visit.Events.SingleOrDefault(item => item.Event.Id == control.Id) is { } entry && !HasConsumedWorldEntry(entry, control.ConditionFingerprint) && !entry.Unavailable))
        {
            State.Active = null;
        }
    }

    private bool TryBeginWorldControl(AutoPlayPlanStep next, Ff7AutoPlayObservation observation)
    {
        if (!worldControlCatalog.TryGetValue(next.Event.Id, out Ff7AutoPlayWorldControlObservation? control))
        {
            return false;
        }
        if (next.IsTravel)
        {
            throw new NotSupportedException("A native world control action cannot replay an unauthenticated travel route.");
        }
        planner.BeginEvent(observation.VisitId, control.Id);
        State.NavigationGoal = next.Goal;
        State.Active = new(observation.VisitId, control.Id, control.ConditionFingerprint, session.FieldGeneration, control.Model, -1, Ff7AutoPlayTargetKind.WorldControl, null, Story(), session.CaptureEventProgressWitness(), NavigationKey: observation.NavigationKey, WorldControl: control);
        return true;
    }

    private Ff7AutoplayInput AdvanceWorldControl(Ff7WorldRuntime world, Ff7AutoPlayObservation observation, Ff7AutoPlayActiveEvent active)
    {
        Ff7AutoPlayWorldControlObservation control = active.WorldControl ?? throw new InvalidDataException("A native world control owner has no action observation.");
        if (world.HasAutoPlayControlCompleted(control))
        {
            planner.Completed(active.VisitId, active.EventId, observedStoryChanged: false, entrySignature: active.ConditionFingerprint, entrySignatureAuthoritative: true, availabilityFingerprintAuthoritative: true);
            State.Active = null;
            State.NavigationGoal = null;
            State.Status = "The native transport control returned to its rider";
            Publish();
            return default;
        }
        TrackFrontierProgress(State, Story());
        if (active.WorldControlPhase >= 2 && world.Process.Vehicle is not { DismountPhase: > 0 })
        {
            Ff7AutoPlayWorldVisitState visited = State.WorldVisits[active.VisitId];
            string key = active.VisitId + ":" + active.EventId + ":physical";
            string physical = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Ff7AutoplayDriver.WorldMovementKey(world.CaptureAutoPlayMovementState()))));
            visited.PhysicalGuards.Add(key);
            visited.ObservedPhysicalGuards[key] = physical;
            planner.MarkUnavailable(active.VisitId, active.EventId, new(active.ConditionFingerprint, active.VisitId, key, physical, active.EventId));
            State.Active = null;
            State.Status = "The native transport could not place its rider at this source";
            Publish();
            return default;
        }
        Ff7AutoplayInput input = world.ChooseAutoPlayControlInput(control);
        int phase = active.WorldControlPhase;
        if (phase < 2)
        {
            phase = (input.Held & 0x40) != 0 ? 1 : 2;
        }
        State.Active = active with { Accepted = true, WorldControlPhase = phase };
        State.Status = "Using the native transport exit control";
        Publish();
        return input;
    }
}
