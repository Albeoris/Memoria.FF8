namespace Memoria.FF7.Runtime;

internal sealed record Ff7AutoPlayWorldModeGuide(Ff7AutoPlayWorldGuide TransportGuide, int RiderModel, Ff7WorldTerrainReference? LandingSource, Ff7WorldTerrainReference? RiderLanding, Ff7AutoPlayWorldGuide RiderGuide, Ff7WorldTerrainReference[] RiderReachableFaces, int Expanded, bool Limited, string? Reason);

public sealed partial class Ff7WorldRuntime
{
    internal Ff7WorldModeTopologyCursor CreateAutoPlaySurfaceVehicleGuide(Ff7AutoPlayWorldScriptObservation target, Func<Ff7AutoPlayWorldGuideFace, Ff7AutoPlayWorldGuideFace?, Ff7WorldTerrainReference?> readLanding, IReadOnlySet<(Ff7WorldTerrainReference From, Ff7WorldTerrainReference To)>? unproductiveEdges = null)
    {
        return new(EnumerateAutoPlaySurfaceVehicleGuide(target.Faces.Select(face => face.Source).ToHashSet(), readLanding, unproductiveEdges).GetEnumerator());
    }

    internal Ff7AutoPlayWorldModeGuide ReadAutoPlaySurfaceVehicleGuide(Ff7AutoPlayWorldScriptObservation target, Func<Ff7AutoPlayWorldGuideFace, Ff7AutoPlayWorldGuideFace?, Ff7WorldTerrainReference?> readLanding, int maximumNodes = 16384, IReadOnlySet<(Ff7WorldTerrainReference From, Ff7WorldTerrainReference To)>? unproductiveEdges = null)
    {
        return ReadAutoPlaySurfaceVehicleGuide(target.Faces.Select(face => face.Source).ToHashSet(), readLanding, maximumNodes, unproductiveEdges);
    }

    internal Ff7AutoPlayWorldModeGuide ReadAutoPlaySurfaceVehicleGuide(IReadOnlySet<Ff7WorldTerrainReference> goals, Func<Ff7AutoPlayWorldGuideFace, Ff7AutoPlayWorldGuideFace?, Ff7WorldTerrainReference?> readLanding, int maximumNodes = 16384, IReadOnlySet<(Ff7WorldTerrainReference From, Ff7WorldTerrainReference To)>? unproductiveEdges = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumNodes, 1);
        using Ff7WorldModeTopologyCursor cursor = new(EnumerateAutoPlaySurfaceVehicleGuide(goals, readLanding, unproductiveEdges).GetEnumerator());
        cursor.Resume(new(maximumNodes, TimeSpan.MaxValue));
        Ff7AutoPlayWorldGuide pending = new([], (int)cursor.Operations, true, "The native vehicle/rider topology traversal has remaining work.");
        return cursor.Result ?? new(pending, ReadSurfaceVehicleRider(), null, null, pending, [], (int)cursor.Operations, true, pending.Reason);
    }

    private IEnumerable<Ff7AutoPlayWorldModeGuide?> EnumerateAutoPlaySurfaceVehicleGuide(IReadOnlySet<Ff7WorldTerrainReference> goals, Func<Ff7AutoPlayWorldGuideFace, Ff7AutoPlayWorldGuideFace?, Ff7WorldTerrainReference?> readLanding, IReadOnlySet<(Ff7WorldTerrainReference From, Ff7WorldTerrainReference To)>? unproductiveEdges)
    {
        ArgumentNullException.ThrowIfNull(readLanding);
        int rider = ReadSurfaceVehicleRider();
        Ff7AutoPlayWorldModeGuide Unavailable(int expanded, bool limited, string reason)
        {
            Ff7AutoPlayWorldGuide empty = new([], expanded, limited, reason);
            return new(empty, rider, null, null, empty, [], expanded, limited, reason);
        }
        if (Resources.World != 0 || ReadAutoPlaySurfaceVehicleRider() is null || rider == controlledModel || ReadAutoPlayAdviceGround(CaptureAutoPlayMovementState()) is not { } start)
        {
            yield return Unavailable(0, false, "No native surface-vehicle rider route is available.");
            yield break;
        }
        AutoPlayTerrainGraph graph = CreateAutoPlayTerrainGraph(goals, true, 36 * 28);
        foreach (Ff7AutoPlayWorldModeGuide? result in EnumerateSurfaceVehicleTopology(graph, goals, start, controlledModel, rider, RequireEntity(controlledModel).Terrain, terrain => CanLeaveSurfaceVehicle(controlledModel, terrain & 31) && (controlledModel != SubmarineModel || (terrain & 31) == 18), readLanding, unproductiveEdges))
        {
            yield return result;
        }
    }

    internal static IEnumerable<Ff7AutoPlayWorldModeGuide?> EnumerateSurfaceVehicleTopology(AutoPlayTerrainGraph graph, IReadOnlySet<Ff7WorldTerrainReference> goals, Ff7WorldTerrainReference start, int transportModel, int rider, int startTerrain, Func<int, bool> canLeave, Func<Ff7AutoPlayWorldGuideFace, Ff7AutoPlayWorldGuideFace?, Ff7WorldTerrainReference?> readLanding, IReadOnlySet<(Ff7WorldTerrainReference From, Ff7WorldTerrainReference To)>? unproductiveEdges = null)
    {
        graph.Read(start);
        int bookkeeping = 0;
        long batchStarted = System.Diagnostics.Stopwatch.GetTimestamp();
        bool BatchFinished() => ++bookkeeping >= 64 || System.Diagnostics.Stopwatch.GetElapsedTime(batchStarted) >= TimeSpan.FromMilliseconds(8);
        void RestartBatch()
        {
            bookkeeping = 0;
            batchStarted = System.Diagnostics.Stopwatch.GetTimestamp();
        }
        Dictionary<Ff7WorldTerrainReference, Ff7WorldTerrainReference> riderNext = [];
        HashSet<Ff7WorldTerrainReference> riderReachable = [];
        Queue<Ff7WorldTerrainReference> reverse = new();
        foreach (Ff7WorldTerrainReference goal in goals)
        {
            if (!graph.Ensure(goal))
            {
                throw new InvalidDataException("A native world goal could not be materialized within the complete world extent.");
            }
            if (graph.Eligible(goal, graph.ReadTerrain(goal), rider) && riderReachable.Add(goal))
            {
                reverse.Enqueue(goal);
            }
            if (BatchFinished())
            {
                yield return null;
                RestartBatch();
            }
        }
        int expanded = 0;
        while (reverse.TryDequeue(out Ff7WorldTerrainReference? reverseCurrent))
        {
            expanded++;
            foreach (Ff7WorldTerrainReference previous in graph.Adjacent(reverseCurrent, rider, graph.ReadTerrain(reverseCurrent), true))
            {
                if (graph.Eligible(previous, graph.ReadTerrain(previous), rider) && riderReachable.Add(previous))
                {
                    riderNext.Add(previous, reverseCurrent);
                    reverse.Enqueue(previous);
                }
            }
            if (BatchFinished())
            {
                yield return null;
                RestartBatch();
            }
        }

        if (bookkeeping > 0)
        {
            yield return null;
        }

        graph.Read(start);
        Dictionary<Ff7WorldTerrainReference, double> costs = new() { [start] = 0 };
        Dictionary<Ff7WorldTerrainReference, Ff7WorldTerrainReference> parents = [];
        HashSet<Ff7WorldTerrainReference> closed = [];
        PriorityQueue<Ff7WorldTerrainReference, double> open = new();
        open.Enqueue(start, 0);
        int transportExpanded = 0;
        while (open.TryDequeue(out Ff7WorldTerrainReference? current, out _))
        {
            if (!closed.Add(current))
            {
                continue;
            }
            yield return null;
            transportExpanded++;
            Ff7AutoPlayWorldGuideFace source = graph.Read(current);
            Ff7AutoPlayWorldGuideFace? predecessor = parents.TryGetValue(current, out Ff7WorldTerrainReference? previous) ? graph.Read(previous) : null;
            int sourceTerrain = current == start ? startTerrain : source.Terrain;
            if (canLeave(sourceTerrain))
            {
                Ff7WorldTerrainReference? landing = readLanding(source, predecessor);
                if (landing is not null && riderReachable.Contains(landing) && graph.Eligible(landing, graph.ReadTerrain(landing), rider))
                {
                    List<Ff7AutoPlayWorldGuideFace> transportRoute = [source];
                    Ff7WorldTerrainReference cursor = current;
                    while (parents.TryGetValue(cursor, out Ff7WorldTerrainReference? parent))
                    {
                        transportRoute.Add(graph.Read(parent));
                        cursor = parent;
                    }
                    transportRoute.Reverse();
                    List<Ff7AutoPlayWorldGuideFace> riderRoute = [graph.Read(landing)];
                    cursor = landing;
                    while (riderNext.TryGetValue(cursor, out Ff7WorldTerrainReference? next))
                    {
                        riderRoute.Add(graph.Read(next));
                        cursor = next;
                    }
                    yield return new(new(transportRoute.ToArray(), transportExpanded, false, null), rider, current, landing, new(riderRoute.ToArray(), expanded, false, null), riderReachable.ToArray(), expanded + transportExpanded, false, null);
                    yield break;
                }
            }
            foreach (Ff7WorldTerrainReference next in graph.Adjacent(current, transportModel, sourceTerrain, false).Where(next => unproductiveEdges?.Contains((current, next)) != true))
            {
                System.Numerics.Vector3 a = source.Center;
                System.Numerics.Vector3 b = graph.Read(next).Center;
                double dx = Math.Abs(a.X - b.X);
                double dz = Math.Abs(a.Z - b.Z);
                dx = Math.Min(dx, 0x48000 - dx);
                dz = Math.Min(dz, 0x38000 - dz);
                double cost = costs[current] + Math.Sqrt(dx * dx + dz * dz);
                if (closed.Contains(next) || costs.TryGetValue(next, out double known) && known <= cost)
                {
                    continue;
                }
                costs[next] = cost;
                parents[next] = current;
                open.Enqueue(next, cost);
            }
        }
        string reason = "No legal native landing source connects the vehicle component to the rider goal component.";
        Ff7AutoPlayWorldGuide empty = new([], expanded + transportExpanded, false, reason);
        yield return new(empty, rider, null, null, empty, [], expanded + transportExpanded, false, reason);
    }
}

internal sealed class Ff7WorldModeTopologyCursor(IEnumerator<Ff7AutoPlayWorldModeGuide?> search) : IDisposable
{
    private IEnumerator<Ff7AutoPlayWorldModeGuide?>? remaining = search;
    internal bool Completed { get; private set; }
    internal bool HasPendingWork => !Completed && remaining is not null;
    internal long Operations { get; private set; }
    internal Ff7AutoPlayWorldModeGuide? Result { get; private set; }
    internal Ff7AutoPlayWorldGuide? BoardingContactGuide { get; set; }

    internal void Resume(Ff7NativePlanningQuantum quantum)
    {
        IEnumerator<Ff7AutoPlayWorldModeGuide?>? current = remaining;
        if (current is null)
        {
            return;
        }
        while (!Completed && quantum.TryBeginOperation())
        {
            Operations++;
            if (!current.MoveNext())
            {
                Completed = true;
                Dispose();
            }
            else if (current.Current is { } result)
            {
                Result = result;
                Completed = true;
                Dispose();
            }
        }
    }

    public void Dispose()
    {
        remaining?.Dispose();
        remaining = null;
        BoardingContactGuide = null;
    }
}
