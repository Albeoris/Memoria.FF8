using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed record Ff7AutoPlayWorldChunk(int X, int Z);
public sealed record Ff7AutoPlayWorldDispatchReceipt(string VisitId, string EventId, int Entry, int CallDepth, long Tick, int[] HandoffOperations, Ff7WorldFieldDeparture? Departure = null, int? DestinationField = null, int? DepartureStory = null);
public sealed class Ff7AutoPlayWorldVisitState
{
    public string ProgramIdentity { get; set; } = string.Empty;
    public bool DiscoveryComplete { get; set; }
    public string? DiscoveryContextFingerprint { get; set; }
    public List<Ff7AutoPlayWorldChunk> ObservedChunks { get; set; } = [];
    public WorldVariableRead[] GlobalReads { get; set; } = [];
    public string? GlobalFingerprint { get; set; }
    public HashSet<string> PhysicalGuards { get; set; } = new(StringComparer.Ordinal);
    public HashSet<string> SourceProgressEntries { get; set; } = new(StringComparer.Ordinal);
}

public sealed partial class Ff7AutoPlayPolicy
{
    private Ff7WorldRuntime? observedWorld;
    private readonly Dictionary<Ff7AutoPlayWorldChunk, Ff7AutoPlayWorldFrontierCatalog> worldCatalogs = [];
    private readonly Queue<Ff7AutoPlayWorldChunk> worldChunkQueue = new();
    private readonly HashSet<Ff7AutoPlayWorldChunk> queuedWorldChunks = [];
    private sealed record WorldComponentDeferral(string Context, HashSet<Ff7WorldTerrainReference> Faces, string PhysicalFingerprint);
    private readonly Dictionary<string, WorldComponentDeferral> worldComponentDeferrals = new(StringComparer.Ordinal);
    private string? worldVisit;

    private void ObserveWorldAllocation()
    {
        Ff7WorldRuntime? world = session.World;
        if (ReferenceEquals(observedWorld, world))
        {
            return;
        }
        if (observedWorld is not null)
        {
            observedWorld.NativeMeshDispatched -= ObserveWorldDispatch;
            observedWorld.NativeHostOperationObserved -= ObserveWorldHostOperation;
        }
        observedWorld = world;
        worldComponentDeferrals.Clear();
        worldCatalogs.Clear();
        worldChunkQueue.Clear();
        queuedWorldChunks.Clear();
        worldVisit = null;
        if (world is not null)
        {
            world.NativeMeshDispatched += ObserveWorldDispatch;
            world.NativeHostOperationObserved += ObserveWorldHostOperation;
            if (world.Process.Dialogue?.RequestId != State.WorldDialogueOwner?.Id)
            {
                State.WorldDialogueOwner = null;
            }
            if (world.Process.FieldDeparture is null && State.Active?.Kind == Ff7AutoPlayTargetKind.WorldMesh && State.WorldDispatch is { } receipt)
            {
                State.WorldDispatch = receipt with { Departure = null, DestinationField = null };
            }
        }
    }

    private Ff7AutoplayInput ChooseWorldInput(Ff7AutoplayDriver driver)
    {
        ObserveWorldAllocation();
        Ff7WorldRuntime world = session.World!;
        Ff7AutoPlayObservation observation = ObserveWorldPlanner(world);
        if (State.Active is { Kind: not Ff7AutoPlayTargetKind.WorldMesh } field)
        {
            if (IsAcceptedWorldArrival(field, world.Process, session.Host.Process.Transition))
            {
                CompleteActive(observation, field, changedField: true);
                if (State.PendingRestore is not null)
                {
                    return default;
                }
            }
            else if (field.Generation != session.FieldGeneration)
            {
                State.Status = "A different native event interrupted the planned contact";
                State.Active = null;
                Publish();
            }
            else
            {
                State.Status = "Waiting for the native field-to-world source owner";
                Publish();
                return default;
            }
        }
        if (State.Active is { Kind: Ff7AutoPlayTargetKind.WorldMesh, Accepted: true } active && State.WorldDispatch is { } dispatch)
        {
            WorldEntitySnapshot system = world.Machine.Capture().System;
            bool returned = HasWorldControlReturned(dispatch, system);
            if (dispatch.Departure is null && returned)
            {
                State.Active = active with { Returned = true };
                CompleteActive(observation, State.Active, changedField: false);
                State.WorldDispatch = null;
            }
            else
            {
                State.Status = "Waiting for the accepted native world callback";
                Publish();
                return default;
            }
        }
        if (State.PendingRestore is not null)
        {
            return default;
        }
        if (!observation.CanAcceptInput)
        {
            return default;
        }
        if (State.Active is null && State.Choice is not null && choices.CompleteAtControlReturn())
        {
            Publish();
            return default;
        }
        Ff7AutoPlayWorldVisitState visited = State.WorldVisits[observation.VisitId];
        Ff7AutoPlayWorldMovementState movement = world.CaptureAutoPlayMovementState();
        string physical = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Ff7AutoplayDriver.WorldMovementKey(movement))));
        string componentContext = visited.DiscoveryContextFingerprint + ":" + world.ReadAutoPlayNavigationContext();
        Dictionary<string, string> physicalGuards = new(StringComparer.Ordinal);
        foreach (string key in visited.PhysicalGuards)
        {
            if (worldComponentDeferrals.TryGetValue(key, out WorldComponentDeferral? deferred) && deferred.Context == componentContext && movement.Input.GroundTriangle is { } ground && deferred.Faces.Contains(ground))
            {
                physicalGuards[key] = deferred.PhysicalFingerprint;
            }
            else
            {
                worldComponentDeferrals.Remove(key);
                physicalGuards[key] = physical;
            }
        }
        planner.RefreshUnavailable(observation.VisitId, physicalGuards);
        if (State.Active is { Kind: Ff7AutoPlayTargetKind.WorldMesh, Accepted: false } && State.NavigationGoal is { EventId: string goalEvent } travelGoal && State.Coverage.NavigationVisits.SingleOrDefault(visit => visit.Key == travelGoal.NavigationKey) is { } goalVisit && State.Coverage.Visits.SingleOrDefault(visit => visit.Visit.Key == goalVisit.VisitKey)?.Events.SingleOrDefault(item => item.Event.Id == goalEvent) is { Available: false })
        {
            State.Active = null;
            State.NavigationGoal = null;
        }
        if (State.Active is null)
        {
            AutoPlayPlanStep? next = planner.PlanNavigation(observation.NavigationKey, preferredGoal: State.NavigationGoal);
            if (ShouldDiscoverWorldChunk(State, observation.VisitId, next, !visited.DiscoveryComplete && worldChunkQueue.Count != 0) && ObserveNextWorldChunk(world))
            {
                State.Status = "Discovering the next native world collision chunk";
                Publish();
                return default;
            }
            if (next is null)
            {
                if (ObserveNextWorldChunk(world))
                {
                    State.Status = "Discovering the next native world collision chunk";
                    Publish();
                    return default;
                }
                string? incomplete = worldCatalogs.Values.SelectMany(catalog => catalog.Scripts).FirstOrDefault(script => !script.Complete)?.Reason;
                throw new NotSupportedException(incomplete ?? "No reachable unconsumed field-entry frontier remains in the bounded native world catalog.");
            }
            if (!TryWorldSource(next.Event.Id, out Ff7AutoPlayWorldChunk chunk, out int script))
            {
                throw new InvalidDataException("The shared world plan has no native mesh/script source identity.");
            }
            ObserveWorldChunk(world, chunk);
            Ff7AutoPlayWorldScriptObservation target = worldCatalogs[chunk].Scripts.SingleOrDefault(item => item.Script == script) ?? throw new NotSupportedException("The recorded native world dispatch is absent from the current collision allocation.");
            if (!target.Complete)
            {
                throw new NotSupportedException(target.Reason ?? "The native world frontier has an unsupported source dependency.");
            }
            observation = ObserveWorldPlanner(world);
            string condition = WorldCondition(target);
            planner.BeginEvent(observation.VisitId, target.Id);
            State.NavigationGoal = next.Goal;
            State.Active = new(observation.VisitId, target.Id, condition, session.FieldGeneration, -1, script, Ff7AutoPlayTargetKind.WorldMesh, null, Story(), session.CaptureEventProgressWitness(), NavigationKey: observation.NavigationKey);
        }
        Ff7AutoPlayActiveEvent selected = State.Active!;
        if (!TryWorldSource(selected.EventId, out Ff7AutoPlayWorldChunk source, out int selectedScript))
        {
            throw new InvalidDataException("The active world event lacks its native dispatch source.");
        }
        if (!worldCatalogs.ContainsKey(source))
        {
            ObserveWorldChunk(world, source);
        }
        Ff7AutoPlayWorldScriptObservation goal = worldCatalogs[source].Scripts.Single(item => item.Script == selectedScript);
        Ff7AutoPlayWorldNavigationResult result = driver.ChooseWorldInput(goal);
        if (result.Status == Ff7AutoPlayWorldNavigationStatus.Unsupported)
        {
            throw new NotSupportedException(result.Reason ?? "The current native world movement mechanism has no physical preview contract.");
        }
        if (result.Status == Ff7AutoPlayWorldNavigationStatus.Blocked)
        {
            string key = observation.VisitId + ":" + selected.EventId + ":physical";
            visited.PhysicalGuards.Add(key);
            if (result.DeferredComponent is { Length: > 0 } component)
            {
                worldComponentDeferrals[key] = new(componentContext, component.ToHashSet(), physical);
            }
            planner.MarkUnavailable(observation.VisitId, selected.EventId, new(selected.ConditionFingerprint, observation.VisitId, key, physical, selected.EventId));
            State.Active = null;
            State.Status = result.Reason ?? "Replanning after the bounded native world search found no admitted route";
            Publish();
            return default;
        }
        if (!result.HasInput)
        {
            throw new NotSupportedException(result.Reason ?? "The bounded native world search did not establish an advancing physical input.");
        }
        State.Status = $"World {world.Resources.World}: {selected.EventId}, {result.Status}";
        Publish();
        return result.Input;
    }

    private Ff7AutoPlayObservation ObserveWorldPlanner(Ff7WorldRuntime world)
    {
        Ff7WorldProcess native = world.Process;
        WorldSnapshot vm = native.Vm;
        worldVisit ??= $"ff7-world:{world.Resources.World}:{vm.ProgramIdentity}";
        if (!State.WorldVisits.TryGetValue(worldVisit, out Ff7AutoPlayWorldVisitState? visited))
        {
            State.WorldVisits[worldVisit] = visited = new() { ProgramIdentity = vm.ProgramIdentity };
        }
        string discoveryContext = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            native.WorldId,
            native.WorldMapState,
            native.ControlledModel,
            vm.ProgramIdentity,
            Models = vm.Models.Select(model => new { model.Model, model.ScriptDisabled, model.Detached }).OrderBy(model => model.Model).ToArray()
        })));
        if (ObserveWorldDiscoveryContext(visited, discoveryContext))
        {
            // These are disposable catalog/search observations; shared execution coverage is retained.
            worldCatalogs.Clear();
            worldChunkQueue.Clear();
            queuedWorldChunks.Clear();
        }
        Ff7WorldEntity? player = native.Entities.SingleOrDefault(entity => entity.Model == world.ControlledModel);
        if (player is not null)
        {
            ObserveWorldChunk(world, new(player.X >> 13, player.Z >> 13));
        }
        AutoPlayVisitCoverage<Ff7AutoPlayDeferral>? previous = State.Coverage.Visits.SingleOrDefault(item => item.Visit.Key == worldVisit);
        Dictionary<string, AutoPlayEvent> events = previous?.Events.ToDictionary(item => item.Event.Id, item => item.Event, StringComparer.Ordinal) ?? new(StringComparer.Ordinal);
        foreach (Ff7AutoPlayWorldFrontierCatalog catalog in worldCatalogs.Values)
        {
            foreach (Ff7AutoPlayWorldScriptObservation script in catalog.Scripts)
            {
                events.Remove(script.Id);
                if (script.Complete && script.Analysis.MayEnterField)
                {
                    events[script.Id] = new(script.Id, AutoPlayEventKind.Transition, ConditionSignature: WorldCondition(script));
                }
            }
        }
        planner.Observe(new(worldVisit, vm.ProgramIdentity), events.Values.ToArray());
        ReobserveUnattributedWorldTransfers(State.Coverage.Visits.Single(visit => visit.Visit.Key == worldVisit), worldCatalogs.Values.SelectMany(catalog => catalog.Scripts), visited.SourceProgressEntries);
        string navigation = worldVisit + ":nav:world";
        planner.ObserveNavigation(worldVisit, navigation, events.Values.ToArray());
        visited.GlobalReads = visited.GlobalReads.Concat(worldCatalogs.Values.SelectMany(catalog => catalog.Scripts).SelectMany(script => script.Analysis.Reads).Where(read => (read.Opcode & 3) == 0)).Distinct().OrderBy(read => read.Opcode).ThenBy(read => read.Operand).ToArray();
        visited.GlobalFingerprint = world.Machine.ReadPredicateFingerprint(visited.GlobalReads);
        bool ready = world.Process.Initialized && world.Process.MovementEnabled && world.Process.ControlLock == 0 && world.Process.MainState == 1 && world.Process.Dialogue is not { Completed: false };
        return new(worldVisit, vm.ProgramIdentity, session.FieldGeneration, [], ready, true) { NavigationKey = navigation };
    }

    internal static void ReobserveUnattributedWorldTransfers(AutoPlayVisitCoverage<Ff7AutoPlayDeferral> visit, IEnumerable<Ff7AutoPlayWorldScriptObservation> scripts, IReadOnlySet<string>? sourceProgressEntries = null)
    {
        foreach (Ff7AutoPlayWorldScriptObservation script in scripts.Where(script => script.Complete && script.Analysis.IsPureFieldTransfer))
        {
            AutoPlayEventCoverage<Ff7AutoPlayDeferral>? coverage = visit.Events.SingleOrDefault(item => item.Event.Id == script.Id);
            string condition = WorldCondition(script);
            if (coverage is { Completed: true } && sourceProgressEntries?.Contains(script.Id + ":" + condition) != true && coverage.Routes.Any(route => route.ConditionSignature == condition && route.ObservedStoryChanged == true) && !coverage.Routes.Any(route => route.ConditionSignature == condition && route.ObservedStoryChanged == false))
            {
                // Rediscover source-only travel evidence; retain historical observations and consumed entries.
                coverage.RouteNeedsRediscovery = true;
            }
        }
    }

    private void ObserveWorldChunk(Ff7WorldRuntime world, Ff7AutoPlayWorldChunk chunk)
    {
        Ff7AutoPlayWorldFrontierCatalog catalog = world.ReadAutoPlayFrontierCatalog(chunk.X, chunk.Z);
        worldCatalogs[chunk] = catalog;
        Ff7AutoPlayWorldVisitState visited = State.WorldVisits[worldVisit!];
        if (!visited.ObservedChunks.Contains(chunk))
        {
            visited.ObservedChunks.Add(chunk);
        }
        QueueWorldChunk(chunk.X - 1, chunk.Z);
        QueueWorldChunk(chunk.X + 1, chunk.Z);
        QueueWorldChunk(chunk.X, chunk.Z - 1);
        QueueWorldChunk(chunk.X, chunk.Z + 1);
    }

    private void QueueWorldChunk(int x, int z)
    {
        Ff7AutoPlayWorldChunk chunk = new(x, z);
        try
        {
            WorldProgram.MeshDispatchHeader(x, z, 0);
            if (!worldCatalogs.ContainsKey(chunk) && queuedWorldChunks.Add(chunk))
            {
                worldChunkQueue.Enqueue(chunk);
            }
        }
        catch (ArgumentOutOfRangeException)
        {
            // The native dispatch domain bounds lazy discovery; it is not a physical admission proof.
        }
    }

    private bool ObserveNextWorldChunk(Ff7WorldRuntime world)
    {
        Ff7AutoPlayWorldVisitState visited = State.WorldVisits[worldVisit!];
        if (visited.DiscoveryComplete)
        {
            return false;
        }
        while (worldChunkQueue.TryDequeue(out Ff7AutoPlayWorldChunk? chunk))
        {
            if (!worldCatalogs.ContainsKey(chunk))
            {
                ObserveWorldChunk(world, chunk);
                return true;
            }
        }
        visited.DiscoveryComplete = true;
        return false;
    }

    internal static bool ObserveWorldDiscoveryContext(Ff7AutoPlayWorldVisitState visited, string fingerprint)
    {
        if (visited.DiscoveryContextFingerprint == fingerprint)
        {
            return false;
        }
        visited.DiscoveryContextFingerprint = fingerprint;
        visited.DiscoveryComplete = false;
        return true;
    }

    internal static bool ShouldDiscoverWorldChunk(Ff7AutoPlayState state, string visitKey, AutoPlayPlanStep? next, bool hasQueuedChunks)
    {
        if (!hasQueuedChunks || next is { IsTravel: false })
        {
            return false;
        }
        AutoPlayVisitCoverage<Ff7AutoPlayDeferral>? coverage = state.Coverage.Visits.SingleOrDefault(visit => visit.Visit.Key == visitKey);
        return coverage is null || !coverage.Events.Any(item => item.Available && !item.Unavailable && !item.PolicyDeferred && item.BlockedReason is null && AutoPlayPlanner<Ff7AutoPlayDeferral>.NeedsExecution(item));
    }

    private void ObserveWorldDispatch(Ff7WorldMeshDispatchObservation dispatch)
    {
        if (!dispatch.Accepted || observedWorld is not { } world || State.Active is not { Kind: Ff7AutoPlayTargetKind.WorldMesh } active)
        {
            return;
        }
        string id = $"mesh:{dispatch.ChunkX}:{dispatch.ChunkZ}:script:{dispatch.Event + 3}";
        if (active.EventId != id)
        {
            return;
        }
        Ff7AutoPlayWorldScriptObservation source = world.ReadAutoPlayFrontierCatalog(dispatch.ChunkX, dispatch.ChunkZ).Scripts.Single(script => script.Id == id);
        string condition = WorldCondition(source);
        planner.Observe(new(active.VisitId, world.Machine.Capture().ProgramIdentity), State.Coverage.Visits.Single(visit => visit.Visit.Key == active.VisitId).Events.Select(item => item.Event.Id == id ? item.Event with { ConditionSignature = condition } : item.Event).ToArray());
        planner.BeginEvent(active.VisitId, id);
        State.Active = active with { Accepted = true, ConditionFingerprint = condition, BeforeStory = Story(), BeforeWitness = session.CaptureEventProgressWitness() };
        State.WorldDispatch = new(active.VisitId, id, dispatch.Entry, dispatch.CallDepth, dispatch.Tick, source.Analysis.Transitions.Select(transition => transition.ProgramCounter).Distinct().ToArray());
        Publish();
    }

    private void ObserveWorldHostOperation(WorldHostRequest request)
    {
        bool changed = false;
        if (request.Command.Operation == WorldOperation.Choice && State.WorldDialogueOwner != request)
        {
            State.WorldDialogueOwner = request;
            changed = true;
        }
        if (observedWorld is { } world && State.Active is { Kind: Ff7AutoPlayTargetKind.WorldMesh, Accepted: true } worldActive && State.WorldDispatch is { } dispatch && dispatch.HandoffOperations.Contains(request.ProgramCounter) && request.Command.Operation is WorldOperation.FieldTransition or WorldOperation.FieldTransitionSelector && world.Process.FieldDeparture is { } departure)
        {
            int? destination = departure.Operation == WorldOperation.FieldTransition ? world.ResolveFieldEntry(departure.Selector).Map : null;
            Ff7AutoPlayWorldDispatchReceipt updated = dispatch with { Departure = departure, DestinationField = destination };
            if (updated != dispatch || !worldActive.TransitionOwned)
            {
                State.WorldDispatch = updated;
                State.Active = worldActive with { TransitionOwned = true };
                changed = true;
            }
        }
        if (changed)
        {
            Publish();
        }
    }

    private void ObserveWorldFieldDeparture(Ff7WorldFieldDeparture departure, short story)
    {
        if (State.Active is { Kind: Ff7AutoPlayTargetKind.WorldMesh, Accepted: true } active && State.WorldDispatch is { } dispatch && dispatch.EventId == active.EventId && dispatch.Departure == departure)
        {
            State.WorldDispatch = dispatch with { DepartureStory = story };
            Publish();
        }
    }

    internal static bool IsAcceptedWorldArrival(Ff7AutoPlayActiveEvent active, Ff7WorldProcess world, FieldTransitionCommand? transition)
    {
        return active.Kind != Ff7AutoPlayTargetKind.WorldMesh && (active.Accepted && active.TransitionOwned || active.Kind == Ff7AutoPlayTargetKind.Gateway && active.DestinationField == world.EntrySelector && (transition is null || transition.Map == world.EntrySelector));
    }

    internal static bool IsAcceptedWorldDeparture(Ff7AutoPlayActiveEvent active, Ff7AutoPlayWorldDispatchReceipt? receipt, long generation, int field)
    {
        return active.Kind == Ff7AutoPlayTargetKind.WorldMesh && active.Accepted && active.Generation != generation && receipt is { Departure.Operation: WorldOperation.FieldTransition } && receipt.EventId == active.EventId && receipt.DestinationField == field;
    }

    internal static bool HasWorldControlReturned(Ff7AutoPlayWorldDispatchReceipt receipt, WorldEntitySnapshot system)
    {
        return system.Calls.Length < receipt.CallDepth || receipt.CallDepth == 0 && (system.Priority != 3 || system.ProgramCounter == 0);
    }

    internal static string WorldCondition(Ff7AutoPlayWorldScriptObservation source) => source.PredicateFingerprint + ":geometry:" + source.GeometryFingerprint;

    private static bool TryWorldSource(string id, out Ff7AutoPlayWorldChunk chunk, out int script)
    {
        string[] parts = id.Split(':');
        if (parts.Length == 5 && parts[0] == "mesh" && parts[3] == "script" && int.TryParse(parts[1], out int x) && int.TryParse(parts[2], out int z) && int.TryParse(parts[4], out script))
        {
            chunk = new(x, z);
            return true;
        }
        chunk = null!;
        script = 0;
        return false;
    }
}
