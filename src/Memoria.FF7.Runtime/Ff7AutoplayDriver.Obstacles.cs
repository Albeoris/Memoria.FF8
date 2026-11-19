using System.Numerics;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

internal enum Ff7AutoPlayPhysicalSearchStatus
{
    Progress,
    Exhausted,
    Pending
}

internal sealed record Ff7AutoPlayPhysicalRoute(Ff7AutoPlayPhysicalSearchStatus Status, Ff7AutoplayInput[] Inputs, int Expanded);

public sealed partial class Ff7AutoplayDriver
{
    private readonly Queue<Vector3> obstacleHistory = new();
    private readonly Queue<EscapeStep> obstacleEscape = new();
    private long obstacleGeneration = -1;
    private Vector3? obstacleTarget;
    private int obstacleSpeed;
    private PhysicalSearchCursor? obstacleSearch;
    private string? obstacleSearchContext;
    private Vector3? obstacleSearchAlternative;
    internal long PhysicalPlanningWork { get; private set; }

    private sealed record EscapeStep(Ff7AutoplayInput Input, int X4096, int Y4096, int Z4096, int Triangle);
    private sealed record SearchNode(Ff7Actor Actor, SearchNode? Parent, Ff7AutoplayInput Input, float Cost);

    private (Vector3 Point, int Triangle) ChooseFloorContact(IReadOnlyList<(Vector3 Point, int Triangle)> contacts, Ff7Actor player)
    {
        (Vector3 Point, int Triangle)? retained = null;
        if (obstacleGeneration == session.FieldGeneration && obstacleTarget is Vector3 target && obstacleEscape.TryPeek(out EscapeStep? next))
        {
            if (MatchesEscapeSource(player, next))
            {
                retained = (target, waypointDestination);
            }
            else
            {
                obstacleEscape.Clear();
            }
        }
        if (retained is null && approachAdviceExclusions.Count > 0 && obstacleGeneration == session.FieldGeneration && obstacleTarget is Vector3 acceptedTarget && approachGuideContext == ReadApproachGuideContext(player, acceptedTarget, waypointDestination))
        {
            retained = (acceptedTarget, waypointDestination);
        }
        return SelectFloorContact(contacts, Position3D(player), retained, player.Triangle);
    }

    internal static (Vector3 Point, int Triangle) SelectFloorContact(IReadOnlyList<(Vector3 Point, int Triangle)> contacts, Vector3 position, (Vector3 Point, int Triangle)? retained, int? sourceTriangle = null)
    {
        if (retained is { } selected && contacts.Contains(selected))
        {
            return selected;
        }
        var nearest = contacts.OrderBy(contact => Vector3.DistanceSquared(position, contact.Point)).ThenBy(contact => contact.Triangle).First();
        return contacts.Where(contact => contact.Point == nearest.Point).OrderBy(contact => contact.Triangle == sourceTriangle ? 0 : 1).ThenBy(contact => contact.Triangle).First();
    }

    private static bool MatchesEscapeSource(Ff7Actor player, EscapeStep step)
    {
        return player.X4096 == step.X4096 && player.Y4096 == step.Y4096 && player.Z4096 == step.Z4096 && player.Triangle == step.Triangle;
    }

    private void ResetObstacleEscape()
    {
        obstacleHistory.Clear();
        obstacleEscape.Clear();
        obstacleGeneration = -1;
        CancelObstacleSearch();
    }

    private void CancelObstacleSearch()
    {
        obstacleSearch = null;
        obstacleSearchContext = null;
        obstacleSearchAlternative = null;
    }

    private bool TryObstacleEscape(Ff7Actor player, Vector3 target, Vector3 waypoint, int destination, int nextTriangle, int[] routeDistance, bool blockedForward, IReadOnlyList<int> blockingEntities, Ff7NativeMovement movement, out Ff7AutoplayInput input)
    {
        input = default;
        if (obstacleGeneration != session.FieldGeneration || obstacleTarget != target)
        {
            ResetObstacleEscape();
            obstacleGeneration = session.FieldGeneration;
            obstacleTarget = target;
        }
        if (obstacleSpeed != player.MoveSpeed)
        {
            obstacleEscape.Clear();
            CancelObstacleSearch();
            obstacleSpeed = player.MoveSpeed;
        }
        string context = $"{ReadApproachGuideContext(player, target, destination)}|source:{player.Slot}:{player.X4096}:{player.Y4096}:{player.Z4096}:{player.Triangle}:{player.Direction}:{player.MoveSpeed}:{player.PlayerControlled}:{player.MovementFacingDisabled}|waypoint:{waypoint}:{nextTriangle}|mode:{navigationRunMode}|control:{session.Field.Triggers.ControlDirection}|input:{navigationLineInput}|talk:{navigationLineTalk}|targets:{string.Join(';', navigationLineTargets?.Select(candidate => $"{candidate.Id}:{candidate.ConditionFingerprint}") ?? [])}";
        if (obstacleSearchContext != context)
        {
            CancelObstacleSearch();
        }
        if (TakeEscapeStep(player, movement, out input))
        {
            return true;
        }
        if (!blockedForward && obstacleSearch is null)
        {
            Vector3 position = Position3D(player);
            obstacleHistory.Enqueue(position);
            if (obstacleHistory.Count < 24)
            {
                return false;
            }
            Vector3 previous = obstacleHistory.Dequeue();
            float step = player.MoveSpeed / 256f;
            if (Vector3.DistanceSquared(position, previous) >= step * step * 64 || Vector3.DistanceSquared(position, target) <= step * step * 4 || session.Host.Process.LineBlocked)
            {
                return false;
            }
        }
        obstacleHistory.Clear();
        Ff7NativeMoveResult Preview(Ff7Actor actor, Ff7AutoplayInput action)
        {
            var direction = Directions.Single(value => value.X == action.Horizontal && value.Y == action.Vertical);
            return movement.StepDirection(actor, unchecked((byte)(direction.Direction + session.Field.Triggers.ControlDirection)), session.Host.Process.Actors, session.Host.Process.BlockedTriangles, false, session.Host.Process.LineBlocked);
        }
        obstacleSearch ??= new(player, waypoint, triangle => routeDistance[triangle], Preview, AllowsPlannedMove);
        obstacleSearchContext = context;
        Ff7NativePlanningQuantum quantum = new();
        Ff7AutoPlayPhysicalRoute route = ResumePhysicalSearch(obstacleSearch, quantum);
        if (route.Status == Ff7AutoPlayPhysicalSearchStatus.Exhausted && obstacleSearchAlternative is null && blockedForward && nextTriangle != player.Triangle && !approachAdviceExclusions.Contains((player.Triangle, nextTriangle)))
        {
            // Try alternate advice only after exhausting the original represented search graph.
            // A failed physical prefix rejects this advice, not the native portal itself.
            HashSet<(int From, int To)> alternatives = ReadNavigationOwnedPortals(session.Field.Walkmesh);
            alternatives.UnionWith(approachAdviceExclusions);
            alternatives.Add((player.Triangle, nextTriangle));
            var (previous, distance) = ReadApproachRoutes(session.Field.Walkmesh, session.Host.Process.BlockedTriangles, player.Triangle, destination, spatialBoundaryGeneration == session.FieldGeneration ? spatialBoundaryTriangles : null, admittedSpatialTriangle, alternatives);
            int alternative = previous[player.Triangle];
            if (alternative >= 0 && alternative != nextTriangle)
            {
                var triangle = session.Field.Walkmesh[alternative];
                Vector3 alternativeWaypoint = (triangle.A + triangle.B + triangle.C) / 3;
                obstacleSearchAlternative = alternativeWaypoint;
                obstacleSearch = new(player, alternativeWaypoint, triangle => distance[triangle], Preview, AllowsPlannedMove);
                route = ResumePhysicalSearch(obstacleSearch, quantum);
            }
        }
        if (route.Status == Ff7AutoPlayPhysicalSearchStatus.Pending)
        {
            DeferPlanning();
            return true;
        }
        if (route.Status == Ff7AutoPlayPhysicalSearchStatus.Progress && obstacleSearchAlternative is Vector3 acceptedWaypoint)
        {
            approachAdviceExclusions.Add((player.Triangle, nextTriangle));
            committedWaypoint = acceptedWaypoint;
            waypointSourceTriangle = player.Triangle;
        }
        CancelObstacleSearch();
        if (route.Status != Ff7AutoPlayPhysicalSearchStatus.Progress)
        {
            if (blockedForward)
            {
                Ff7AutoPlayMovementFailure region = ReadApproachRegion(session.Field.Walkmesh, player, nextTriangle);
                throw new Ff7AutoPlayNavigationUnavailableException(-1, $"The current native approach search {route.Status.ToString().ToLowerInvariant()} after {route.Expanded} nodes without advancing through its blocked portal; this local approach is deferred until its region or collision admission changes.", blockingEntities, target, destination, movementFailure: region);
            }
            return false;
        }
        Ff7Actor projected = player;
        foreach (Ff7AutoplayInput action in route.Inputs)
        {
            obstacleEscape.Enqueue(new(action, projected.X4096, projected.Y4096, projected.Z4096, projected.Triangle));
            projected = Preview(projected, action).Actor;
        }
        return TakeEscapeStep(player, movement, out input);
    }

    private bool TakeEscapeStep(Ff7Actor player, Ff7NativeMovement movement, out Ff7AutoplayInput input)
    {
        input = default;
        if (!obstacleEscape.TryPeek(out EscapeStep? next))
        {
            return false;
        }
        if (!MatchesEscapeSource(player, next))
        {
            obstacleEscape.Clear();
            return false;
        }
        var direction = Directions.Single(value => value.X == next.Input.Horizontal && value.Y == next.Input.Vertical);
        byte heading = unchecked((byte)(direction.Direction + session.Field.Triggers.ControlDirection));
        Ff7NativeMoveResult preview = movement.StepDirection(player, heading, session.Host.Process.Actors, session.Host.Process.BlockedTriangles, false, session.Host.Process.LineBlocked);
        if (!preview.Moved || !AllowsPlannedMove(player, preview))
        {
            obstacleEscape.Clear();
            return false;
        }
        obstacleEscape.Dequeue();
        input = next.Input;
        return true;
    }

    internal Ff7AutoPlayPhysicalRoute ResumePhysicalSearch(PhysicalSearchCursor cursor, Ff7NativePlanningQuantum quantum)
    {
        int before = cursor.Expanded;
        Ff7AutoPlayPhysicalRoute route = cursor.Resume(quantum);
        PhysicalPlanningWork += cursor.Expanded - before;
        return route;
    }

    // A one-quantum adapter; callers needing completion retain a cursor and resume Pending work.
    internal static Ff7AutoPlayPhysicalRoute SearchPhysicalEscape(Ff7Actor player, Vector3 waypoint, Func<int, int> remaining, Func<Ff7Actor, Ff7AutoplayInput, Ff7NativeMoveResult> preview, Func<Ff7Actor, Ff7NativeMoveResult, bool> allows, int operationQuantum = 1024)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(operationQuantum, 1);
        return new PhysicalSearchCursor(player, waypoint, remaining, preview, allows).Resume(new(operationQuantum, TimeSpan.MaxValue));
    }

    // This retains the existing advisory position buckets, not an exact continuous reachability proof.
    internal sealed class PhysicalSearchCursor
    {
        private readonly Ff7Actor source;
        private readonly Vector3 waypoint;
        private readonly Func<int, int> remaining;
        private readonly Func<Ff7Actor, Ff7AutoplayInput, Ff7NativeMoveResult> preview;
        private readonly Func<Ff7Actor, Ff7NativeMoveResult, bool> allows;
        private readonly float cell;
        private readonly Dictionary<(int X, int Y, int Z, int Triangle), float> costs = new();
        private readonly PriorityQueue<SearchNode, (float Score, int Remaining)> pending = new();
        private int expanded;
        private Ff7AutoPlayPhysicalRoute? completed;
        internal int Expanded => expanded;

        internal PhysicalSearchCursor(Ff7Actor source, Vector3 waypoint, Func<int, int> remaining, Func<Ff7Actor, Ff7AutoplayInput, Ff7NativeMoveResult> preview, Func<Ff7Actor, Ff7NativeMoveResult, bool> allows)
        {
            this.source = source;
            this.waypoint = waypoint;
            this.remaining = remaining;
            this.preview = preview;
            this.allows = allows;
            cell = Math.Max(1, source.MoveSpeed * 4);
            costs[Key(source)] = 0;
            pending.Enqueue(new(source, null, default, 0), (Vector3.Distance(Position3D(source), waypoint), remaining(source.Triangle)));
        }

        private (int X, int Y, int Z, int Triangle) Key(Ff7Actor actor)
        {
            return ((int)MathF.Floor(actor.X4096 / cell), (int)MathF.Floor(actor.Y4096 / cell), (int)MathF.Floor(actor.Z4096 / cell), actor.Triangle);
        }

        internal Ff7AutoPlayPhysicalRoute Resume(Ff7NativePlanningQuantum quantum)
        {
            if (completed is not null)
            {
                return completed;
            }
            while (pending.Count > 0)
            {
                if (!quantum.TryBeginOperation())
                {
                    return new(Ff7AutoPlayPhysicalSearchStatus.Pending, [], expanded);
                }
                SearchNode current = pending.Dequeue();
                if (costs[Key(current.Actor)] < current.Cost)
                {
                    continue;
                }
                expanded++;
                foreach (var direction in Directions)
                {
                    Ff7AutoplayInput action = new(direction.X, direction.Y);
                    Ff7NativeMoveResult step = preview(current.Actor, action);
                    if (!step.Moved || !allows(current.Actor, step))
                    {
                        continue;
                    }
                    Ff7Actor actor = step.Actor;
                    float cost = current.Cost + Vector3.Distance(Position3D(current.Actor), Position3D(actor));
                    var key = Key(actor);
                    if (costs.TryGetValue(key, out float known) && known <= cost)
                    {
                        continue;
                    }
                    costs[key] = cost;
                    SearchNode next = new(actor, current, action, cost);
                    if (HasPhysicalRouteProgress(source, actor, waypoint, remaining))
                    {
                        Stack<Ff7AutoplayInput> path = new();
                        for (SearchNode? node = next; node?.Parent is not null; node = node.Parent)
                        {
                            path.Push(node.Input);
                        }
                        completed = new(Ff7AutoPlayPhysicalSearchStatus.Progress, path.ToArray(), expanded);
                        pending.Clear();
                        costs.Clear();
                        return completed;
                    }
                    pending.Enqueue(next, (cost + Vector3.Distance(Position3D(actor), waypoint), remaining(actor.Triangle)));
                }
            }
            completed = new(Ff7AutoPlayPhysicalSearchStatus.Exhausted, [], expanded);
            costs.Clear();
            return completed;
        }
    }

    internal static bool HasPhysicalRouteProgress(Ff7Actor source, Ff7Actor actor, Vector3 waypoint, Func<int, int> remaining)
    {
        int initial = remaining(source.Triangle);
        return initial > 0 ? remaining(actor.Triangle) < initial : remaining(actor.Triangle) == 0 && Vector3.DistanceSquared(Position3D(actor), waypoint) < Vector3.DistanceSquared(Position3D(source), waypoint);
    }

    internal static Ff7AutoPlayMovementFailure ReadApproachRegion(IReadOnlyList<PcWalkmeshTriangle> mesh, Ff7Actor source, int nextTriangle)
    {
        Vector3[] points = [Position3D(source), mesh[source.Triangle].A, mesh[source.Triangle].B, mesh[source.Triangle].C];
        if ((uint)nextTriangle < mesh.Count && nextTriangle != source.Triangle)
        {
            PcWalkmeshTriangle next = mesh[nextTriangle];
            points = [.. points, next.A, next.B, next.C];
        }
        return new((int)MathF.Floor(points.Min(point => point.X) * 4096), (int)MathF.Ceiling(points.Max(point => point.X) * 4096), (int)MathF.Floor(points.Min(point => point.Y) * 4096), (int)MathF.Ceiling(points.Max(point => point.Y) * 4096), (int)MathF.Floor(points.Min(point => point.Z) * 4096), (int)MathF.Ceiling(points.Max(point => point.Z) * 4096));
    }

    private static Vector3 Position3D(Ff7Actor actor) => new(actor.X4096 / 4096f, actor.Y4096 / 4096f, actor.Z4096 / 4096f);
}
