using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;

namespace Memoria.FF8;

internal sealed record AutoPlayWalkingRoute(IReadOnlyList<WalkmeshPosition> Waypoints);

internal sealed partial class GameWorld
{
    public bool TryFindAutoPlayWalkingRoute(AutoPlayTarget target, out AutoPlayWalkingRoute route, AutoPlayIdleDeadline? idleDeadline = null, Func<FieldFixedPosition, int, Vector2, NativePlayerMovementResult>? probe = null)
    {
        route = new AutoPlayWalkingRoute([]);
        if (Field is null || !HasCurrentFieldAllocation || !Events.CanAcceptMovementInput || Events.PlayerActor is not ActorState player)
        {
            return false;
        }
        FieldFixedPosition start = Events.Scene.NativePlayerPosition ?? throw new NotSupportedException("Autoplay walking requires the retained native player position.");
        Vector3 destination = new(target.ApproachX ?? target.X, target.ApproachY ?? target.Y, target.ApproachZ ?? target.Z);
        int triangle = target.ApproachTriangle ?? target.Triangle;
        using IDisposable? suspended = idleDeadline?.Suspend();
        return TryFindAutoPlayWalkingRoute(Field.Walkmesh, Events.Scene.LockedTriangles, start, player.WalkmeshTriangle, new WalkmeshPosition(destination, triangle), Events.AutoPlayMovementStepDistance, probe ?? Events.ProbeAutoPlayMovement, out route);
    }

    internal static bool TryFindAutoPlayWalkingRoute(FieldWalkmesh mesh, IReadOnlySet<int> locked, FieldFixedPosition start, int startTriangle, WalkmeshPosition destination, float stepDistance, Func<FieldFixedPosition, int, Vector2, NativePlayerMovementResult> probe, out AutoPlayWalkingRoute route, int maximumProbes = 1_000_000)
    {
        route = new AutoPlayWalkingRoute([]);
        if (!float.IsFinite(stepDistance) || stepDistance <= 0 || maximumProbes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(stepDistance));
        }
        if ((uint)startTriangle >= mesh.Triangles.Count || (uint)destination.Triangle >= mesh.Triangles.Count || locked.Contains(startTriangle) || locked.Contains(destination.Triangle))
        {
            return false;
        }
        HashSet<int> reachable = AutoPlayReachableTriangles(mesh, startTriangle, locked);
        if (!reachable.Contains(destination.Triangle))
        {
            return false;
        }
        int probes = 0;
        float tolerance = Math.Max(0.125f, stepDistance * 0.75f);
        Vector3 source = Position(start);
        if (TrySegment(start, startTriangle, destination, out _, out _))
        {
            route = new AutoPlayWalkingRoute(Array.AsReadOnly(new[] { destination }));
            return true;
        }

        List<WalkmeshPosition> nodes = [];
        Dictionary<int, List<int>> triangleNodes = [];
        Dictionary<(int First, int Second), List<int>> portals = [];
        foreach (int index in reachable.Order())
        {
            WalkmeshTriangle triangle = mesh.Triangles[index];
            Vector3 center = (triangle.A + triangle.B + triangle.C) / 3;
            AddNode(center, index);
            AddPortal(triangle.A, triangle.B, triangle.NeighborA);
            AddPortal(triangle.B, triangle.C, triangle.NeighborB);
            AddPortal(triangle.C, triangle.A, triangle.NeighborC);

            void AddPortal(Vector3 a, Vector3 b, int neighbor)
            {
                if (!reachable.Contains(neighbor) || locked.Contains(neighbor))
                {
                    return;
                }
                var key = (Math.Min(index, neighbor), Math.Max(index, neighbor));
                if (!portals.TryGetValue(key, out List<int>? entries))
                {
                    entries = [];
                    portals.Add(key, entries);
                }
                for (int sample = 1; sample <= 3; sample++)
                {
                    // Interior samples avoid assigning an ambiguous shared-edge plane.
                    Vector3 point = Vector3.Lerp(Vector3.Lerp(a, b, sample / 4f), center, 0.02f);
                    entries.Add(AddNode(point, index));
                }
            }
        }
        int sourceNode = AddNode(source, startTriangle);
        int goalNode = AddNode(destination.Position, destination.Triangle);
        Dictionary<int, List<int>> crossing = [];
        foreach (List<int> entries in portals.Values)
        {
            foreach (int from in entries)
            {
                crossing[from] = entries.Where(to => nodes[to].Triangle != nodes[from].Triangle).ToList();
            }
        }
        float[] costs = Enumerable.Repeat(float.PositiveInfinity, nodes.Count).ToArray();
        int[] parents = Enumerable.Repeat(-1, nodes.Count).ToArray();
        FieldFixedPosition[] positions = new FieldFixedPosition[nodes.Count];
        int[] triangles = new int[nodes.Count];
        bool[] closed = new bool[nodes.Count];
        PriorityQueue<int, float> pending = new();
        costs[sourceNode] = 0;
        positions[sourceNode] = start;
        triangles[sourceNode] = startTriangle;
        pending.Enqueue(sourceNode, 0);
        while (pending.TryDequeue(out int current, out _))
        {
            if (closed[current])
            {
                continue;
            }
            closed[current] = true;
            if (current == goalNode)
            {
                List<WalkmeshPosition> path = [];
                for (int node = current; node != sourceNode; node = parents[node])
                {
                    path.Add(nodes[node]);
                }
                path.Reverse();
                route = new AutoPlayWalkingRoute(path.AsReadOnly());
                return true;
            }
            IEnumerable<int> neighbors = triangleNodes[nodes[current].Triangle];
            if (crossing.TryGetValue(current, out List<int>? across))
            {
                neighbors = neighbors.Concat(across);
            }
            foreach (int next in neighbors)
            {
                if (next == current || closed[next] || !TrySegment(positions[current], triangles[current], nodes[next], out FieldFixedPosition end, out int endTriangle))
                {
                    continue;
                }
                float cost = costs[current] + Vector3.Distance(Position(positions[current]), Position(end));
                if (cost >= costs[next])
                {
                    continue;
                }
                costs[next] = cost;
                positions[next] = end;
                triangles[next] = endTriangle;
                parents[next] = current;
                pending.Enqueue(next, cost + Vector3.Distance(Position(end), destination.Position));
            }
        }
        return false;

        int AddNode(Vector3 point, int triangle)
        {
            if (nodes.Count >= 100_002)
            {
                throw new NotSupportedException("Autoplay walking visibility graph exceeded its bounded node budget.");
            }
            int node = nodes.Count;
            nodes.Add(new WalkmeshPosition(point, triangle));
            if (!triangleNodes.TryGetValue(triangle, out List<int>? entries))
            {
                entries = [];
                triangleNodes.Add(triangle, entries);
            }
            entries.Add(node);
            return node;
        }

        bool TrySegment(FieldFixedPosition origin, int triangle, WalkmeshPosition goal, out FieldFixedPosition end, out int endTriangle)
        {
            end = origin;
            endTriangle = triangle;
            Vector2 from = new(origin.X / 4096f, origin.Y / 4096f);
            Vector2 target = new(goal.Position.X, goal.Position.Y);
            HashSet<(int X, int Y, int Triangle)> visited = [];
            int attempts = checked((int)Math.Min(100_000, Math.Ceiling(Vector2.Distance(from, target) / stepDistance) * 4 + 16));
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                Vector3 current = Position(end);
                Vector2 location = new(current.X, current.Y);
                if (AutoPlayWalkingWaypointReached(mesh, end, endTriangle, goal, tolerance))
                {
                    return true;
                }
                if (!visited.Add((end.X, end.Y, endTriangle)))
                {
                    return false;
                }
                if (++probes > maximumProbes)
                {
                    throw new NotSupportedException("Autoplay walking visibility search exceeded its bounded native probe budget.");
                }
                NativePlayerMovementResult movement = probe(end, endTriangle, target - location);
                if (!movement.Accepted)
                {
                    return false;
                }
                end = new FieldFixedPosition(movement.FixedX, movement.FixedY, movement.FixedZ);
                endTriangle = movement.Triangle;
                if (locked.Contains(endTriangle) || (uint)endTriangle >= mesh.Triangles.Count)
                {
                    return false;
                }
                Vector2 next = new(end.X / 4096f, end.Y / 4096f);
                Vector2 segment = target - from;
                float along = segment.LengthSquared() == 0 ? 0 : Math.Clamp(Vector2.Dot(next - from, segment) / segment.LengthSquared(), 0, 1);
                if (Vector2.Distance(next, from + segment * along) > Math.Max(4, stepDistance * 4))
                {
                    return false;
                }
            }
            return false;
        }

        static Vector3 Position(FieldFixedPosition value) => new(value.X / 4096f, value.Y / 4096f, value.Z / 4096f);
    }

    internal static bool AutoPlayWalkingWaypointReached(FieldWalkmesh mesh, FieldFixedPosition position, int triangle, WalkmeshPosition waypoint, float tolerance)
    {
        if (triangle != waypoint.Triangle || Vector2.Distance(new Vector2(position.X / 4096f, position.Y / 4096f), new Vector2(waypoint.Position.X, waypoint.Position.Y)) > tolerance)
        {
            return false;
        }
        // A slope's height changes across the allowed planar approach distance.
        // Retain exact triangle ownership and validate the player's own native plane,
        // rather than comparing heights sampled at two different XY positions.
        int? height = FieldPartyTraceGeometry.ResolveTriangleHeight(mesh, triangle, position.X / 4096, position.Y / 4096);
        return height is int nativeHeight && position.Z == checked(nativeHeight * 4096);
    }
}
