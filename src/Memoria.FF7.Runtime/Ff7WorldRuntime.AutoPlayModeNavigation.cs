namespace Memoria.FF7.Runtime;

internal sealed record Ff7AutoPlayWorldModeGuide(Ff7AutoPlayWorldGuide TransportGuide, int RiderModel, Ff7WorldTerrainReference? LandingSource, Ff7WorldTerrainReference? RiderLanding, Ff7AutoPlayWorldGuide RiderGuide, Ff7WorldTerrainReference[] RiderReachableFaces, int Expanded, bool Limited, string? Reason);

public sealed partial class Ff7WorldRuntime
{
    internal Ff7AutoPlayWorldModeGuide ReadAutoPlaySurfaceVehicleGuide(Ff7AutoPlayWorldScriptObservation target, Func<Ff7AutoPlayWorldGuideFace, Ff7AutoPlayWorldGuideFace?, Ff7WorldTerrainReference?> readLanding, int maximumNodes = 16384, int maximumChunks = 128)
    {
        return ReadAutoPlaySurfaceVehicleGuide(target.Faces.Select(face => face.Source).ToHashSet(), readLanding, maximumNodes, maximumChunks);
    }

    internal Ff7AutoPlayWorldModeGuide ReadAutoPlaySurfaceVehicleGuide(IReadOnlySet<Ff7WorldTerrainReference> goals, Func<Ff7AutoPlayWorldGuideFace, Ff7AutoPlayWorldGuideFace?, Ff7WorldTerrainReference?> readLanding, int maximumNodes = 16384, int maximumChunks = 128)
    {
        ArgumentNullException.ThrowIfNull(readLanding);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumNodes, 1);
        int rider = ReadSurfaceVehicleRider();
        Ff7AutoPlayWorldModeGuide Unavailable(int expanded, bool limited, string reason)
        {
            Ff7AutoPlayWorldGuide empty = new([], expanded, limited, reason);
            return new(empty, rider, null, null, empty, [], expanded, limited, reason);
        }
        if (Resources.World != 0 || ReadAutoPlaySurfaceVehicleRider() is null || rider == controlledModel || input.GroundTriangle is not { } start)
        {
            return Unavailable(0, false, "No native surface-vehicle rider route is available.");
        }
        AutoPlayTerrainGraph graph = CreateAutoPlayTerrainGraph(goals, true, maximumChunks);
        graph.Read(start);
        Dictionary<Ff7WorldTerrainReference, Ff7WorldTerrainReference> riderNext = [];
        HashSet<Ff7WorldTerrainReference> riderReachable = [];
        Queue<Ff7WorldTerrainReference> reverse = new();
        foreach (Ff7WorldTerrainReference goal in goals)
        {
            if (!graph.Ensure(goal))
            {
                return Unavailable(0, true, "The native terrain-advice chunk budget was exhausted while observing rider goals.");
            }
            if (graph.Eligible(goal, graph.ReadTerrain(goal), rider) && riderReachable.Add(goal))
            {
                reverse.Enqueue(goal);
            }
        }
        int expanded = 0;
        while (expanded < maximumNodes && reverse.TryDequeue(out Ff7WorldTerrainReference? reverseCurrent))
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
        }
        // A partial reverse search can still supply positive routes, but never a negative component proof.
        bool riderLimited = reverse.Count > 0 || graph.IsLimited();
        graph.Read(start);
        Dictionary<Ff7WorldTerrainReference, double> costs = new() { [start] = 0 };
        Dictionary<Ff7WorldTerrainReference, Ff7WorldTerrainReference> parents = [];
        HashSet<Ff7WorldTerrainReference> closed = [];
        PriorityQueue<Ff7WorldTerrainReference, double> open = new();
        open.Enqueue(start, 0);
        int transportExpanded = 0;
        while (transportExpanded < maximumNodes && open.TryDequeue(out Ff7WorldTerrainReference? current, out _))
        {
            if (!closed.Add(current))
            {
                continue;
            }
            transportExpanded++;
            Ff7AutoPlayWorldGuideFace source = graph.Read(current);
            Ff7AutoPlayWorldGuideFace? predecessor = parents.TryGetValue(current, out Ff7WorldTerrainReference? previous) ? graph.Read(previous) : null;
            int sourceTerrain = current == start ? RequireEntity(controlledModel).Terrain : source.Terrain;
            if (CanLeaveSurfaceVehicle(controlledModel, sourceTerrain & 31) && (controlledModel != SubmarineModel || (sourceTerrain & 31) == 18))
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
                    return new(new(transportRoute.ToArray(), transportExpanded, false, null), rider, current, landing, new(riderRoute.ToArray(), expanded, false, null), riderReachable.ToArray(), expanded + transportExpanded, false, null);
                }
            }
            foreach (Ff7WorldTerrainReference next in graph.Adjacent(current, controlledModel, sourceTerrain, false))
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
        bool limited = riderLimited || open.Count > 0 || graph.IsLimited();
        return Unavailable(expanded + transportExpanded, limited, limited ? "The bounded vehicle-to-rider terrain advice is incomplete." : "No legal native landing source connects the vehicle component to the rider goal component.");
    }
}
