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
        return !visit.Events.Any(item => item.Available && item.BlockedReason is null && !item.PolicyDeferred && !HasConsumedWorldEntry(item, item.Event.ConditionSignature) && readAvailability(item.Event)?.Unavailable != true);
    }

    internal static IEnumerable<AutoPlayPlanStep?> EnumerateWorldBoardingPrerequisites(string visitKey, string navigationKey, IReadOnlyList<AutoPlayEvent> destinations, IReadOnlyList<AutoPlayEvent> boarding, Func<AutoPlayEvent, AutoPlayEvent, bool> connected)
    {
        foreach (AutoPlayEvent destination in destinations)
        {
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

    private AutoPlayPlanStep? PlanWorldBoardingPrerequisite(Ff7WorldRuntime world, Ff7AutoPlayObservation observation, Ff7AutoplayDriver driver, Func<AutoPlayEvent, AutoPlayEventAvailability?> readAvailability)
    {
        if (world.ReadAutoPlaySurfaceVehicleRider() is not null || !ShouldPlanWorldBoardingPrerequisite(State, observation.VisitId, readAvailability))
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
        Dictionary<string, Ff7AutoPlayWorldModelScriptObservation> contacts = worldModelCatalog.Values.Where(contact => contact.Complete && contact.Analysis.Transitions.Any(transition => transition.Operation == Memoria.FF7.Events.WorldOperation.BoardTransport && transition.Model is not null) && coverage.TryGetValue(contact.Id, out var item) && Eligible(item) && readAvailability(item.Event)?.Unavailable != true).ToDictionary(contact => contact.Id, StringComparer.Ordinal);
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
            worldBoardingPlanning = new(EnumerateWorldBoardingPrerequisites(observation.VisitId, observation.NavigationKey, destinations, boarding, (contact, target) =>
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
                // A nonpositive projection is advice only; it publishes no native unavailability or completion.
                return world.ReadAutoPlayBoardingAdvice(contacts[contact.Id], current).Connected;
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

    private sealed class WorldModeCandidate(Ff7AutoPlayWorldScriptObservation target, Ff7AutoPlayWorldModeGoal goal)
    {
        public Ff7AutoPlayWorldScriptObservation Target { get; } = target;
        public Ff7AutoPlayWorldModeGoal Goal { get; } = goal;
        public Ff7AutoPlayWorldModeGuide? Guide { get; set; }
        public string? RefinementSource { get; set; }
        public HashSet<(Ff7WorldTerrainReference From, Ff7WorldTerrainReference To)> UnproductiveEdges { get; } = [];
    }

    private readonly Dictionary<string, WorldModeCandidate> worldModeCatalog = new(StringComparer.Ordinal);
    private readonly Queue<WorldModeCandidate> worldModeQueue = new();
    private Ff7WorldRuntime? worldModeOwner;
    private int worldModeModel = -1;
    private bool worldModeCandidatesObserved;

    private void ObserveWorldModeGoals(Ff7WorldRuntime world, string visitKey, Dictionary<string, AutoPlayEvent> events)
    {
        foreach (string id in events.Keys.Where(id => id.StartsWith("mode:", StringComparison.Ordinal)).ToArray())
        {
            events.Remove(id);
        }
        if (!ReferenceEquals(worldModeOwner, world) || worldModeModel != world.ControlledModel)
        {
            worldModeCatalog.Clear();
            worldModeQueue.Clear();
            worldModeCandidatesObserved = false;
            worldModeOwner = world;
            worldModeModel = world.ControlledModel;
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
            worldModeCatalog.Clear();
            worldModeQueue.Clear();
            worldModeCandidatesObserved = false;
            return;
        }
        if (!worldModeCandidatesObserved)
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
                if (!currentEntry.Complete || !currentEntry.IsInert || !riderEntry.Complete || !riderEntry.MayEnterField)
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
            foreach (var item in candidates.OrderBy(item => item.Candidate.Target.Id == State.NavigationGoal?.EventId ? 0 : 1).ThenBy(item => item.Visited).ThenBy(item => item.Distance).ThenBy(item => item.Candidate.Target.Id, StringComparer.Ordinal))
            {
                worldModeQueue.Enqueue(item.Candidate);
            }
            worldModeCandidatesObserved = true;
        }
        if (worldModeCatalog.Count == 0 && worldModeQueue.TryDequeue(out WorldModeCandidate? candidate))
        {
            candidate.Guide = ReadWorldModeGuide(world, candidate);
            if (candidate.Guide.Limited && candidate.Guide.TransportGuide.Faces.Length == 0)
            {
                throw new NotSupportedException(candidate.Guide.Reason ?? "The native vehicle/rider route requires a larger bounded geometry contract.");
            }
            if (candidate.Guide.TransportGuide.Faces.Length > 0)
            {
                worldModeCatalog.Add("mode:surface-rider:" + candidate.Target.Id, candidate);
            }
        }
        foreach (var entry in worldModeCatalog)
        {
            events[entry.Key] = new(entry.Key, AutoPlayEventKind.Interaction, ConditionSignature: WorldCondition(entry.Value.Target) + ":rider:" + entry.Value.Goal.RiderModel);
        }
    }

    private static Ff7AutoPlayWorldModeGuide ReadWorldModeGuide(Ff7WorldRuntime world, WorldModeCandidate candidate)
    {
        return world.ReadAutoPlaySurfaceVehicleGuide(candidate.Target, (source, predecessor) => world.PreviewAutoPlaySurfaceLandingAtFace(source, predecessor).Ground, unproductiveEdges: candidate.UnproductiveEdges);
    }

    private Ff7AutoplayInput AdvanceWorldModeApproach(Ff7AutoplayDriver driver, Ff7WorldRuntime world, Ff7AutoPlayObservation observation, Ff7AutoPlayActiveEvent active)
    {
        if (!worldModeCatalog.TryGetValue(active.EventId, out WorldModeCandidate? candidate))
        {
            throw new InvalidDataException("The selected native mode route has no observed target.");
        }
        TrackFrontierProgress(State, Story());
        string refinementSource = Ff7AutoplayDriver.WorldPositionKey(world.CaptureAutoPlayMovementState()) + ":" + world.ReadAutoPlayNavigationContext();
        if (candidate.RefinementSource != refinementSource)
        {
            candidate.UnproductiveEdges.Clear();
            candidate.RefinementSource = refinementSource;
        }
        Ff7AutoPlayWorldModeGuide? guide = candidate.Guide;
        if (guide is null || !guide.TransportGuide.Faces.Any(face => face.Source == world.Process.Input?.GroundTriangle))
        {
            guide = ReadWorldModeGuide(world, candidate);
            candidate.Guide = guide;
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
        if (!result.HasInput)
        {
            if (result.Status == Ff7AutoPlayWorldNavigationStatus.SearchLimited)
            {
                int index = Array.FindIndex(guide.TransportGuide.Faces, face => face.Source == world.Process.Input?.GroundTriangle);
                if (index < 0 || index + 1 >= guide.TransportGuide.Faces.Length || !candidate.UnproductiveEdges.Add((guide.TransportGuide.Faces[index].Source, guide.TransportGuide.Faces[index + 1].Source)))
                {
                    throw new NotSupportedException(result.Reason ?? "The bounded native landing search has no alternate advisory edge.");
                }
                candidate.Guide = ReadWorldModeGuide(world, candidate);
                if (candidate.Guide.TransportGuide.Faces.Length == 0)
                {
                    throw new NotSupportedException(candidate.Guide.Reason ?? "No alternate native transport advice remains for this physical source.");
                }
                State.Status = "Refining the native transport guide after a footprint rejection";
                Publish();
                return default;
            }
            throw new NotSupportedException(result.Reason ?? "The native vehicle approach did not establish an input.");
        }
        State.Status = "Approaching a native landing connected to " + candidate.Goal.TargetEventId;
        Publish();
        return result.Input;
    }
}
