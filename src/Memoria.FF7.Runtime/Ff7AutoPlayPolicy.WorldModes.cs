using Memoria.AutoPlay;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayPolicy
{
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
            foreach (var item in candidates.OrderBy(item => item.Visited).ThenBy(item => item.Distance).ThenBy(item => item.Candidate.Target.Id, StringComparer.Ordinal))
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
