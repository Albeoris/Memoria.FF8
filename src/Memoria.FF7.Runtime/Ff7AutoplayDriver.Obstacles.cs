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
    internal long PhysicalPlanningWork { get; private set; }

    private sealed record EscapeStep(Ff7AutoplayInput Input, int X4096, int Y4096, int Z4096, int Triangle, int Direction);
    private sealed record SearchNode(Ff7Actor Actor, SearchNode? Parent, Ff7AutoplayInput Input, float Cost);
    internal sealed record PhysicalSearchGuide(PhysicalSearchCursor Cursor, Vector3 Waypoint, int NextTriangle, (int From, int To)[] AdviceExclusions);

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
        return player.X4096 == step.X4096 && player.Y4096 == step.Y4096 && player.Z4096 == step.Z4096 && player.Triangle == step.Triangle && player.Direction == step.Direction;
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
        if (!preparingNavigationMode)
        {
            ClearParkedNavigationModes(preserveSelection: true);
        }
        obstacleSearch = null;
        obstacleSearchContext = null;
    }

    internal void UpdatePhysicalApproachContext(string context)
    {
        if (obstacleSearchContext == context)
        {
            return;
        }
        CancelObstacleSearch();
        obstacleSearchContext = context;
    }

    private bool TryObstacleEscape(Ff7Actor player, Vector3 target, Vector3 waypoint, int destination, int nextTriangle, int[] routeDistance, bool blockedForward, IReadOnlyList<int> blockingEntities, Ff7NativeMovement movement, out Ff7AutoplayInput input)
    {
        input = default;
        if (obstacleGeneration != session.FieldGeneration)
        {
            ResetObstacleEscape();
            obstacleGeneration = session.FieldGeneration;
        }
        obstacleTarget = target;
        if (obstacleSpeed != player.MoveSpeed)
        {
            obstacleEscape.Clear();
            CancelObstacleSearch();
            obstacleSpeed = player.MoveSpeed;
        }
        string context = $"{ReadPhysicalApproachContext(player, target, destination)}|source:{player.Slot}:{player.X4096}:{player.Y4096}:{player.Z4096}:{player.Triangle}:{player.Direction}:{player.MoveSpeed}:{player.PlayerControlled}:{player.MovementFacingDisabled}|waypoint:{waypoint}:{nextTriangle}|mode:{navigationRunMode}|control:{session.Field.Triggers.ControlDirection}|input:{navigationLineInput}|talk:{navigationLineTalk}|targets:{string.Join(';', navigationLineTargets?.Select(candidate => $"{candidate.Id}:{candidate.ConditionFingerprint}") ?? [])}";
        context = ReadPhysicalFailureGuideContext(context, routeDistance, approachAdviceExclusions, navigationTalkContactGoal is null ? "route-progress:1" : navigationSelectedTarget?.Kind == Ff7AutoPlayTargetKind.Gateway ? "native-gateway-crossing:1" : navigationSelectedTarget?.Kind == Ff7AutoPlayTargetKind.ActorContact ? "native-actor-push:2" : "native-fresh-contact:2");
        if (navigationTalkContactGoal is not null)
        {
            context += "|local-portal-prefix:1";
        }
        UpdatePhysicalApproachContext(context);
        if (TakeEscapeStep(player, movement, out input))
        {
            return true;
        }
        if (!blockedForward && obstacleSearch is null && !(navigationTalkContactGoal is not null && (navigationTalkContactSearchPhase || navigationTalkContactRequiresSearch)))
        {
            Vector3 position = Position3D(player);
            obstacleHistory.Enqueue(position);
            if (obstacleHistory.Count < 24)
            {
                return false;
            }
            Vector3 previous = obstacleHistory.Dequeue();
            float step = player.MoveSpeed / 256f;
            if (Vector3.DistanceSquared(position, previous) >= step * step * 64 || navigationTalkContactContext is null && Vector3.DistanceSquared(position, target) <= step * step * 4 || session.Host.Process.LineBlocked)
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
        if (obstacleSearch is null)
        {
            if (navigationTalkContactGoal is not null)
            {
                navigationTalkContactSearchPhase = true;
            }
            if (TryReadPhysicalFailureReceipt(context, out Ff7AutoPlayPhysicalFailureReceipt? receipt))
            {
                throw new Ff7AutoPlayNavigationUnavailableException(-1, "This exact native source and local guide already exhausted its represented physical search.", blockingEntities, target, destination, ReadApproachRegion(session.Field.Walkmesh, player, nextTriangle)) { PhysicalFailure = receipt };
            }
            obstacleSearch = new(player, waypoint, triangle => routeDistance[triangle], Preview, AllowsPlannedMove, navigationTalkContactGoal, allowRouteProgress: navigationTalkContactGoal is not null);
            List<PhysicalSearchGuide> guides = [new(obstacleSearch, waypoint, nextTriangle, [])];
            if (nextTriangle >= 0 && nextTriangle != player.Triangle)
            {
                HashSet<(int From, int To)> alternatives = ReadNavigationOwnedPortals(session.Field.Walkmesh);
                alternatives.UnionWith(approachAdviceExclusions);
                List<(int From, int To)> selectedExclusions = [];
                int selected = nextTriangle;
                while (alternatives.Add((player.Triangle, selected)))
                {
                    selectedExclusions.Add((player.Triangle, selected));
                    var (previous, distance) = ReadApproachRoutes(session.Field.Walkmesh, session.Host.Process.BlockedTriangles, player.Triangle, destination, spatialBoundaryGeneration == session.FieldGeneration ? spatialBoundaryTriangles : null, admittedSpatialTriangle, alternatives);
                    selected = previous[player.Triangle];
                    if (selected < 0 || selected == player.Triangle)
                    {
                        break;
                    }
                    var triangle = session.Field.Walkmesh[selected];
                    Vector3 alternativeWaypoint = (triangle.A + triangle.B + triangle.C) / 3;
                    PhysicalSearchCursor cursor = new(player, alternativeWaypoint, triangle => distance[triangle], Preview, AllowsPlannedMove, navigationTalkContactGoal, allowRouteProgress: navigationTalkContactGoal is not null);
                    guides.Add(new(cursor, alternativeWaypoint, selected, selectedExclusions.ToArray()));
                }
            }
            obstacleSearch.ConfigureGuides(guides);
        }
        obstacleSearchContext = context;
        Ff7AutoPlayPhysicalRoute route = ResumePhysicalSearch(obstacleSearch);
        if (route.Status == Ff7AutoPlayPhysicalSearchStatus.Pending)
        {
            DeferPlanning();
            return true;
        }
        if (route.Status == Ff7AutoPlayPhysicalSearchStatus.Progress && obstacleSearch.WinningGuide is { } winner && winner.NextTriangle != nextTriangle)
        {
            approachAdviceExclusions.UnionWith(winner.AdviceExclusions);
            committedWaypoint = winner.Waypoint;
            waypointSourceTriangle = player.Triangle;
        }
        CancelObstacleSearch();
        if (route.Status != Ff7AutoPlayPhysicalSearchStatus.Progress)
        {
            if (blockedForward || navigationTalkContactGoal is not null)
            {
                Ff7AutoPlayMovementFailure region = ReadApproachRegion(session.Field.Walkmesh, player, nextTriangle);
                Ff7AutoPlayPhysicalFailureReceipt receipt = CreatePhysicalFailureReceipt(session.CaptureAutoPlayNavigation(), context, route.Expanded);
                throw new Ff7AutoPlayNavigationUnavailableException(-1, $"The current native approach search {route.Status.ToString().ToLowerInvariant()} after {route.Expanded} nodes without advancing through its blocked portal; this exact local attempt is deferred.", blockingEntities, target, destination, movementFailure: region) { PhysicalFailure = receipt };
            }
            return false;
        }
        Ff7Actor projected = player;
        foreach (Ff7AutoplayInput action in route.Inputs)
        {
            obstacleEscape.Enqueue(new(action, projected.X4096, projected.Y4096, projected.Z4096, projected.Triangle, projected.Direction));
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
        if (!HasNativePhysicalStateChange(player, preview.Actor) || !AllowsPlannedMove(player, preview))
        {
            obstacleEscape.Clear();
            return false;
        }
        obstacleEscape.Dequeue();
        input = next.Input;
        return true;
    }

    internal Ff7AutoPlayPhysicalRoute ResumePhysicalSearch(PhysicalSearchCursor cursor)
    {
        if (navigationTalkContactGoal is not null)
        {
            navigationTalkContactSearchPhase = true;
        }
        return ResumePhysicalSearch(cursor, navigationModeQuantum ?? new());
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
        private readonly Func<Ff7Actor, bool>? goal;
        private readonly bool allowRouteProgress;
        private readonly float cell;
        private readonly Dictionary<(int X, int Y, int Z, int Triangle, int Direction), float> costs = new();
        private readonly PriorityQueue<SearchNode, (float Score, int Remaining)> pending = new();
        private int expanded;
        private Ff7AutoPlayPhysicalRoute? completed;
        private GuideWorkset? guideWorkset;
        internal int Expanded => guideWorkset?.Expanded ?? expanded;
        internal bool HasPendingWork => guideWorkset?.HasPendingWork ?? (completed is null && pending.Count > 0);
        internal PhysicalSearchGuide? WinningGuide => guideWorkset?.Winner;

        private sealed class GuideWorkset(PhysicalSearchGuide[] guides)
        {
            private readonly Queue<PhysicalSearchGuide> pending = new(guides);
            private Ff7AutoPlayPhysicalRoute? completed;
            internal PhysicalSearchGuide? Winner { get; private set; }
            internal bool HasPendingWork => completed is null && pending.Count > 0;
            internal int Expanded
            {
                get
                {
                    int total = 0;
                    foreach (PhysicalSearchGuide guide in guides)
                    {
                        total = unchecked(total + guide.Cursor.expanded);
                    }
                    return total;
                }
            }

            internal Ff7AutoPlayPhysicalRoute Resume(Ff7NativePlanningQuantum quantum)
            {
                if (completed is not null)
                {
                    return completed;
                }
                while (pending.TryPeek(out PhysicalSearchGuide? guide))
                {
                    int before = quantum.Operations;
                    Ff7AutoPlayPhysicalRoute route = guide.Cursor.ResumeGraph(quantum, oneOperation: true);
                    if (route.Status == Ff7AutoPlayPhysicalSearchStatus.Pending && quantum.Operations == before)
                    {
                        return new(Ff7AutoPlayPhysicalSearchStatus.Pending, [], Expanded);
                    }
                    pending.Dequeue();
                    if (route.Status == Ff7AutoPlayPhysicalSearchStatus.Progress)
                    {
                        Winner = guide;
                        completed = route with { Expanded = Expanded };
                        return completed;
                    }
                    if (route.Status == Ff7AutoPlayPhysicalSearchStatus.Pending)
                    {
                        pending.Enqueue(guide);
                    }
                }
                completed = new(Ff7AutoPlayPhysicalSearchStatus.Exhausted, [], Expanded);
                return completed;
            }
        }

        internal void ConfigureGuides(IReadOnlyList<PhysicalSearchGuide> guides)
        {
            if (guides.Count == 0 || !ReferenceEquals(guides[0].Cursor, this))
            {
                throw new ArgumentException("A physical guide workset must begin with its original cursor.", nameof(guides));
            }
            guideWorkset = new(guides.ToArray());
        }

        internal PhysicalSearchCursor(Ff7Actor source, Vector3 waypoint, Func<int, int> remaining, Func<Ff7Actor, Ff7AutoplayInput, Ff7NativeMoveResult> preview, Func<Ff7Actor, Ff7NativeMoveResult, bool> allows, Func<Ff7Actor, bool>? goal = null, bool allowRouteProgress = false)
        {
            this.goal = goal;
            this.allowRouteProgress = allowRouteProgress;
            this.source = source;
            this.waypoint = waypoint;
            this.remaining = remaining;
            this.preview = preview;
            this.allows = allows;
            cell = Math.Max(1, source.MoveSpeed * 4);
            costs[Key(source)] = 0;
            pending.Enqueue(new(source, null, default, 0), (Vector3.Distance(Position3D(source), waypoint), remaining(source.Triangle)));
        }

        private (int X, int Y, int Z, int Triangle, int Direction) Key(Ff7Actor actor)
        {
            return ((int)MathF.Floor(actor.X4096 / cell), (int)MathF.Floor(actor.Y4096 / cell), (int)MathF.Floor(actor.Z4096 / cell), actor.Triangle, actor.Direction);
        }

        internal Ff7AutoPlayPhysicalRoute Resume(Ff7NativePlanningQuantum quantum)
        {
            return guideWorkset?.Resume(quantum) ?? ResumeGraph(quantum, oneOperation: false);
        }

        private Ff7AutoPlayPhysicalRoute ResumeGraph(Ff7NativePlanningQuantum quantum, bool oneOperation)
        {
            if (completed is not null)
            {
                return completed;
            }
            bool operated = false;
            while (pending.Count > 0)
            {
                if (operated && oneOperation || !quantum.TryBeginOperation())
                {
                    return new(Ff7AutoPlayPhysicalSearchStatus.Pending, [], expanded);
                }
                operated = true;
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
                    if (!HasNativePhysicalStateChange(current.Actor, step.Actor) || !allows(current.Actor, step))
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
                    bool routePrefix = allowRouteProgress && goal is not null && remaining(source.Triangle) > 0 && step.Moved && (source.X4096 != actor.X4096 || source.Y4096 != actor.Y4096 || source.Z4096 != actor.Z4096) && HasPhysicalRouteProgress(source, actor, waypoint, remaining);
                    if ((goal?.Invoke(actor) ?? HasPhysicalRouteProgress(source, actor, waypoint, remaining)) || routePrefix)
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

    internal static bool HasNativePhysicalStateChange(Ff7Actor source, Ff7Actor actor)
    {
        return source.X4096 != actor.X4096 || source.Y4096 != actor.Y4096 || source.Z4096 != actor.Z4096 || source.Triangle != actor.Triangle || source.Direction != actor.Direction;
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
