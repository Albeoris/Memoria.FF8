using System.Numerics;
using System.Text;
using System.Security.Cryptography;
using System.Text.Json;

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
    private string? worldRouteTarget;
    private string? worldRepeatedTarget;
    private bool worldRouteRearming;
    private Ff7AutoPlayWorldGuide? worldTerrainGuide;
    private Ff7WorldRuntime? worldTerrainGuideOwner;
    private string? worldTerrainGuideContext;
    internal bool PlanningPending { get; private set; }

    internal void BeginPlanningStep()
    {
        PlanningPending = false;
        physicalSearchPreparedThisStep = false;
    }

    internal void DeferPlanning()
    {
        physicalSearchPreparedThisStep = false;
        PlanningPending = true;
    }

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
            worldRepeatedTarget = identity;
        }
        bool rearming = RequiresWorldTargetRearming(source, target);
        Vector3? guidePoint = null;
        if (!rearming)
        {
            string context = $"{identity}:{world.ReadAutoPlayNavigationContext()}";

            int guideIndex = worldTerrainGuide is null ? -1 : Array.FindIndex(worldTerrainGuide.Faces, face => face.Source == source.Input.GroundTriangle);
            if (!ReferenceEquals(worldTerrainGuideOwner, world) || worldTerrainGuideContext != context || guideIndex < 0 && worldTerrainGuide is not { Limited: true, Faces.Length: 0 })
            {
                worldTerrainGuide = world.ReadAutoPlayNavigationGuide(target);
                worldTerrainGuideOwner = world;
                worldTerrainGuideContext = context;
                guideIndex = 0;
            }
            Ff7AutoPlayWorldGuide guide = worldTerrainGuide ?? throw new InvalidOperationException("Native world route guidance was not initialized.");
            if (guide.Faces.Length == 0 && !guide.Limited)
            {
                worldRoute.Clear();
                Ff7AutoPlayWorldNavigationStatus status = guide.Limited ? Ff7AutoPlayWorldNavigationStatus.SearchLimited : guide.DeferredComponent is { Length: > 0 } ? Ff7AutoPlayWorldNavigationStatus.Blocked : Ff7AutoPlayWorldNavigationStatus.Unsupported;
                return new(status, default, guide.Expanded, guide.Reason, DeferredComponent: guide.DeferredComponent);
            }
            if (guideIndex + 1 < guide.Faces.Length)
            {
                guidePoint = guide.Faces[guideIndex + 1].Center;
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
        Ff7AutoPlayWorldRoute route = SearchWorldRoute(source, target, (input, state) => world.PreviewAutoPlayInput(input, state), departureHints, nodeBudget, repeated, guidePoint, (create, quantum) => ResumeWorldPhysicalSearch(ReadWorldPhysicalSearchContext(world, source, identity + "|" + guidePoint), create, quantum, world));
        physicalExpanded = route.Expanded;
        if (route.Steps.Length == 0 && route.Status != Ff7AutoPlayWorldNavigationStatus.SearchLimited && repeated is { Count: > 0 })
        {
            repeated.Clear();
            if (physicalExpanded == nodeBudget)
            {
                SearchWorldRoute(source, target, (input, state) => world.PreviewAutoPlayInput(input, state), departureHints, 0, waypoint: guidePoint, resume: (create, quantum) => ResumeWorldPhysicalSearch(ReadWorldPhysicalSearchContext(world, source, identity + "|" + guidePoint), create, quantum, world));
                retry = true;
                return new(Ff7AutoPlayWorldNavigationStatus.SearchLimited, default, physicalExpanded, "Native input preferences were renewed; the bounded physical search remains pending.");
            }
            route = SearchWorldRoute(source, target, (input, state) => world.PreviewAutoPlayInput(input, state), departureHints, nodeBudget - physicalExpanded, waypoint: guidePoint, resume: (create, quantum) => ResumeWorldPhysicalSearch(ReadWorldPhysicalSearchContext(world, source, identity + "|" + guidePoint), create, quantum, world));
            physicalExpanded += route.Expanded;
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

    private readonly record struct WorldPhysicalKey(ulong A, ulong B, ulong C, ulong D)
    {
        internal static WorldPhysicalKey Read(string value) => Read(System.Runtime.InteropServices.MemoryMarshal.AsBytes(value.AsSpan()));

        internal static WorldPhysicalKey Read(ReadOnlySpan<byte> value)
        {
            Span<byte> hash = stackalloc byte[32];
            SHA256.HashData(value, hash);
            return new(System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(hash), System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(hash[8..]), System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(hash[16..]), System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(hash[24..]));
        }
    }

    private sealed record WorldSearchNode(Ff7AutoPlayWorldMovementState? State, WorldPhysicalKey Key, WorldPhysicalKey SearchKey, int Parent, Ff7AutoplayInput Input, Ff7AutoPlayWorldTerrainBoundary? Boundary, Ff7AutoPlayWorldModelBoundary[]? ModelBoundaries, double Cost, double Distance);

    private static bool RequiresWorldTargetRearming(Ff7AutoPlayWorldMovementState source, Ff7AutoPlayWorldScriptObservation target)
    {
        return source.Input.TerrainScript == target.Script && target.Faces.Any(face => face.Source == source.Input.GroundTriangle);
    }

    internal static Ff7AutoPlayWorldRoute SearchWorldRoute(Ff7AutoPlayWorldMovementState source, Ff7AutoPlayWorldScriptObservation target, Func<Ff7AutoplayInput, Ff7AutoPlayWorldMovementState, Ff7AutoPlayWorldMovementPreview> preview, IReadOnlyList<Vector3> departureHints, int nodeBudget, IReadOnlySet<Ff7AutoplayInput>? avoidFirst = null, Vector3? waypoint = null, Func<Func<WorldPhysicalSearchCursor>, int, Ff7AutoPlayWorldRoute>? resume = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(nodeBudget, resume is null ? 1 : 0);
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
        return SearchWorldPhysicalRoute(source, preview, Distance, Admit, Reached, nodeBudget, avoidFirst, rearming ? Ff7AutoPlayWorldNavigationStatus.Rearming : Ff7AutoPlayWorldNavigationStatus.Step, resume: resume);
    }

    private WorldPhysicalSearchCursor? worldPhysicalSearch;
    private string? worldPhysicalSearchContext;
    private object? worldPhysicalSearchOwner;
    internal bool HasPendingWorldPhysicalSearch => worldPhysicalSearch is { FrontierCount: > 0 };

    private string ReadWorldPhysicalSearchContext(Ff7WorldRuntime world, Ff7AutoPlayWorldMovementState source, string goal)
    {
        Ff7WorldProcess process = world.Process;
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            Goal = goal,
            Source = source,
            Context = world.ReadAutoPlayNavigationContext(),
            process.Tick,
            process.Vm,
            process.Entities,
            process.Vehicle,
            process.Chocobo,
            process.Snow,
            process.Marsh
        })));
    }

    internal Ff7AutoPlayWorldRoute ResumeWorldPhysicalSearch(string context, Func<WorldPhysicalSearchCursor> create, int quantum, object? owner = null)
    {
        if (!ReferenceEquals(worldPhysicalSearchOwner, owner) || worldPhysicalSearchContext != context || worldPhysicalSearch is null)
        {
            worldPhysicalSearch = create();
            worldPhysicalSearchContext = context;
            worldPhysicalSearchOwner = owner;
        }
        long before = worldPhysicalSearch.Expanded;
        Ff7AutoPlayWorldRoute result = worldPhysicalSearch.Resume(quantum);
        PhysicalPlanningWork += worldPhysicalSearch.Expanded - before;
        PlanningPending = result.Status == Ff7AutoPlayWorldNavigationStatus.SearchLimited && result.Steps.Length == 0;
        if (!PlanningPending)
        {
            worldPhysicalSearch = null;
            worldPhysicalSearchContext = null;
            worldPhysicalSearchOwner = null;
        }
        return result;
    }

    private static Ff7AutoPlayWorldRoute SearchWorldPhysicalRoute(Ff7AutoPlayWorldMovementState source, Func<Ff7AutoplayInput, Ff7AutoPlayWorldMovementState, Ff7AutoPlayWorldMovementPreview> preview, Func<Ff7AutoPlayWorldMovementState, double> distanceToTarget, Func<Ff7AutoPlayWorldMovementPreview, bool> admit, Func<Ff7AutoPlayWorldMovementPreview, bool> reachedTarget, int nodeBudget, IReadOnlySet<Ff7AutoplayInput>? avoidFirst = null, Ff7AutoPlayWorldNavigationStatus success = Ff7AutoPlayWorldNavigationStatus.Step, ushort held = 0, Func<Func<WorldPhysicalSearchCursor>, int, Ff7AutoPlayWorldRoute>? resume = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(nodeBudget, resume is null ? 1 : 0);
        WorldPhysicalSearchCursor Create() => new(source, preview, distanceToTarget, admit, reachedTarget, avoidFirst, success, held);
        return resume is null ? Create().Resume(nodeBudget) : resume(Create, nodeBudget);
    }

    internal sealed class WorldPhysicalSearchCursor
    {
        private readonly Func<Ff7AutoplayInput, Ff7AutoPlayWorldMovementState, Ff7AutoPlayWorldMovementPreview> preview;
        private readonly Func<Ff7AutoPlayWorldMovementState, double> distanceToTarget;
        private readonly Func<Ff7AutoPlayWorldMovementPreview, bool> admit;
        private readonly Func<Ff7AutoPlayWorldMovementPreview, bool> reachedTarget;
        private readonly HashSet<Ff7AutoplayInput>? avoidFirst;
        private readonly Ff7AutoPlayWorldNavigationStatus success;
        private readonly ushort held;
        private readonly Ff7AutoPlayWorldMovementState source;
        private readonly List<WorldSearchNode> nodes;
        private readonly Dictionary<WorldPhysicalKey, double> costs;
        private readonly Dictionary<WorldPhysicalKey, List<byte[]>> randomTables = [];
        private readonly PriorityQueue<int, (double Score, double Distance, int Order)> open = new();
        private int best = -1;
        private string? unsupported;
        private Ff7AutoPlayWorldRoute? terminal;
        internal long Expanded { get; private set; }
        internal int FrontierCount => open.Count;
        internal int VisitedCount => costs.Count;
        internal int RetainedStateCount => nodes.Count(node => node.State is not null);
        internal int RetainedRandomTableCount => randomTables.Values.Sum(tables => tables.Count);

        internal WorldPhysicalSearchCursor(Ff7AutoPlayWorldMovementState source, Func<Ff7AutoplayInput, Ff7AutoPlayWorldMovementState, Ff7AutoPlayWorldMovementPreview> preview, Func<Ff7AutoPlayWorldMovementState, double> distanceToTarget, Func<Ff7AutoPlayWorldMovementPreview, bool> admit, Func<Ff7AutoPlayWorldMovementPreview, bool> reachedTarget, IReadOnlySet<Ff7AutoplayInput>? avoidFirst = null, Ff7AutoPlayWorldNavigationStatus success = Ff7AutoPlayWorldNavigationStatus.Step, ushort held = 0)
        {
            this.preview = preview;
            this.distanceToTarget = distanceToTarget;
            this.admit = admit;
            this.reachedTarget = reachedTarget;
            this.avoidFirst = avoidFirst?.ToHashSet();
            this.success = success;
            this.held = Ff7WorldRuntime.IsAutoPlayAircraft(source.Player) ? (ushort)(held | 0x80) : held;
            this.source = RetainRandomTable(source);
            WorldPhysicalKey key = WorldPhysicalKey.Read(WorldMovementKey(source));
            WorldPhysicalKey searchKey = WorldPhysicalKey.Read(WorldSearchKey(source));
            nodes = [new(this.source, key, searchKey, -1, default, null, null, 0, distanceToTarget(source))];
            costs = new() { [searchKey] = 0 };
            open.Enqueue(0, (nodes[0].Distance, nodes[0].Distance, 0));
        }

        private Ff7AutoPlayWorldMovementState RetainRandomTable(Ff7AutoPlayWorldMovementState state)
        {
            if (state.Random is not { } random)
            {
                return state;
            }
            WorldPhysicalKey key = WorldPhysicalKey.Read(random);
            if (!randomTables.TryGetValue(key, out List<byte[]>? tables))
            {
                tables = [];
                randomTables.Add(key, tables);
            }
            foreach (byte[] known in tables)
            {
                if (random.AsSpan().SequenceEqual(known))
                {
                    return state with { Random = known };
                }
            }
            // Native previews copy the saved random table before drawing or refreshing it.
            byte[] retained = random.ToArray();
            tables.Add(retained);
            return state with { Random = retained };
        }

        internal Ff7AutoPlayWorldRoute Resume(int nodeBudget)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(nodeBudget, 0);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(nodeBudget, 16384);
            if (terminal is not null)
            {
                return terminal with { Expanded = 0 };
            }
            int expanded = 0;
            while (expanded < nodeBudget && open.TryDequeue(out int current, out _))
            {
                WorldSearchNode node = nodes[current];
                nodes[current] = node with { State = null };
                if (costs[node.SearchKey] != node.Cost)
                {
                    continue;
                }
                expanded++;
                Expanded++;
                foreach (Ff7AutoplayInput input in WorldNavigationInputs(held))
                {
                    if (current == 0 && avoidFirst?.Contains(input) == true)
                    {
                        continue;
                    }
                    Ff7AutoPlayWorldMovementPreview step = preview(input, node.State!);
                    if (!step.Supported || step.State is null)
                    {
                        unsupported ??= step.Reason;
                        continue;
                    }
                    if (!admit(step))
                    {
                        continue;
                    }
                    WorldPhysicalKey searchKey = WorldPhysicalKey.Read(WorldSearchKey(step.State));
                    double cost = node.Cost + Math.Max(1, WorldPointDistance(node.State!.Player, new(step.State.Player.X, step.State.Player.Y, step.State.Player.Z)));
                    bool reached = reachedTarget(step);
                    if (!reached && costs.TryGetValue(searchKey, out double prior) && prior <= cost)
                    {
                        continue;
                    }
                    WorldPhysicalKey key = WorldPhysicalKey.Read(WorldMovementKey(step.State));
                    double distance = distanceToTarget(step.State);
                    int index = nodes.Count;
                    nodes.Add(new(RetainRandomTable(step.State), key, searchKey, current, input, step.TerrainBoundary, step.ModelBoundaries, cost, distance));
                    if (reached)
                    {
                        terminal = new(success, ReconstructRoute(index), expanded, null);
                        return terminal;
                    }
                    costs[searchKey] = cost;
                    open.Enqueue(index, (cost + distance, distance, index));
                    if (step.Moved && distance < nodes[0].Distance && (best < 0 || distance < nodes[best].Distance || distance == nodes[best].Distance && cost < nodes[best].Cost))
                    {
                        best = index;
                    }
                }
            }

            if (open.Count == 0)
            {
                terminal = new(unsupported is null ? Ff7AutoPlayWorldNavigationStatus.Blocked : Ff7AutoPlayWorldNavigationStatus.Unsupported, [], expanded, unsupported ?? "No distinct admissible native step remains in the explored physical state graph.");
                return terminal;
            }
            return best >= 0
                ? new(Ff7AutoPlayWorldNavigationStatus.SearchLimited, ReconstructRoute(best), expanded, "An admitted partial route is available; the goal is not yet proved reachable.")
                : new(Ff7AutoPlayWorldNavigationStatus.SearchLimited, [], expanded, "The cooperative native physical search remains pending.");
        }

        private Ff7AutoPlayWorldRouteStep[] ReconstructRoute(int index)
        {
            Stack<int> path = [];
            while (nodes[index].Parent >= 0)
            {
                path.Push(index);
                index = nodes[index].Parent;
            }
            Ff7AutoPlayWorldMovementState state = source;
            string sourceKey = WorldMovementKey(state);
            List<Ff7AutoPlayWorldRouteStep> result = [];
            foreach (int nodeIndex in path)
            {
                WorldSearchNode node = nodes[nodeIndex];
                Ff7AutoPlayWorldMovementPreview step = preview(node.Input, state);
                if (!step.Supported || step.State is null || !admit(step) || WorldPhysicalKey.Read(WorldMovementKey(step.State)) != node.Key || step.TerrainBoundary != node.Boundary || !(step.ModelBoundaries ?? []).SequenceEqual(node.ModelBoundaries ?? []))
                {
                    throw new InvalidOperationException("A retained world route no longer reproduces its admitted native physical trace.");
                }
                string destinationKey = WorldMovementKey(step.State);
                result.Add(new(sourceKey, node.Input, destinationKey, node.Boundary, node.ModelBoundaries));
                sourceKey = destinationKey;
                state = step.State;
            }
            return result.ToArray();
        }
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
        // Ordinary foot motion can merge visual facing; snow wind captures the native facing at its onset.
        return WorldMovementKey(state, includePresentationFacing: false);
    }

    private static string WorldMovementKey(Ff7AutoPlayWorldMovementState state, bool includePresentationFacing)
    {
        Ff7WorldEntity p = state.Player;
        Ff7WorldInputState i = state.Input;
        bool facing = includePresentationFacing || state.WorldId == 3 || Ff7WorldRuntime.IsAutoPlayAircraft(p);
        StringBuilder key = new($"model-contact-admission:{WorldModelContactAdmissionRevision}:{state.WorldId}:{state.WorldMapState}:{p.Model}:{p.X}:{p.Y}:{p.Z}:{p.Terrain}:{p.Flags}:{p.ContactModel}:{(facing ? p.Direction : 0)}:{p.TargetDirection}:{(facing ? p.TurnOffset : 0)}:{state.ViewMode}:{i.CameraHeadingTarget ?? state.Camera.Heading}:{i.FreeDirectionOffset}:{i.PreviousControlHeld}:{i.CollisionSide}:{Math.Min(i.CollisionAge, 8)}:{i.CollisionRecovery}:{i.BridgeCooldown}:{i.HeightSmoothVelocity}:{i.HeightSmoothRemaining}:{i.TerrainScript}:{i.GroundTriangle}:{i.Bridge}");
        foreach (Ff7WorldTerrainReference cached in i.TerrainCache ?? [])
        {
            key.Append($"|{cached.ChunkX},{cached.ChunkZ},{cached.Triangle}");
        }
        if (state.WorldId == 3)
        {
            Ff7WorldSnowState? snow = state.Snow;
            key.Append($"|snow:{state.Camera.Heading}:{p.ScriptDirection}:{state.RandomIndex}:{(state.Random is null ? "none" : Convert.ToHexString(SHA256.HashData(state.Random)))}:{snow?.PoleIndex}:{snow?.Countdown}:{snow?.WindLength}:{snow?.WindVelocity}:{snow?.CycleLength}:{snow?.LockedFacing}:{JsonSerializer.Serialize(state.SnowPoles)}:{JsonSerializer.Serialize(state.ScriptCamera)}");
        }
        if (Ff7WorldRuntime.IsAutoPlayAircraft(p))
        {
            Ff7WorldVehicleState? physicalVehicle = state.Vehicle is null ? null : state.Vehicle with { HighwindPropellerFrame = 0, HighwindPropellerPoseFrame = null, FlightBasePitch = null, FlightBaseDistance = null };
            // Propeller frames and pitch/distance smoothing affect presentation; heading and the flight envelope drive motion.
            key.Append($"|aircraft:{p.GroundHeight}:{p.TerrainResolved}:{JsonSerializer.Serialize(physicalVehicle)}:{state.Camera.Heading}:{JsonSerializer.Serialize(state.ScriptCamera)}");
        }
        // Ordinary foot and aircraft control RNG draws have no physical return value. Snow wind consumes their values above.
        return key.ToString();
    }

    internal static string WorldPositionKey(Ff7AutoPlayWorldMovementState state)
    {
        // Repeated physical edges are exploration preferences, not equivalent native execution states.
        return $"{state.WorldId}:{state.WorldMapState}:{state.Player.Model}:{state.Player.X}:{state.Player.Y}:{state.Player.Z}";
    }
}
