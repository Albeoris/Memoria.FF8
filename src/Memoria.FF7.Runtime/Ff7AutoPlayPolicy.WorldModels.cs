using Memoria.AutoPlay;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayPolicy
{
    private readonly Dictionary<string, Ff7AutoPlayWorldModelScriptObservation> worldModelCatalog = new(StringComparer.Ordinal);

    private void ObserveWorldModelFrontiers(Ff7WorldRuntime world)
    {
        worldModelCatalog.Clear();
        foreach (Ff7AutoPlayWorldModelScriptObservation source in world.ReadAutoPlayModelFrontiers())
        {
            worldModelCatalog.Add(source.Id, source);
        }
    }

    private bool TryBeginWorldModelEvent(AutoPlayPlanStep next, Ff7AutoPlayObservation observation, IReadOnlyDictionary<string, string> physicalGuards)
    {
        if (!next.Event.Id.StartsWith("model:", StringComparison.Ordinal))
        {
            return false;
        }
        if (!worldModelCatalog.TryGetValue(next.Event.Id, out Ff7AutoPlayWorldModelScriptObservation? target) || !target.Complete)
        {
            throw new NotSupportedException("The shared world plan has no currently offered native model dispatch source.");
        }
        var coverage = State.Coverage.Visits.Single(visit => visit.Visit.Key == observation.VisitId).Events.Single(item => item.Event.Id == target.Id);
        string? availability = ReadWorldSemanticAvailability(coverage, coverage.Event, AutoPlayPlanner<Ff7AutoPlayDeferral>.ReadAvailability(coverage, physicalGuards)).Fingerprint;
        planner.BeginEvent(observation.VisitId, target.Id);
        State.NavigationGoal = next.Goal;
        State.Active = new(observation.VisitId, target.Id, WorldModelCondition(target), session.FieldGeneration, target.Model, target.Event, Ff7AutoPlayTargetKind.WorldModel, null, Story(), session.CaptureEventProgressWitness(), NavigationKey: observation.NavigationKey, EntryAvailabilityFingerprint: availability, TravelRoute: next.Route, TravelSelections: next.Route is null ? null : []);
        return true;
    }

    private Ff7AutoPlayWorldNavigationResult ChooseSelectedWorldInput(Ff7AutoplayDriver driver, Ff7WorldRuntime world, Ff7AutoPlayActiveEvent selected)
    {
        if (selected.Kind == Ff7AutoPlayTargetKind.WorldModel)
        {
            if (!worldModelCatalog.TryGetValue(selected.EventId, out Ff7AutoPlayWorldModelScriptObservation? target))
            {
                return new(Ff7AutoPlayWorldNavigationStatus.Unsupported, default, 0, "The selected native model contact source is no longer offered.");
            }
            return driver.ChooseWorldModelInput(target);
        }
        if (!TryWorldSource(selected.EventId, out Ff7AutoPlayWorldChunk source, out int selectedScript))
        {
            throw new InvalidDataException("The active world event lacks its native dispatch source.");
        }
        if (!worldCatalogs.ContainsKey(source))
        {
            ObserveWorldChunk(world, source);
        }
        return driver.ChooseWorldInput(worldCatalogs[source].Scripts.Single(item => item.Script == selectedScript));
    }

    private void ObserveWorldModelDispatch(Ff7WorldModelDispatchObservation dispatch)
    {
        if (!dispatch.Accepted || observedWorld is not { } world || State.Active is not { Kind: Ff7AutoPlayTargetKind.WorldModel, Accepted: false } active || active.Entity != dispatch.Model || active.Script != dispatch.Event || active.EventId != $"model:{dispatch.Model}:event:{dispatch.Event}")
        {
            return;
        }
        if (!worldModelCatalog.TryGetValue(active.EventId, out Ff7AutoPlayWorldModelScriptObservation? source))
        {
            throw new InvalidOperationException("An accepted native model contact has no admitted source analysis.");
        }
        planner.BeginEvent(active.VisitId, active.EventId);
        State.Active = active with { Accepted = true, ConditionFingerprint = WorldModelCondition(source), BeforeStory = Story(), BeforeWitness = session.CaptureEventProgressWitness() };
        int priority = Math.Clamp(dispatch.Event - 2, 0, 3);
        State.WorldDispatch = new(active.VisitId, active.EventId, dispatch.Entry, dispatch.CallDepth, dispatch.Tick, source.Analysis.Transitions.Select(transition => transition.ProgramCounter).Distinct().ToArray(), Model: dispatch.Model, OwnerPriority: priority, Owners: [new(dispatch.Model, dispatch.Entry, dispatch.CallDepth, priority)]);
        Publish();
    }

    private void ObserveWorldOwnedDispatch(WorldScriptDispatchObservation observation)
    {
        if (State.Active is not { Accepted: true } active || State.WorldDispatch is not { } receipt || active.EventId != receipt.EventId || observation.Priority == 0 || !OwnsWorldDispatchSource(receipt, observation))
        {
            return;
        }
        Ff7AutoPlayWorldOwner owner = new(observation.TargetModel, observation.Entry, observation.CallDepth, observation.Priority);
        Ff7AutoPlayWorldOwner[] retained = receipt.Owners ?? [new(receipt.Model ?? -1, receipt.Entry, receipt.CallDepth, receipt.OwnerPriority)];
        if (!retained.Contains(owner))
        {
            State.WorldDispatch = receipt with { Owners = [.. retained, owner] };
            Publish();
        }
    }

    private static bool OwnsWorldExecutingModel(Ff7AutoPlayWorldDispatchReceipt receipt, int model)
    {
        return receipt.Owners is { } owners ? owners.Any(owner => owner.Model == model) : receipt.Model is null || receipt.Model == model;
    }

    internal static bool OwnsWorldDispatchSource(Ff7AutoPlayWorldDispatchReceipt receipt, WorldScriptDispatchObservation observation)
    {
        Ff7AutoPlayWorldOwner[] owners = receipt.Owners ?? [new(receipt.Model ?? -1, receipt.Entry, receipt.CallDepth, receipt.OwnerPriority)];
        return owners.Any(owner => owner.Model == observation.SourceModel && owner.Priority == observation.SourcePriority && observation.SourceCallDepth >= owner.CallDepth);
    }

    internal static bool HasWorldOwnershipReturned(Ff7AutoPlayWorldDispatchReceipt receipt, WorldSnapshot execution)
    {
        Ff7AutoPlayWorldOwner[] owners = receipt.Owners ?? [new(receipt.Model ?? -1, receipt.Entry, receipt.CallDepth, receipt.OwnerPriority)];
        foreach (Ff7AutoPlayWorldOwner owner in owners)
        {
            WorldEntitySnapshot? current = owner.Model == -1 ? execution.System : execution.Models.SingleOrDefault(entity => entity.Model == owner.Model);
            if (current is not null && !(current.Calls.Length < owner.CallDepth || owner.CallDepth == 0 && (current.Priority != owner.Priority || current.ProgramCounter == 0)))
            {
                return false;
            }
        }
        return true;
    }

    internal static string WorldModelCondition(Ff7AutoPlayWorldModelScriptObservation source) => source.PredicateFingerprint + ":geometry:" + source.GeometryFingerprint;
}
