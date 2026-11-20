using Memoria.AutoPlay;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayPolicy
{
    private Ff7NativePlanningCursor? worldBoardingPlanning;
    private string? worldBoardingPlanningContext;
    private bool worldBoardingCatalogChanged;

    private void CancelWorldBoardingPlanning()
    {
        worldBoardingPlanning?.Dispose();
        worldBoardingPlanning = null;
        worldBoardingPlanningContext = null;
        worldBoardingCatalogChanged = false;
    }

    internal static bool ShouldPlanWorldBoardingPrerequisite(Ff7AutoPlayState state, string visitKey, Func<AutoPlayEvent, AutoPlayEventAvailability?> readAvailability)
    {
        if (state.DestinationField is not null)
        {
            return false;
        }
        AutoPlayVisitCoverage<Ff7AutoPlayDeferral> visit = state.Coverage.Visits.Single(visit => visit.Visit.Key == visitKey);
        return !visit.Events.Any(item => item.Available && item.BlockedReason is null && !item.PolicyDeferred && !HasConsumedWorldEntry(item, item.Event.ConditionSignature) && !HasAttemptedEntryCondition(item, item.Event) && readAvailability(item.Event)?.Unavailable != true);
    }

    internal static IEnumerable<AutoPlayPlanStep?> EnumerateWorldBoardingPrerequisites(string visitKey, string navigationKey, IReadOnlyList<AutoPlayEvent> destinations, IReadOnlyList<AutoPlayEvent> boarding, Func<AutoPlayEvent, AutoPlayEvent, bool> connected, Func<AutoPlayEvent, bool>? prepareDestination = null)
    {
        foreach (AutoPlayEvent destination in destinations)
        {
            if (prepareDestination is not null)
            {
                bool eligible = prepareDestination(destination);
                yield return null;
                if (!eligible)
                {
                    continue;
                }
            }
            foreach (AutoPlayEvent contact in boarding)
            {
                if (connected(contact, destination))
                {
                    yield return new(visitKey, contact, false, Goal: new(navigationKey, destination.Id, destination.ConditionSignature));
                    yield break;
                }
                yield return null;
            }
        }
    }

    private AutoPlayPlanStep? PlanWorldBoardingPrerequisite(Ff7WorldRuntime world, Ff7AutoPlayObservation observation, Ff7AutoplayDriver driver, Func<AutoPlayEvent, AutoPlayEventAvailability?> readAvailability, string? selectedBoarding = null)
    {
        if (State.DestinationField is not null || world.ReadAutoPlaySurfaceVehicleRider() is not null || selectedBoarding is null && !ShouldPlanWorldBoardingPrerequisite(State, observation.VisitId, readAvailability))
        {
            CancelWorldBoardingPlanning();
            return null;
        }
        AutoPlayVisitCoverage<Ff7AutoPlayDeferral> visit = State.Coverage.Visits.Single(visit => visit.Visit.Key == observation.VisitId);
        Dictionary<string, AutoPlayEventCoverage<Ff7AutoPlayDeferral>> coverage = visit.Events.ToDictionary(item => item.Event.Id, StringComparer.Ordinal);
        bool Eligible(AutoPlayEventCoverage<Ff7AutoPlayDeferral> item) => item.Available && item.BlockedReason is null && !item.PolicyDeferred;
        Dictionary<string, (Ff7AutoPlayWorldChunk Chunk, int Script)> targets = new(StringComparer.Ordinal);
        foreach (var item in visit.Events.Where(item => Eligible(item) && item.Event.Kind == AutoPlayEventKind.Transition && !HasConsumedWorldEntry(item, item.Event.ConditionSignature)))
        {
            if (TryWorldSource(item.Event.Id, out var chunk, out int script))
            {
                targets[item.Event.Id] = (chunk, script);
            }
        }
        Dictionary<string, Ff7AutoPlayWorldModelScriptObservation> contacts = worldModelCatalog.Values.Where(contact => (selectedBoarding is null || contact.Id == selectedBoarding) && contact.Complete && contact.Analysis.Transitions.Any(transition => transition.Operation == Memoria.FF7.Events.WorldOperation.BoardTransport && transition.Model is not null) && coverage.TryGetValue(contact.Id, out var item) && Eligible(item) && readAvailability(item.Event)?.Unavailable != true).ToDictionary(contact => contact.Id, StringComparer.Ordinal);
        if (targets.Count == 0 || contacts.Count == 0)
        {
            CancelWorldBoardingPlanning();
            return null;
        }
        AutoPlayEvent[] destinations = targets.Keys.Select(id => coverage[id].Event).OrderBy(target => State.NavigationGoal?.EventId == target.Id ? 0 : 1).ThenBy(target => target.Id, StringComparer.Ordinal).ToArray();
        AutoPlayEvent[] boarding = contacts.Keys.Select(id => coverage[id].Event).OrderBy(contact => contact.Id, StringComparer.Ordinal).ToArray();
        string context = world.Process.Tick + ":" + Ff7AutoplayDriver.WorldMovementKey(world.CaptureAutoPlayMovementState()) + ":" + world.ReadAutoPlayNavigationContext() + ":" + State.NavigationGoal + ":" + string.Join('|', destinations.Concat(boarding).Select(target => target.Id + ":" + target.ConditionSignature + ":" + readAvailability(target)?.Fingerprint));
        if (worldBoardingPlanning is null || worldBoardingPlanningContext != context)
        {
            CancelWorldBoardingPlanning();
            worldBoardingPlanningContext = context;
            Dictionary<string, Ff7AutoPlayWorldScriptObservation> prepared = new(StringComparer.Ordinal);
            worldBoardingPlanning = new(EnumerateWorldBoardingPrerequisites(observation.VisitId, observation.NavigationKey, destinations, boarding, (contact, target) =>
            {
                nativePlanningWork++;
                // A nonpositive projection is advice only; it publishes no native unavailability or completion.
                return world.ReadAutoPlayBoardingAdvice(contacts[contact.Id], prepared[target.Id]).Connected;
            }, target =>
            {
                nativePlanningWork++;
                var source = targets[target.Id];
                if (!worldCatalogs.TryGetValue(source.Chunk, out var catalog))
                {
                    ObserveWorldChunk(world, source.Chunk);
                    catalog = worldCatalogs[source.Chunk];
                    worldBoardingCatalogChanged = true;
                }
                var current = catalog.Scripts.SingleOrDefault(item => item.Script == source.Script);
                if (current is not { Complete: true } || !current.Analysis.MayEnterField || WorldCondition(current) != target.ConditionSignature)
                {
                    // Reobserve changed or unresolved entries; this is not a native route failure.
                    return false;
                }
                prepared[target.Id] = current;
                return true;
            }).GetEnumerator());
        }
        worldBoardingPlanning.Resume(nativePlanningQuantum ?? new());
        if (!worldBoardingPlanning.Completed)
        {
            driver.DeferPlanning();
            return null;
        }
        AutoPlayPlanStep? result = worldBoardingPlanning.Result;
        bool reobserve = result is null && worldBoardingCatalogChanged;
        CancelWorldBoardingPlanning();
        if (reobserve)
        {
            driver.DeferPlanning();
        }
        return result;
    }

    internal static bool RequiresWorldRiderPrerequisite(Memoria.FF7.Events.WorldScriptAnalysis current, Memoria.FF7.Events.WorldScriptAnalysis rider)
    {
        return current.Complete && current.IsInert && rider.Complete && rider.IsPureFieldTransfer && rider.MayEnterField;
    }

    internal sealed class WorldModeCandidate(Ff7AutoPlayWorldScriptObservation target, Ff7AutoPlayWorldModeGoal goal)
    {
        public Ff7AutoPlayWorldScriptObservation Target { get; } = target;
        public Ff7AutoPlayWorldModeGoal Goal { get; } = goal;
        public Ff7AutoPlayWorldModeGuide? Guide { get; set; }
        public Ff7WorldModeTopologyCursor? Search { get; set; }
        public string? SearchContext { get; set; }
    }

    private readonly Dictionary<string, WorldModeCandidate> worldModeCatalog = new(StringComparer.Ordinal);
    private readonly Queue<WorldModeCandidate> worldModeQueue = new();
    private Ff7WorldRuntime? worldModeOwner;
    private int worldModeModel = -1;
    private void CancelWorldModePlanning()
    {
        DisposeWorldModeCandidates(worldModePrerequisites.Values, worldModeCatalog.Values);
        worldModePrerequisites.Clear();
        worldModeCatalog.Clear();
        worldModeQueue.Clear();
        worldModeCandidatesObserved = false;
        worldModeTopologyPending = false;
    }

    internal static void DisposeWorldModeCandidates(IEnumerable<WorldModeCandidate> prerequisites, IEnumerable<WorldModeCandidate> catalog)
    {
        foreach (WorldModeCandidate candidate in prerequisites.Concat(catalog).Distinct())
        {
            candidate.Search?.Dispose();
        }
    }

    private bool worldModeCandidatesObserved;
    private bool worldModeTopologyPending;
    private readonly Dictionary<string, WorldModeCandidate> worldModePrerequisites = new(StringComparer.Ordinal);

    private void ObserveWorldModeGoals(Ff7WorldRuntime world, string visitKey, Dictionary<string, AutoPlayEvent> events)
    {
        worldModeTopologyPending = false;
        foreach (string id in events.Keys.Where(id => id.StartsWith("mode:", StringComparison.Ordinal)).ToArray())
        {
            events.Remove(id);
        }
        if (!ReferenceEquals(worldModeOwner, world) || worldModeModel != world.ControlledModel)
        {
            DisposeWorldModeCandidates(worldModePrerequisites.Values, worldModeCatalog.Values);
            worldModePrerequisites.Clear();
            worldModeCatalog.Clear();
            worldModeQueue.Clear();
            worldModeCandidatesObserved = false;
            worldModeOwner = world;
            worldModeModel = world.ControlledModel;
        }
        foreach (var retained in worldModePrerequisites.ToArray())
        {
            WorldModeCandidate item = retained.Value;
            Ff7AutoPlayWorldScriptObservation? current = world.ReadAutoPlayFrontierCatalog(item.Goal.Chunk.X, item.Goal.Chunk.Z).Scripts.SingleOrDefault(target => target.Id == item.Target.Id);
            bool valid = current is { Complete: true } && WorldCondition(current) == WorldCondition(item.Target) && RequiresWorldRiderPrerequisite(world.ReadAutoPlayMeshEntryForModel(current, world.ControlledModel), world.ReadAutoPlayMeshEntryForModel(current, item.Goal.RiderModel));
            if (!valid)
            {
                item.Search?.Dispose();
                worldModePrerequisites.Remove(retained.Key);
                worldModeCatalog.Remove("mode:surface-rider:" + retained.Key);
                worldModeCandidatesObserved = false;
                if (State.Active is { WorldControl: null, WorldModeGoal: { } plannedMode } && plannedMode.TargetEventId == retained.Key)
                {
                    State.Active = null;
                }
            }
        }
        if (!worldModeCandidatesObserved)
        {
            worldModeQueue.Clear();
        }
        if (State.Active is { WorldModeGoal: { } ownedGoal } active)
        {
            if (!worldModeCatalog.ContainsKey(active.EventId))
            {
                Ff7AutoPlayWorldScriptObservation target = world.ReadAutoPlayFrontierCatalog(ownedGoal.Chunk.X, ownedGoal.Chunk.Z).Scripts.Single(item => item.Id == ownedGoal.TargetEventId);
                worldModeCatalog.Add(active.EventId, new(target, ownedGoal));
            }
            events[active.EventId] = new(active.EventId, AutoPlayEventKind.Interaction, ConditionSignature: active.ConditionFingerprint);
            return;
        }
        if (!world.Process.Initialized || !world.Process.MovementEnabled || world.Process.MainState != 1 || world.ReadAutoPlaySurfaceVehicleRider() is not int rider)
        {
            DisposeWorldModeCandidates(worldModePrerequisites.Values, worldModeCatalog.Values);
            worldModePrerequisites.Clear();
            worldModeCatalog.Clear();
            worldModeQueue.Clear();
            worldModeCandidatesObserved = false;
            return;
        }
        {
            List<(WorldModeCandidate Candidate, bool Visited, double Distance)> candidates = [];
            Ff7WorldEntity player = world.CaptureAutoPlayMovementState().Player;
            foreach (AutoPlayEvent observed in events.Values.ToArray())
            {
                if (!TryWorldSource(observed.Id, out Ff7AutoPlayWorldChunk chunk, out int script))
                {
                    continue;
                }
                Ff7AutoPlayWorldScriptObservation? target = world.ReadAutoPlayFrontierCatalog(chunk.X, chunk.Z).Scripts.SingleOrDefault(item => item.Script == script);
                if (target is not { Complete: true } || !target.Analysis.IsPureFieldTransfer || target.FieldEntries.Length == 0)
                {
                    continue;
                }
                var currentEntry = world.ReadAutoPlayMeshEntryForModel(target, world.ControlledModel);
                var riderEntry = world.ReadAutoPlayMeshEntryForModel(target, rider);
                if (!RequiresWorldRiderPrerequisite(currentEntry, riderEntry))
                {
                    continue;
                }
                ushort[] destinations = riderEntry.Transitions.Where(transition => transition.Operation == Memoria.FF7.Events.WorldOperation.FieldTransition && transition.Selector is not null).Select(transition => world.ResolveFieldEntry(transition.Selector!.Value).Map).Distinct().ToArray();
                if (destinations.Length == 0 || State.DestinationField is ushort desired && !destinations.Contains(desired))
                {
                    continue;
                }
                bool visited = destinations.All(destination => State.Coverage.Visits.Any(visit => visit.Visit.Key.StartsWith($"ff7:{destination}:", StringComparison.Ordinal)));
                double distance = target.Faces.Min(face =>
                {
                    var center = (face.A + face.B + face.C) / 3;
                    double dx = Math.Abs(center.X - player.X);
                    double dz = Math.Abs(center.Z - player.Z);
                    dx = Math.Min(dx, 0x48000 - dx);
                    dz = Math.Min(dz, 0x38000 - dz);
                    return dx * dx + dz * dz;
                });
                candidates.Add((new(target, new(target.Id, chunk, script, rider)), visited, distance));
            }
            foreach (var item in candidates.OrderBy(item => item.Candidate.Target.Id == State.Active?.EventId ? 0 : item.Candidate.Target.Id == State.NavigationGoal?.EventId ? 1 : 2).ThenBy(item => item.Visited).ThenBy(item => item.Distance).ThenBy(item => item.Candidate.Target.Id, StringComparer.Ordinal))
            {
                EnqueueWorldModePrerequisite(worldModeQueue, worldModePrerequisites, item.Candidate, !worldModeCandidatesObserved);
            }
            worldModeCandidatesObserved = true;
        }
        foreach (string targetId in worldModePrerequisites.Keys)
        {
            events.Remove(targetId);
        }
        WorldModeCandidate? selected = State.Active is { Kind: Ff7AutoPlayTargetKind.WorldMesh, Accepted: false } raw && worldModePrerequisites.TryGetValue(raw.EventId, out WorldModeCandidate? prerequisite) ? prerequisite : null;
        worldModeQueue.TryPeek(out WorldModeCandidate? queued);
        Ff7AutoPlayObservation modeObservation = new(visitKey, world.Machine.ProgramIdentity, session.FieldGeneration, [], true, true) { NavigationKey = visitKey + ":nav:world" };
        var modeFacts = RefreshWorldPhysicalAvailability(world, modeObservation).Facts;
        int actionable = worldModeCatalog.Count(entry =>
        {
            AutoPlayEvent current = new(entry.Key, AutoPlayEventKind.Interaction, ConditionSignature: WorldCondition(entry.Value.Target) + ":rider:" + entry.Value.Goal.RiderModel);
            AutoPlayEventCoverage<Ff7AutoPlayDeferral>? coverage = State.Coverage.Visits.SingleOrDefault(visit => visit.Visit.Key == visitKey)?.Events.SingleOrDefault(item => item.Event.Id == entry.Key);
            return IsWorldModeActionable(coverage, current, modeFacts);
        });
        WorldModeCandidate? candidate = SelectWorldModePrerequisite(State.Active, worldModePrerequisites, actionable, queued);
        if (candidate is not null)
        {
            candidate.Guide = ReadWorldModeGuide(world, candidate);
            if (candidate.Guide is null)
            {
                worldModeTopologyPending = true;
            }
            else
            {
                if (worldModeQueue.TryPeek(out WorldModeCandidate? head) && ReferenceEquals(head, candidate))
                {
                    worldModeQueue.Dequeue();
                }
                if (candidate.Guide.TransportGuide.Faces.Length > 0)
                {
                    string modeId = "mode:surface-rider:" + candidate.Target.Id;
                    worldModeCatalog[modeId] = candidate;
                    if (selected is not null && State.Active is { Accepted: false } planned)
                    {
                        State.Active = planned with { EventId = modeId, Kind = Ff7AutoPlayTargetKind.WorldControl, ConditionFingerprint = WorldCondition(candidate.Target) + ":rider:" + candidate.Goal.RiderModel, WorldModeGoal = candidate.Goal };
                    }
                }
                else if (selected is not null)
                {
                    State.Active = null;
                }
            }
        }
        foreach (var entry in worldModeCatalog)
        {
            events[entry.Key] = new(entry.Key, AutoPlayEventKind.Interaction, ConditionSignature: WorldCondition(entry.Value.Target) + ":rider:" + entry.Value.Goal.RiderModel);
        }
    }

    internal static void EnqueueWorldModePrerequisite(Queue<WorldModeCandidate> queue, IDictionary<string, WorldModeCandidate> known, WorldModeCandidate observed, bool rebuild)
    {
        if (known.TryGetValue(observed.Target.Id, out WorldModeCandidate? retained))
        {
            if (rebuild)
            {
                queue.Enqueue(retained);
            }
        }
        else
        {
            known.Add(observed.Target.Id, observed);
            queue.Enqueue(observed);
        }
    }

    internal static bool IsWorldModeActionable(AutoPlayEventCoverage<Ff7AutoPlayDeferral>? coverage, AutoPlayEvent current, IReadOnlyDictionary<string, string> facts)
    {
        if (coverage is null)
        {
            return true;
        }
        if (coverage.BlockedReason is not null || coverage.PolicyDeferred)
        {
            return false;
        }
        AutoPlayEventCoverage<Ff7AutoPlayDeferral> projected = new()
        {
            Event = current,
            Completed = coverage.Completed,
            Conditions = coverage.Conditions ?? (current.ConditionSignature is null ? null : new()),
            ObservedConditionSignature = coverage.ObservedConditionSignature,
            RouteNeedsRediscovery = coverage.RouteNeedsRediscovery,
            UnavailableAttempts = coverage.UnavailableAttempts,
            CompletedAvailabilityFingerprints = coverage.CompletedAvailabilityFingerprints
        };
        AutoPlayEventAvailability availability = ReadWorldSemanticAvailability(projected, current, AutoPlayPlanner<Ff7AutoPlayDeferral>.ReadAvailability(projected, facts));
        projected.AvailabilityFingerprint = availability.Fingerprint;
        return !availability.Unavailable && AutoPlayPlanner<Ff7AutoPlayDeferral>.NeedsExecution(projected);
    }

    internal static WorldModeCandidate? SelectWorldModePrerequisite(Ff7AutoPlayActiveEvent? active, IReadOnlyDictionary<string, WorldModeCandidate> prerequisites, int actionableCount, WorldModeCandidate? queued)
    {
        if (active is { Accepted: true })
        {
            return null;
        }
        if (active is { Kind: Ff7AutoPlayTargetKind.WorldMesh } && prerequisites.TryGetValue(active.EventId, out WorldModeCandidate? selected))
        {
            return selected;
        }
        return actionableCount == 0 ? queued : null;
    }

    private Ff7AutoPlayWorldModeGuide? ReadWorldModeGuide(Ff7WorldRuntime world, WorldModeCandidate candidate)
    {
        string context = System.Text.Json.JsonSerializer.Serialize(new { World = world.Process, Vm = world.Machine.Capture(), State.NavigationGoal, State.DestinationField, Target = WorldCondition(candidate.Target) });
        if (candidate.SearchContext != context)
        {
            candidate.Search?.Dispose();
            candidate.Search = null;
            candidate.Guide = null;
            candidate.SearchContext = context;
        }
        if (candidate.Guide is not null)
        {
            return candidate.Guide;
        }
        candidate.Search ??= world.CreateAutoPlaySurfaceVehicleGuide(candidate.Target, (source, predecessor) =>
        {
            Ff7AutoPlayWorldLandingPreview preview = world.PreviewAutoPlaySurfaceLandingAtFace(source, predecessor);
            if (!preview.Supported)
            {
                throw new NotSupportedException(preview.Reason ?? "The native landing preview is unsupported.");
            }
            return preview.Ground;
        });
        long before = candidate.Search.Operations;
        candidate.Search.Resume(nativePlanningQuantum ?? new());
        nativePlanningWork += candidate.Search.Operations - before;
        return candidate.Search.Result;
    }

    private Ff7AutoplayInput AdvanceWorldModeApproach(Ff7AutoplayDriver driver, Ff7WorldRuntime world, Ff7AutoPlayObservation observation, Ff7AutoPlayActiveEvent active)
    {
        if (!worldModeCatalog.TryGetValue(active.EventId, out WorldModeCandidate? candidate))
        {
            throw new InvalidDataException("The selected native mode route has no observed target.");
        }
        Ff7AutoPlayWorldModeGuide? guide = candidate.Guide;
        if (guide is null || !guide.TransportGuide.Faces.Any(face => face.Source == world.Process.Input?.GroundTriangle))
        {
            guide = ReadWorldModeGuide(world, candidate);
            candidate.Guide = guide;
        }
        if (guide is null)
        {
            driver.DeferPlanning();
            State.Status = "Planning native vehicle/rider topology";
            return default;
        }
        if (guide.TransportGuide.Faces.Length == 0)
        {
            throw new NotSupportedException(guide.Reason ?? "The selected native mode route has no vehicle approach.");
        }
        Ff7AutoPlayWorldLandingPreview landing = world.PreviewAutoPlaySurfaceLanding();
        if (landing.Rider?.Model == candidate.Goal.RiderModel && landing.Ground is not null && guide.RiderReachableFaces.Contains(landing.Ground))
        {
            Ff7AutoPlayWorldControlObservation control = world.ReadAutoPlayControlFrontiers().Single(item => item.RiderModel == candidate.Goal.RiderModel);
            State.Active = active with { WorldControl = control };
            return AdvanceWorldControl(world, observation, State.Active);
        }
        Ff7AutoPlayWorldNavigationResult result = driver.ChooseWorldVehicleApproachInput(candidate.Target, guide);
        if (TryWaitForWorldPhysicalSearch(State, driver, result, "Planning a native landing connected to " + candidate.Goal.TargetEventId, Publish))
        {
            return default;
        }
        if (!result.HasInput)
        {
            throw new NotSupportedException(result.Reason ?? "The native vehicle approach did not establish an input.");
        }
        CommitFrontierInput(State, Story(), controllable: true, driver.PlanningPending, State.PendingRestore is not null);
        State.Status = "Approaching a native landing connected to " + candidate.Goal.TargetEventId;
        Publish();
        return result.Input;
    }
}
