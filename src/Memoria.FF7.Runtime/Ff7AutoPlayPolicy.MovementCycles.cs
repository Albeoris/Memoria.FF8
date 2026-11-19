using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Memoria.AutoPlay;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayPolicy
{
    internal static bool DeferUnavailableActiveNavigation(Ff7AutoPlayState state, Ff7AutoPlayObservation observation, Func<Ff7AutoPlayActiveEvent, Ff7AutoPlayTarget, bool>? canContinueApproach = null)
    {
        if (state.Active is not { Accepted: false } active || active.VisitId != observation.VisitId || active.Generation != observation.Generation)
        {
            return false;
        }
        Ff7AutoPlayTarget? target = observation.Events.SingleOrDefault(target => target.Id == active.EventId);
        AutoPlayEventCoverage<Ff7AutoPlayDeferral>? coverage = state.Coverage.Visits.SingleOrDefault(visit => visit.Visit.Key == observation.VisitId)?.Events.SingleOrDefault(item => item.Event.Id == active.EventId);
        if (target is null || coverage is null || coverage.Event.ConditionSignature != target.ConditionFingerprint || !coverage.UnavailableVariants.Contains(target.Id))
        {
            return false;
        }
        if (canContinueApproach?.Invoke(active, target) == true)
        {
            return false;
        }
        state.Active = null;
        state.Status = "Replanning after entering a deferred native approach region";
        return true;
    }

    private bool TryContinueAdmittedCycleApproach(Ff7AutoPlayActiveEvent active, Ff7AutoPlayTarget target, Ff7AutoPlayObservation observation, Ff7AutoplayDriver driver, IReadOnlyDictionary<string, string> facts, out Ff7AutoplayInput input)
    {
        input = default;
        HashSet<string> admittedCycles = ReadAdmittedCycleGuards(State, active, target, observation, guard =>
        {
            string physical = driver.ReadPhysicalAvailabilityFingerprint(target, guard.BlockingEntities);
            return ReadMovementFailureFingerprint(physical, guard.MovementFailure, session.Host.Process.Actors, guard.BlockingEntities, currentSource: false, retainedSourceInside: false);
        });
        if (admittedCycles.Count == 0)
        {
            return false;
        }
        AutoPlayNavigationVisit navigation = State.Coverage.NavigationVisits.Single(visit => visit.Key == observation.NavigationKey);
        AutoPlayEvent observed = navigation.Events.Single(item => item.Id == target.Id);
        AutoPlayEventAvailability availability = ReadComponentAvailability(State, navigation, observed, facts, includeRefusal: (key, _) => !admittedCycles.Contains(key));
        if (availability.UnavailableVariants.Contains(target.Id))
        {
            return false;
        }
        driver.ConfigureSpatialBoundaries(observation.Events, target);
        input = driver.ChooseTargetInput(target, observation.Events, run => PrepareEntryInput(target, ChooseOrdinaryTargetInput(driver, target, run)));
        return driver.PlanningPending || driver.PlannedActionAdvances == true || driver.AdmitsSelectedTalkApproach(target, input);
    }

    internal static HashSet<string> ReadAdmittedCycleGuards(Ff7AutoPlayState state, Ff7AutoPlayActiveEvent active, Ff7AutoPlayTarget target, Ff7AutoPlayObservation observation, Func<Ff7AutoPlayNavigationGuard, string> readOutsideFingerprint)
    {
        HashSet<string> admittedCycles = [];
        if (active.Accepted || active.VisitId != observation.VisitId || active.Generation != observation.Generation || active.NavigationKey != observation.NavigationKey || active.EventId != target.Id || active.ConditionFingerprint != target.ConditionFingerprint || active.Entity != target.Entity || active.Script != target.Script || active.Kind != target.Kind || active.EntryGuardFingerprints is null)
        {
            return admittedCycles;
        }
        foreach (var entry in active.EntryGuardFingerprints)
        {
            if (!state.NavigationGuards.TryGetValue(entry.Key, out var guard) || guard.VisitId != active.VisitId || guard.Entity != target.Entity || guard.NavigationKey != observation.NavigationKey || guard.EventId != target.Id || guard.MovementFailure is not { } failure || guard.MovementSourceInside != true || entry.Key != ReadMovementFailureGuardKey(observation.NavigationKey, target.Id, "movement-cycle", failure))
            {
                continue;
            }
            if (entry.Value == readOutsideFingerprint(guard))
            {
                admittedCycles.Add(entry.Key);
            }
        }
        return admittedCycles;
    }

    private Ff7AutoplayInput ChooseOrdinaryTargetInput(Ff7AutoplayDriver driver, Ff7AutoPlayTarget target, bool run)
    {
        return target.Kind switch
        {
            Ff7AutoPlayTargetKind.ActorTalk => driver.TalkTo(target.Entity, run),
            Ff7AutoPlayTargetKind.ActorContact => driver.Contact(target.Entity, run),
            Ff7AutoPlayTargetKind.Gateway => driver.GatewayAt(target.GatewayIndex!.Value, run),
            Ff7AutoPlayTargetKind.AutonomousSpatial => driver.Approach(target.Position, target.Triangle, run),
            _ => ChooseLineInput(driver, target, run)
        };
    }

    internal static string ReadMovementFailureGuardKey(string navigationKey, string eventId, string kind, Ff7AutoPlayMovementFailure? failure, IReadOnlyList<int>? blockingEntities = null)
    {
        string key = navigationKey + ":" + eventId + ":" + kind;
        if (failure is null)
        {
            return key;
        }
        byte[] scope = JsonSerializer.SerializeToUtf8Bytes(new { Region = failure, Blockers = blockingEntities?.Distinct().Order().ToArray() ?? [] });
        return key + ":" + Convert.ToHexString(SHA256.HashData(scope));
    }

    internal static void RestoreUnscopedMovementFailures(Ff7AutoPlayState state)
    {
        foreach (var visit in state.Coverage.Visits)
        {
            foreach (var item in visit.Events.Where(item => item.BlockKind == AutoPlayBlockKind.Walking && item.BlockedReason == "Physical navigation repeated a bounded native position cycle."))
            {
                // Older observation caches recorded a local search failure as permanent execution.
                // They have no position witness with which to retain that optional deferral.
                item.BlockedReason = null;
                item.BlockKind = null;
                item.WalkingSearchRevision = null;
                if (!item.Completed && item.Event.ConditionSignature is string signature)
                {
                    item.Conditions?.ConsumedEntries.Remove(signature);
                }
            }
        }
    }

    internal static string ReadMovementFailureFingerprint(string physical, Ff7AutoPlayMovementFailure? failure, IReadOnlyList<Ff7Actor> actors, IReadOnlyList<int>? blockingEntities = null, bool currentSource = true, IReadOnlyCollection<string>? retainedFingerprints = null, bool? retainedSourceInside = null)
    {
        if (failure is null)
        {
            return physical;
        }
        StringBuilder witness = new($"{physical}|movement-entry-admission:2");
        foreach (Ff7Actor actor in actors.Where(actor => !actor.PlayerControlled && actor.CollisionEnabled && (blockingEntities is not { Count: > 0 } || blockingEntities.Contains(actor.Entity))).OrderBy(actor => actor.Entity))
        {
            witness.Append($"|occupancy:{actor.Entity}:{actor.Model}:{actor.X4096}:{actor.Y4096}:{actor.Z4096}:{actor.Triangle}:{actor.CollisionRadius}");
        }
        string previous = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(witness.ToString())));
        if (!currentSource && retainedSourceInside is null && retainedFingerprints?.Contains(previous) == true)
        {
            // Retained components have no new native source-position observation.
            return previous;
        }
        bool inside = currentSource ? ReadMovementSourceInside(failure, actors.Single(actor => actor.PlayerControlled)) : retainedSourceInside ?? throw new InvalidOperationException("A remote movement region requires its retained native source observation.");
        witness.Append($"|movement-region-membership:1:{inside}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(witness.ToString())));
    }

    internal static bool ReadMovementSourceInside(Ff7AutoPlayMovementFailure failure, Ff7Actor source)
    {
        return source.X4096 >= failure.MinX && source.X4096 <= failure.MaxX && source.Y4096 >= failure.MinY && source.Y4096 <= failure.MaxY && (failure.MinZ is null || source.Z4096 >= failure.MinZ) && (failure.MaxZ is null || source.Z4096 <= failure.MaxZ);
    }
}
