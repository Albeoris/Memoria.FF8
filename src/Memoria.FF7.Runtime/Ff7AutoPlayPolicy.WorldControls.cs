using System.Security.Cryptography;
using System.Text;
using Memoria.AutoPlay;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayPolicy
{
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
        if (worldModeCatalog.Any(mode => visit.Events.SingleOrDefault(item => item.Event.Id == mode.Key) is { } entry && !HasConsumedWorldEntry(entry, entry.Event.ConditionSignature) && !entry.Unavailable))
        {
            State.Active = null;
        }
    }

    private bool TryBeginWorldControl(AutoPlayPlanStep next, Ff7AutoPlayObservation observation)
    {
        if (!worldModeCatalog.TryGetValue(next.Event.Id, out WorldModeCandidate? candidate))
        {
            return false;
        }
        if (next.IsTravel)
        {
            throw new NotSupportedException("A native world control action cannot replay an unauthenticated travel route.");
        }
        planner.BeginEvent(observation.VisitId, next.Event.Id);
        RetainDestinationGoal(next, observation.NavigationKey, next.Event.ConditionSignature!);
        State.Active = new(observation.VisitId, next.Event.Id, next.Event.ConditionSignature!, session.FieldGeneration, worldModeModel, -1, Ff7AutoPlayTargetKind.WorldControl, null, Story(), session.CaptureEventProgressWitness(), NavigationKey: observation.NavigationKey, WorldModeGoal: candidate.Goal);
        return true;
    }

    private Ff7AutoplayInput AdvanceWorldControl(Ff7WorldRuntime world, Ff7AutoPlayObservation observation, Ff7AutoPlayActiveEvent active)
    {
        Ff7AutoPlayWorldControlObservation control = active.WorldControl ?? throw new InvalidDataException("A native world control owner has no action observation.");
        if (world.HasAutoPlayControlCompleted(control))
        {
            AutoPlayTravelGoal? destination = null;
            if (active.WorldModeGoal is { } goal)
            {
                ObserveWorldChunk(world, goal.Chunk);
                Ff7AutoPlayWorldScriptObservation target = worldCatalogs[goal.Chunk].Scripts.Single(item => item.Id == goal.TargetEventId);
                Ff7AutoPlayWorldGuide actualRiderRoute = world.ReadAutoPlayNavigationGuide(target);
                if (actualRiderRoute.Faces.Length == 0)
                {
                    throw new NotSupportedException(actualRiderRoute.Reason ?? "The actual native landing did not connect to the selected rider entry.");
                }
                destination = new(observation.NavigationKey, goal.TargetEventId, WorldCondition(target));
            }
            planner.Completed(active.VisitId, active.EventId, observedStoryChanged: false, entrySignature: active.ConditionFingerprint, entrySignatureAuthoritative: true, availabilityFingerprintAuthoritative: true);
            State.Active = null;
            State.NavigationGoal = destination;
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
