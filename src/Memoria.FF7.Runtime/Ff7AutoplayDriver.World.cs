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

internal sealed record Ff7AutoPlayWorldNavigationResult(Ff7AutoPlayWorldNavigationStatus Status, Ff7AutoplayInput Input, int Expanded, string? Reason = null, Ff7AutoPlayWorldTerrainBoundary? PredictedBoundary = null, bool HasInput = false);
internal sealed record Ff7AutoPlayWorldRouteStep(string SourceKey, Ff7AutoplayInput Input, string DestinationKey, Ff7AutoPlayWorldTerrainBoundary? Boundary);
internal sealed record Ff7AutoPlayWorldRoute(Ff7AutoPlayWorldNavigationStatus Status, Ff7AutoPlayWorldRouteStep[] Steps, int Expanded, string? Reason);

public sealed partial class Ff7AutoplayDriver
{
    private const int WorldWidth = 0x48000;
    private const int WorldDepth = 0x38000;
    private readonly Queue<Ff7AutoPlayWorldRouteStep> worldRoute = new();
    private readonly Dictionary<string, HashSet<Ff7AutoplayInput>> worldRepeatedInputs = [];
    private string? worldRouteTarget;
    private bool worldRouteRearming;

    internal Ff7AutoPlayWorldNavigationResult ChooseWorldInput(Ff7AutoPlayWorldScriptObservation target, int nodeBudget = 512)
    {
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
        string identity = $"{source.WorldId}:{source.WorldMapState}:{source.Player.Model}:{target.Id}:{target.Script}:{target.GeometryFingerprint}:{target.PredicateFingerprint}";
        bool rearming = source.Input.TerrainScript == target.Script;
        if (worldRouteTarget != identity || worldRouteRearming != rearming)
        {
            worldRoute.Clear();
            worldRepeatedInputs.Clear();
            worldRouteTarget = identity;
            worldRouteRearming = rearming;
        }
        if (worldRoute.TryPeek(out Ff7AutoPlayWorldRouteStep? retained))
        {
            Ff7AutoPlayWorldMovementPreview fresh = world.PreviewAutoPlayInput(retained.Input, source);
            if (retained.SourceKey == sourceKey && fresh is { Supported: true, State: not null } && WorldMovementKey(fresh.State) == retained.DestinationKey && fresh.TerrainBoundary == retained.Boundary)
            {
                worldRoute.Dequeue();
                RememberWorldInput(sourceKey, retained.Input);
                return new(rearming ? Ff7AutoPlayWorldNavigationStatus.Rearming : Ff7AutoPlayWorldNavigationStatus.Step, retained.Input, 0, PredictedBoundary: fresh.TerrainBoundary, HasInput: true);
            }
            worldRoute.Clear();
        }
        Vector3[] departureHints = rearming ? ReadWorldDepartureHints(world, source, target.Script) : [];
        worldRepeatedInputs.TryGetValue(sourceKey, out HashSet<Ff7AutoplayInput>? repeated);
        Ff7AutoPlayWorldRoute route = SearchWorldRoute(source, target, (input, state) => world.PreviewAutoPlayInput(input, state), departureHints, nodeBudget, repeated);
        if (route.Steps.Length == 0 && repeated is { Count: > 0 })
        {
            repeated.Clear();
            route = SearchWorldRoute(source, target, (input, state) => world.PreviewAutoPlayInput(input, state), departureHints, nodeBudget);
        }
        foreach (Ff7AutoPlayWorldRouteStep step in route.Steps)
        {
            worldRoute.Enqueue(step);
        }
        if (!worldRoute.TryDequeue(out Ff7AutoPlayWorldRouteStep? first))
        {
            return new(route.Status, default, route.Expanded, route.Reason);
        }
        RememberWorldInput(sourceKey, first.Input);
        return new(route.Status, first.Input, route.Expanded, route.Reason, first.Boundary, HasInput: true);
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

    private sealed record WorldSearchNode(Ff7AutoPlayWorldMovementState State, string Key, int Parent, Ff7AutoplayInput Input, Ff7AutoPlayWorldTerrainBoundary? Boundary, double Cost, double Distance);

    internal static Ff7AutoPlayWorldRoute SearchWorldRoute(Ff7AutoPlayWorldMovementState source, Ff7AutoPlayWorldScriptObservation target, Func<Ff7AutoplayInput, Ff7AutoPlayWorldMovementState, Ff7AutoPlayWorldMovementPreview> preview, IReadOnlyList<Vector3> departureHints, int nodeBudget, IReadOnlySet<Ff7AutoplayInput>? avoidFirst = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(nodeBudget, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(nodeBudget, 16384);
        bool rearming = source.Input.TerrainScript == target.Script;
        double Distance(Ff7AutoPlayWorldMovementState state)
        {
            return rearming ? departureHints.Select(point => WorldPointDistance(state.Player, point)).DefaultIfEmpty(0).Min() : target.Faces.Select(face => WorldFaceDistance(state.Player, face)).Min();
        }
        string initial = WorldMovementKey(source);
        List<WorldSearchNode> nodes = [new(source, initial, -1, default, null, 0, Distance(source))];
        Dictionary<string, double> costs = new(StringComparer.Ordinal) { [initial] = 0 };
        PriorityQueue<int, (double Score, double Distance, int Order)> open = new();
        open.Enqueue(0, (nodes[0].Distance, nodes[0].Distance, 0));
        int expanded = 0;
        int best = -1;
        string? unsupported = null;
        while (open.TryDequeue(out int current, out _) && expanded < nodeBudget)
        {
            WorldSearchNode node = nodes[current];
            if (costs[node.Key] != node.Cost)
            {
                continue;
            }
            expanded++;
            foreach (Ff7AutoplayInput input in WorldNavigationInputs())
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
                bool targetBoundary = step.TerrainBoundary is { } boundary && boundary.Script == target.Script - 3 && target.Faces.Any(face => face.Source.ChunkX == boundary.ChunkX && face.Source.ChunkZ == boundary.ChunkZ && face.Source == step.State.Input.GroundTriangle);
                // Forecasts stop at every native mesh dispatch; no child script is simulated.
                if (step.TerrainBoundary is not null && (rearming || !targetBoundary))
                {
                    continue;
                }
                string key = WorldMovementKey(step.State);
                double cost = node.Cost + Math.Max(1, WorldPointDistance(node.State.Player, new(step.State.Player.X, step.State.Player.Y, step.State.Player.Z)));
                bool reached = rearming ? step.State.Input.TerrainScript != target.Script && step.TerrainBoundary is null : targetBoundary;
                if (!reached && costs.TryGetValue(key, out double prior) && prior <= cost)
                {
                    continue;
                }
                double distance = Distance(step.State);
                int index = nodes.Count;
                nodes.Add(new(step.State, key, current, input, step.TerrainBoundary, cost, distance));
                if (reached)
                {
                    return new(rearming ? Ff7AutoPlayWorldNavigationStatus.Rearming : Ff7AutoPlayWorldNavigationStatus.Step, ReconstructWorldRoute(nodes, index), expanded, null);
                }
                costs[key] = cost;
                open.Enqueue(index, (cost + distance, distance, index));
                if (step.Moved && (best < 0 || distance < nodes[best].Distance || distance == nodes[best].Distance && cost < nodes[best].Cost))
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
            result.Add(new(nodes[node.Parent].Key, node.Input, node.Key, node.Boundary));
            index = node.Parent;
        }
        result.Reverse();
        return result.ToArray();
    }

    private static IEnumerable<Ff7AutoplayInput> WorldNavigationInputs()
    {
        foreach (var direction in Directions)
        {
            yield return new(direction.X, direction.Y, Run: true);
        }
        yield return new(Run: false);
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

    internal static string WorldMovementKey(Ff7AutoPlayWorldMovementState state)
    {
        Ff7WorldEntity p = state.Player;
        Ff7WorldInputState i = state.Input;
        StringBuilder key = new($"{state.WorldId}:{state.WorldMapState}:{p.Model}:{p.X}:{p.Y}:{p.Z}:{p.Terrain}:{p.Flags}:{p.Direction}:{p.TargetDirection}:{p.TurnOffset}:{state.ViewMode}:{i.CameraHeadingTarget ?? state.Camera.Heading}:{i.FreeDirectionOffset}:{i.PreviousControlHeld}:{i.CollisionSide}:{Math.Min(i.CollisionAge, 8)}:{i.CollisionRecovery}:{i.BridgeCooldown}:{i.HeightSmoothVelocity}:{i.HeightSmoothRemaining}:{i.TerrainScript}:{i.GroundTriangle}:{i.Bridge}");
        foreach (Ff7WorldTerrainReference cached in i.TerrainCache ?? [])
        {
            key.Append($"|{cached.ChunkX},{cached.ChunkZ},{cached.Triangle}");
        }
        // Supported foot-control RNG draws have no physical return value. VM execution is not forecast.
        return key.ToString();
    }
}
