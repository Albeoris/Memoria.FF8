using System.Numerics;
using System.Text;

namespace Memoria.FF7.Runtime;

internal enum Ff7AutoPlayWorldNavigationStatus
{
    Step,
    Rearming,
    SearchLimited,
    Blocked,
    Unsupported
}

internal sealed record Ff7AutoPlayWorldNavigationResult(Ff7AutoPlayWorldNavigationStatus Status, Ff7AutoplayInput Input, int Expanded, string? Reason = null, Ff7AutoPlayWorldTerrainBoundary? PredictedBoundary = null, bool HasInput = false, Ff7WorldTerrainReference[]? DeferredComponent = null);
internal sealed record Ff7AutoPlayWorldRouteStep(string SourceKey, Ff7AutoplayInput Input, string DestinationKey, Ff7AutoPlayWorldTerrainBoundary? Boundary, Ff7AutoPlayWorldModelBoundary[]? ModelBoundaries = null);
internal sealed record Ff7AutoPlayWorldRoute(Ff7AutoPlayWorldNavigationStatus Status, Ff7AutoPlayWorldRouteStep[] Steps, int Expanded, string? Reason);

public sealed partial class Ff7AutoplayDriver
{
    internal const int WorldModelContactAdmissionRevision = 1;
    private const int WorldWidth = 0x48000;
    private const int WorldDepth = 0x38000;
    private readonly Queue<Ff7AutoPlayWorldRouteStep> worldRoute = new();
    private readonly Dictionary<string, HashSet<Ff7AutoplayInput>> worldRepeatedInputs = [];
    private readonly HashSet<(Ff7WorldTerrainReference From, Ff7WorldTerrainReference To)> worldUnproductiveGuideEdges = [];
    private string? worldRouteTarget;
    private string? worldRepeatedTarget;
    private bool worldRouteRearming;
    private Ff7AutoPlayWorldGuide? worldTerrainGuide;
    private Ff7WorldRuntime? worldTerrainGuideOwner;
    private string? worldTerrainGuideContext;
    private string? worldGuideTrialContext;
    private string? worldGuideTrialSource;
    internal bool PlanningPending { get; private set; }

    internal void BeginPlanningStep() => PlanningPending = false;

    internal Ff7AutoPlayWorldNavigationResult ChooseWorldInput(Ff7AutoPlayWorldScriptObservation target, int nodeBudget = 512)
    {
        int expanded = 0;
        int remaining = nodeBudget;
        PlanningPending = false;
        while (true)
        {
            Ff7AutoPlayWorldNavigationResult result = ChooseWorldInputAttempt(target, remaining, out bool retry, out int physicalExpanded);
            expanded += result.Expanded;
            remaining -= physicalExpanded;
            if (!retry)
            {
                return result with { Expanded = expanded };
            }
            if (remaining == 0)
            {
                PlanningPending = true;
                return result with { Expanded = expanded };
            }
        }
    }

    private Ff7AutoPlayWorldNavigationResult ChooseWorldInputAttempt(Ff7AutoPlayWorldScriptObservation target, int nodeBudget, out bool retry, out int physicalExpanded)
    {
        retry = false;
        physicalExpanded = 0;
        Ff7WorldRuntime world = session.World ?? throw new InvalidOperationException("World navigation requires an allocated native world.");
        if (!target.Complete)
        {
            return new(Ff7AutoPlayWorldNavigationStatus.Unsupported, default, 0, target.Reason ?? "The requested native world script remains incompletely analyzed.");
        }
        if (world.ControlledModel < 0)
        {
            return new(Ff7AutoPlayWorldNavigationStatus.Unsupported, default, 0, "The native controlled world model is not allocated.");
        }
        if (target.Script is < 3 or > 7 || target.Faces.Length == 0)
        {
            return new(Ff7AutoPlayWorldNavigationStatus.Unsupported, default, 0, "A world navigation goal requires its native terrain script and source faces.");
        }
        Ff7AutoPlayWorldMovementState source = world.CaptureAutoPlayMovementState();
        string sourceKey = WorldMovementKey(source);
        string positionKey = WorldPositionKey(source);
        string identity = $"{source.WorldId}:{source.WorldMapState}:{source.Player.Model}:{target.Id}:{target.Script}:{target.GeometryFingerprint}:{target.PredicateFingerprint}";
        if (worldRepeatedTarget != identity)
        {
            worldRepeatedInputs.Clear();
            worldUnproductiveGuideEdges.Clear();
            worldRepeatedTarget = identity;
        }
        bool rearming = RequiresWorldTargetRearming(source, target);
        Vector3? guidePoint = null;
        (Ff7WorldTerrainReference From, Ff7WorldTerrainReference To)? guideEdge = null;
        if (!rearming)
        {
            string context = $"{identity}:{world.ReadAutoPlayNavigationContext()}";
            if (worldGuideTrialContext != context || worldGuideTrialSource != sourceKey)
            {
                worldUnproductiveGuideEdges.Clear();
                worldGuideTrialContext = context;
                worldGuideTrialSource = sourceKey;
            }
            int guideIndex = worldTerrainGuide is null ? -1 : Array.FindIndex(worldTerrainGuide.Faces, face => face.Source == source.Input.GroundTriangle);
            if (!ReferenceEquals(worldTerrainGuideOwner, world) || worldTerrainGuideContext != context || guideIndex < 0)
            {
                worldTerrainGuide = world.ReadAutoPlayNavigationGuide(target, unproductiveEdges: worldUnproductiveGuideEdges);
                worldTerrainGuideOwner = world;
                worldTerrainGuideContext = context;
                guideIndex = 0;
            }
            Ff7AutoPlayWorldGuide guide = worldTerrainGuide ?? throw new InvalidOperationException("Native world route guidance was not initialized.");
            if (guide.Faces.Length == 0)
            {
                worldRoute.Clear();
                Ff7AutoPlayWorldNavigationStatus status = guide.Limited ? Ff7AutoPlayWorldNavigationStatus.SearchLimited : guide.DeferredComponent is { Length: > 0 } ? Ff7AutoPlayWorldNavigationStatus.Blocked : Ff7AutoPlayWorldNavigationStatus.Unsupported;
                return new(status, default, guide.Expanded, guide.Reason, DeferredComponent: guide.DeferredComponent);
            }
            if (guideIndex + 1 < guide.Faces.Length)
            {
                guidePoint = guide.Faces[guideIndex + 1].Center;
                guideEdge = (guide.Faces[guideIndex].Source, guide.Faces[guideIndex + 1].Source);
                identity += $":guide:{guide.Faces[guideIndex + 1].Source}";
            }
        }
        if (worldRouteTarget != identity || worldRouteRearming != rearming)
        {
            worldRoute.Clear();
            worldRouteTarget = identity;
            worldRouteRearming = rearming;
        }
        if (worldRoute.TryPeek(out Ff7AutoPlayWorldRouteStep? retained))
        {
            Ff7AutoPlayWorldMovementPreview fresh = world.PreviewAutoPlayInput(retained.Input, source);
            if (retained.SourceKey == sourceKey && fresh is { Supported: true, State: not null } && WorldMovementKey(fresh.State) == retained.DestinationKey && fresh.TerrainBoundary == retained.Boundary && (fresh.ModelBoundaries ?? []).SequenceEqual(retained.ModelBoundaries ?? []))
            {
                worldRoute.Dequeue();
                RememberWorldInput(positionKey, retained.Input);
                return new(rearming ? Ff7AutoPlayWorldNavigationStatus.Rearming : Ff7AutoPlayWorldNavigationStatus.Step, retained.Input, 0, PredictedBoundary: fresh.TerrainBoundary, HasInput: true);
            }
            worldRoute.Clear();
        }
        Vector3[] departureHints = rearming ? ReadWorldDepartureHints(world, source, target.Script) : [];
        worldRepeatedInputs.TryGetValue(positionKey, out HashSet<Ff7AutoplayInput>? repeated);
        Ff7AutoPlayWorldRoute route = SearchWorldRoute(source, target, (input, state) => world.PreviewAutoPlayInput(input, state), departureHints, nodeBudget, repeated, guidePoint);
        physicalExpanded = route.Expanded;
        if (route.Steps.Length == 0 && repeated is { Count: > 0 })
        {
            repeated.Clear();
            if (physicalExpanded == nodeBudget)
            {
                retry = true;
                return new(Ff7AutoPlayWorldNavigationStatus.SearchLimited, default, physicalExpanded, "Native input preferences were renewed; the bounded physical search remains pending.");
            }
            route = SearchWorldRoute(source, target, (input, state) => world.PreviewAutoPlayInput(input, state), departureHints, nodeBudget - physicalExpanded, waypoint: guidePoint);
            physicalExpanded += route.Expanded;
        }
        if (route.Status == Ff7AutoPlayWorldNavigationStatus.SearchLimited && route.Steps.Length == 0 && guideEdge is { } edge)
        {
            if (!worldUnproductiveGuideEdges.Add(edge))
            {
                throw new InvalidOperationException("A world guide repeated an unresolved physical transition.");
            }
            worldTerrainGuideContext = null;
            retry = true;
            return new(Ff7AutoPlayWorldNavigationStatus.SearchLimited, default, physicalExpanded, route.Reason);
        }
        foreach (Ff7AutoPlayWorldRouteStep step in route.Steps)
        {
            worldRoute.Enqueue(step);
        }
        if (!worldRoute.TryDequeue(out Ff7AutoPlayWorldRouteStep? first))
        {
            return new(route.Status, default, physicalExpanded, route.Reason);
        }
        RememberWorldInput(positionKey, first.Input);
        return new(route.Status, first.Input, physicalExpanded, route.Reason, first.Boundary, HasInput: true);
    }

    private void RememberWorldInput(string source, Ff7AutoplayInput input)
    {
        if (!worldRepeatedInputs.TryGetValue(source, out HashSet<Ff7AutoplayInput>? inputs))
        {
            if (worldRepeatedInputs.Count == 64)
            {
                worldRepeatedInputs.Clear();
            }
            worldRepeatedInputs[source] = inputs = [];
        }
        inputs.Add(input);
    }

    private sealed record WorldSearchNode(Ff7AutoPlayWorldMovementState State, string Key, string SearchKey, int Parent, Ff7AutoplayInput Input, Ff7AutoPlayWorldTerrainBoundary? Boundary, Ff7AutoPlayWorldModelBoundary[]? ModelBoundaries, double Cost, double Distance);

    private static bool RequiresWorldTargetRearming(Ff7AutoPlayWorldMovementState source, Ff7AutoPlayWorldScriptObservation target)
    {
        return source.Input.TerrainScript == target.Script && target.Faces.Any(face => face.Source == source.Input.GroundTriangle);
    }

    internal static Ff7AutoPlayWorldRoute SearchWorldRoute(Ff7AutoPlayWorldMovementState source, Ff7AutoPlayWorldScriptObservation target, Func<Ff7AutoplayInput, Ff7AutoPlayWorldMovementState, Ff7AutoPlayWorldMovementPreview> preview, IReadOnlyList<Vector3> departureHints, int nodeBudget, IReadOnlySet<Ff7AutoplayInput>? avoidFirst = null, Vector3? waypoint = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(nodeBudget, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(nodeBudget, 16384);
        bool rearming = RequiresWorldTargetRearming(source, target);
        double Distance(Ff7AutoPlayWorldMovementState state)
        {
            return rearming ? departureHints.Select(point => WorldPointDistance(state.Player, point)).DefaultIfEmpty(0).Min() : waypoint is Vector3 point ? WorldPointDistance(state.Player, point) : target.Faces.Select(face => WorldFaceDistance(state.Player, face)).Min();
        }
        bool TargetBoundary(Ff7AutoPlayWorldMovementPreview step)
        {
            return step.TerrainBoundary is { } boundary && boundary.Script == target.Script - 3 && target.Faces.Any(face => face.Source.ChunkX == boundary.ChunkX && face.Source.ChunkZ == boundary.ChunkZ && face.Source == step.State!.Input.GroundTriangle);
        }
        bool Admit(Ff7AutoPlayWorldMovementPreview step)
        {
            return AdmitsUnselectedWorldModels(step.ModelBoundaries) && (step.TerrainBoundary is not { Inert: false } || !rearming && TargetBoundary(step));
        }
        bool Reached(Ff7AutoPlayWorldMovementPreview step)
        {
            return rearming ? step.State!.Input.TerrainScript != target.Script && (step.TerrainBoundary is null || step.TerrainBoundary.Inert) : TargetBoundary(step) || RequiresWorldTargetRearming(step.State!, target);
        }
        return SearchWorldPhysicalRoute(source, preview, Distance, Admit, Reached, nodeBudget, avoidFirst, rearming ? Ff7AutoPlayWorldNavigationStatus.Rearming : Ff7AutoPlayWorldNavigationStatus.Step);
    }

    private static Ff7AutoPlayWorldRoute SearchWorldPhysicalRoute(Ff7AutoPlayWorldMovementState source, Func<Ff7AutoplayInput, Ff7AutoPlayWorldMovementState, Ff7AutoPlayWorldMovementPreview> preview, Func<Ff7AutoPlayWorldMovementState, double> distanceToTarget, Func<Ff7AutoPlayWorldMovementPreview, bool> admit, Func<Ff7AutoPlayWorldMovementPreview, bool> reachedTarget, int nodeBudget, IReadOnlySet<Ff7AutoplayInput>? avoidFirst = null, Ff7AutoPlayWorldNavigationStatus success = Ff7AutoPlayWorldNavigationStatus.Step, ushort held = 0)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(nodeBudget, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(nodeBudget, 16384);
        string initial = WorldMovementKey(source);
        string initialSearch = WorldSearchKey(source);
        List<WorldSearchNode> nodes = [new(source, initial, initialSearch, -1, default, null, null, 0, distanceToTarget(source))];
        Dictionary<string, double> costs = new(StringComparer.Ordinal) { [initialSearch] = 0 };
        PriorityQueue<int, (double Score, double Distance, int Order)> open = new();
        open.Enqueue(0, (nodes[0].Distance, nodes[0].Distance, 0));
        int expanded = 0;
        int best = -1;
        string? unsupported = null;
        while (open.TryDequeue(out int current, out _) && expanded < nodeBudget)
        {
            WorldSearchNode node = nodes[current];
            if (costs[node.SearchKey] != node.Cost)
            {
                continue;
            }
            expanded++;
            foreach (Ff7AutoplayInput input in WorldNavigationInputs(held))
            {
                if (current == 0 && avoidFirst?.Contains(input) == true)
                {
                    continue;
                }
                Ff7AutoPlayWorldMovementPreview step = preview(input, node.State);
                if (!step.Supported || step.State is null)
                {
                    unsupported ??= step.Reason;
                    continue;
                }
                if (!admit(step))
                {
                    continue;
                }
                string key = WorldMovementKey(step.State);
                string searchKey = WorldSearchKey(step.State);
                double cost = node.Cost + Math.Max(1, WorldPointDistance(node.State.Player, new(step.State.Player.X, step.State.Player.Y, step.State.Player.Z)));
                bool reached = reachedTarget(step);
                if (!reached && costs.TryGetValue(searchKey, out double prior) && prior <= cost)
                {
                    continue;
                }
                double distance = distanceToTarget(step.State);
                int index = nodes.Count;
                nodes.Add(new(step.State, key, searchKey, current, input, step.TerrainBoundary, step.ModelBoundaries, cost, distance));
                if (reached)
                {
                    return new(success, ReconstructWorldRoute(nodes, index), expanded, null);
                }
                costs[searchKey] = cost;
                open.Enqueue(index, (cost + distance, distance, index));
                if (step.Moved && distance < nodes[0].Distance && (best < 0 || distance < nodes[best].Distance || distance == nodes[best].Distance && cost < nodes[best].Cost))
                {
                    best = index;
                }
            }
        }
        if (expanded >= nodeBudget && best >= 0)
        {
            return new(Ff7AutoPlayWorldNavigationStatus.SearchLimited, ReconstructWorldRoute(nodes, best), expanded, "The bounded native search admitted a partial route; the goal is not yet proved reachable.");
        }
        if (expanded >= nodeBudget)
        {
            return new(Ff7AutoPlayWorldNavigationStatus.SearchLimited, [], expanded, "The native search budget ended before an advancing route was proved.");
        }
        return new(unsupported is null ? Ff7AutoPlayWorldNavigationStatus.Blocked : Ff7AutoPlayWorldNavigationStatus.Unsupported, [], expanded, unsupported ?? "No distinct admissible native step remains in the explored physical state graph.");
    }

    private static Ff7AutoPlayWorldRouteStep[] ReconstructWorldRoute(IReadOnlyList<WorldSearchNode> nodes, int index)
    {
        List<Ff7AutoPlayWorldRouteStep> result = [];
        while (nodes[index].Parent >= 0)
        {
            WorldSearchNode node = nodes[index];
            result.Add(new(nodes[node.Parent].Key, node.Input, node.Key, node.Boundary, node.ModelBoundaries));
            index = node.Parent;
        }
        result.Reverse();
        return result.ToArray();
    }

    private static IEnumerable<Ff7AutoplayInput> WorldNavigationInputs(ushort held = 0)
    {
        foreach (var direction in Directions)
        {
            yield return new(direction.X, direction.Y, Held: held, Run: true);
        }
        yield return new(Held: held, Run: false);
    }

    private static Vector3[] ReadWorldDepartureHints(Ff7WorldRuntime world, Ff7AutoPlayWorldMovementState state, int script)
    {
        List<Vector3> result = [];
        int chunkX = state.Player.X >> 13;
        int chunkZ = state.Player.Z >> 13;
        for (int z = -1; z <= 1; z++)
        {
            for (int x = -1; x <= 1; x++)
            {
                int columns = WorldWidth >> 13;
                int rows = WorldDepth >> 13;
                int cx = (chunkX + x + columns) % columns;
                int cz = (chunkZ + z + rows) % rows;
                var mesh = world.ReadAutoPlayCollisionMesh(cx, cz);
                foreach (var face in mesh.Triangles.Where(face => face.Script < 3 && face.Script != script))
                {
                    result.Add((mesh.Vertices[face.A] + mesh.Vertices[face.B] + mesh.Vertices[face.C]) / 3 + new Vector3(cx << 13, 0, cz << 13));
                }
            }
        }
        return result.ToArray();
    }

    private static double WorldDelta(double difference, int extent)
    {
        difference %= extent;
        return difference > extent / 2d ? difference - extent : difference < -extent / 2d ? difference + extent : difference;
    }

    private static double WorldPointDistance(Ff7WorldEntity player, Vector3 point)
    {
        double dx = WorldDelta(point.X - player.X, WorldWidth);
        double dz = WorldDelta(point.Z - player.Z, WorldDepth);
        return Math.Sqrt(dx * dx + dz * dz);
    }

    internal static double WorldFaceDistance(Ff7WorldEntity player, Ff7AutoPlayWorldFrontierFace face)
    {
        Vector2 a = new((float)WorldDelta(face.A.X - player.X, WorldWidth), (float)WorldDelta(face.A.Z - player.Z, WorldDepth));
        Vector2 b = a + new Vector2((float)WorldDelta(face.B.X - face.A.X, WorldWidth), (float)WorldDelta(face.B.Z - face.A.Z, WorldDepth));
        Vector2 c = a + new Vector2((float)WorldDelta(face.C.X - face.A.X, WorldWidth), (float)WorldDelta(face.C.Z - face.A.Z, WorldDepth));
        static float Cross(Vector2 u, Vector2 v) => u.X * v.Y - u.Y * v.X;
        float ab = Cross(b - a, -a);
        float bc = Cross(c - b, -b);
        float ca = Cross(a - c, -c);
        if (ab >= 0 && bc >= 0 && ca >= 0 || ab <= 0 && bc <= 0 && ca <= 0)
        {
            return 0;
        }
        static double Edge(Vector2 start, Vector2 end)
        {
            Vector2 delta = end - start;
            float ratio = delta.LengthSquared() == 0 ? 0 : Math.Clamp(Vector2.Dot(-start, delta) / delta.LengthSquared(), 0, 1);
            return (start + delta * ratio).Length();
        }
        return Math.Min(Edge(a, b), Math.Min(Edge(b, c), Edge(c, a)));
    }

    internal static bool AdmitsUnselectedWorldModels(Ff7AutoPlayWorldModelBoundary[]? boundaries)
    {
        return boundaries is null || boundaries.All(boundary => boundary.Inert);
    }

    internal static string WorldMovementKey(Ff7AutoPlayWorldMovementState state)
    {
        return WorldMovementKey(state, includePresentationFacing: true);
    }

    internal static string WorldSearchKey(Ff7AutoPlayWorldMovementState state)
    {
        // Admitted navigation inputs exclude transport dismounts. Current facing and visual turn smoothing do not feed their motion.
        return WorldMovementKey(state, includePresentationFacing: false);
    }

    private static string WorldMovementKey(Ff7AutoPlayWorldMovementState state, bool includePresentationFacing)
    {
        Ff7WorldEntity p = state.Player;
        Ff7WorldInputState i = state.Input;
        StringBuilder key = new($"model-contact-admission:{WorldModelContactAdmissionRevision}:{state.WorldId}:{state.WorldMapState}:{p.Model}:{p.X}:{p.Y}:{p.Z}:{p.Terrain}:{p.Flags}:{p.ContactModel}:{(includePresentationFacing ? p.Direction : 0)}:{p.TargetDirection}:{(includePresentationFacing ? p.TurnOffset : 0)}:{state.ViewMode}:{i.CameraHeadingTarget ?? state.Camera.Heading}:{i.FreeDirectionOffset}:{i.PreviousControlHeld}:{i.CollisionSide}:{Math.Min(i.CollisionAge, 8)}:{i.CollisionRecovery}:{i.BridgeCooldown}:{i.HeightSmoothVelocity}:{i.HeightSmoothRemaining}:{i.TerrainScript}:{i.GroundTriangle}:{i.Bridge}");
        foreach (Ff7WorldTerrainReference cached in i.TerrainCache ?? [])
        {
            key.Append($"|{cached.ChunkX},{cached.ChunkZ},{cached.Triangle}");
        }
        // Supported foot-control RNG draws have no physical return value. VM execution is not forecast.
        return key.ToString();
    }

    internal static string WorldPositionKey(Ff7AutoPlayWorldMovementState state)
    {
        // Repeated physical edges are exploration preferences, not equivalent native execution states.
        return $"{state.WorldId}:{state.WorldMapState}:{state.Player.Model}:{state.Player.X}:{state.Player.Y}:{state.Player.Z}";
    }
}
