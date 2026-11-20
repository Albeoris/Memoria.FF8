using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public readonly record struct Ff7AutoplayInput(int Horizontal = 0, int Vertical = 0, ushort Held = 0, bool Talk = false, bool Run = true);

/// <summary>Native FFVII navigation and interaction adapter. The host owns scheduling, recording and presentation.</summary>
public sealed partial class Ff7AutoplayDriver(Ff7Session session)
{
    public string ReadPhysicalAvailabilityFingerprint(Ff7AutoPlayTarget target, IReadOnlyList<int>? blockingEntities = null, int? sourceTriangle = null, IReadOnlySet<int>? sourceBoundaries = null)
    {
        Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        if (sourceTriangle is int triangle)
        {
            player = player with { Triangle = triangle };
        }
        if (target.Kind is Ff7AutoPlayTargetKind.ActorTalk or Ff7AutoPlayTargetKind.ActorContact)
        {
            string actor = ReadPhysicalAvailabilityFingerprint(session.Machine.ProgramIdentity, session.Field.Walkmesh, session.Host.Process.BlockedTriangles, player, session.Host.GetActor(target.Entity), blockingEntities, session.Host.Process.Actors, session.Host.Process.MapJumpDisabled, session.Field.Triggers.Gateways);
            return WithSpatialBoundaryFingerprint(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{actor}|target-boundary:{target.AnalysisComplete}:{target.ChangesField}:{target.DestinationField}:{target.GatewayIndex}"))), sourceBoundaries);
        }
        return WithSpatialBoundaryFingerprint(ReadPhysicalAvailabilityFingerprint(session.Machine.ProgramIdentity, session.Field.Walkmesh, session.Host.Process.BlockedTriangles, player, target, session.Host.Process.Lines, session.Field.Triggers.Gateways, session.Host.Process.MapJumpDisabled, blockingEntities, session.Host.Process.Actors), sourceBoundaries);
    }

    internal static string ReadPhysicalAvailabilityFingerprint(string programIdentity, IReadOnlyList<PcWalkmeshTriangle> walkmesh, byte[] blockedTriangles, Ff7Actor player, Ff7AutoPlayTarget target, IReadOnlyList<Ff7Line> lines, IReadOnlyList<PcFieldGateway> gateways, bool mapJumpDisabled = false, IReadOnlyList<int>? blockingEntities = null, IReadOnlyList<Ff7Actor>? actors = null)
    {
        int component = ReachableTriangles(walkmesh, blockedTriangles, player.Triangle).Min();
        StringBuilder facts = new($"{programIdentity}|component:{component}|blocked:{Convert.ToHexString(blockedTriangles)}|player:{player.Entity}:{player.Model}:{player.CollisionRadius}|target:{target.Id}:{target.Kind}|contact-search:1");
        facts.Append($"|target-boundary:{target.AnalysisComplete}:{target.ChangesField}:{target.DestinationField}:{target.GatewayIndex}");
        AppendGatewayAvailability(facts, mapJumpDisabled, gateways);
        AppendBlockingAvailability(facts, blockingEntities, actors);
        if (target.Kind == Ff7AutoPlayTargetKind.Gateway)
        {
            if (target.GatewayIndex is int index && (uint)index < gateways.Count)
            {
                PcFieldGateway gateway = gateways[index];
                facts.Append($"|gateway:{index}:{BitConverter.SingleToInt32Bits(gateway.A.X)}:{BitConverter.SingleToInt32Bits(gateway.A.Y)}:{BitConverter.SingleToInt32Bits(gateway.A.Z)}:{BitConverter.SingleToInt32Bits(gateway.B.X)}:{BitConverter.SingleToInt32Bits(gateway.B.Y)}:{BitConverter.SingleToInt32Bits(gateway.B.Z)}:{gateway.DestinationField}:{gateway.DestinationTriangle}:{gateway.DestinationX}:{gateway.DestinationY}:{gateway.Direction}");
            }
            else
            {
                facts.Append("|gateway:absent");
            }
        }
        else if (target.Kind == Ff7AutoPlayTargetKind.AutonomousSpatial)
        {
            facts.Append($"|spatial:{target.Triangle}:{BitConverter.SingleToInt32Bits(target.Position.X)}:{BitConverter.SingleToInt32Bits(target.Position.Y)}:{BitConverter.SingleToInt32Bits(target.Position.Z)}");
        }
        else
        {
            foreach (Ff7Line line in lines.Where(line => line.Entity == target.Entity).OrderBy(line => line.Slot))
            {
                facts.Append($"|line:{line.Slot}:{line.Entity}:{line.X1}:{line.Y1}:{line.Z1}:{line.X2}:{line.Y2}:{line.Z2}:{line.Enabled}:{line.SlipDisabled}");
            }
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(facts.ToString())));
    }

    private static void AppendGatewayAvailability(StringBuilder facts, bool mapJumpDisabled, IReadOnlyList<PcFieldGateway>? gateways)
    {
        facts.Append($"|map-jump-disabled:{mapJumpDisabled}");
        for (int index = 0; index < (gateways?.Count ?? 0); index++)
        {
            PcFieldGateway gateway = gateways![index];
            facts.Append($"|native-gateway:{index}:{BitConverter.SingleToInt32Bits(gateway.A.X)}:{BitConverter.SingleToInt32Bits(gateway.A.Y)}:{BitConverter.SingleToInt32Bits(gateway.A.Z)}:{BitConverter.SingleToInt32Bits(gateway.B.X)}:{BitConverter.SingleToInt32Bits(gateway.B.Y)}:{BitConverter.SingleToInt32Bits(gateway.B.Z)}:{gateway.DestinationField}:{gateway.DestinationTriangle}:{gateway.DestinationX}:{gateway.DestinationY}:{gateway.Direction}");
        }
    }

    private static void AppendBlockingAvailability(StringBuilder facts, IReadOnlyList<int>? blockingEntities, IReadOnlyList<Ff7Actor>? actors)
    {
        foreach (int entity in (blockingEntities ?? []).Distinct().Order())
        {
            Ff7Actor[] blockers = actors?.Where(actor => actor.Entity == entity).OrderBy(actor => actor.Slot).ToArray() ?? [];
            if (blockers.Length == 0)
            {
                facts.Append($"|blocker:{entity}:absent");
            }
            foreach (Ff7Actor blocker in blockers)
            {
                facts.Append($"|blocker:{entity}:{blocker.Slot}:{blocker.Model}:{blocker.EntityBound}:{blocker.X4096}:{blocker.Y4096}:{blocker.Z4096}:{blocker.Triangle}:{blocker.TalkEnabled}:{blocker.TalkRadius}:{blocker.CollisionEnabled}:{blocker.CollisionRadius}");
            }
        }
    }

    private long waypointGeneration = -1;
    private int waypointDestination = -1;
    private Vector3? committedWaypoint;
    private int waypointSourceTriangle = -1;
    private Vector3? finalTarget;
    private IReadOnlySet<int>? admittedGateways;
    private static readonly (int X, int Y, int Direction)[] Directions = [(-1, -1, 32), (0, -1, 0), (1, -1, 224), (-1, 0, 64), (1, 0, 192), (-1, 1, 96), (0, 1, 128), (1, 1, 160)];

    public void Apply(Ff7AutoplayInput input)
    {
        if (session.World is { } world)
        {
            ushort previousHeld = world.Process.Input?.Held ?? 0;
            session.SetInput(input.Held, (ushort)(input.Held & ~previousHeld));
            session.MovePlayer(input.Horizontal, input.Vertical, input.Run);
            return;
        }
        if (session.Host.Process.Transition is not null)
        {
            return;
        }
        Ff7Actor? player = session.Host.Process.Actors.SingleOrDefault(actor => actor.PlayerControlled);
        if (session.Host.Process.PlayerLocked || session.Host.Process.Join is not null || player?.Ladder is not null || player?.Jump is not null || player is not null && session.Host.HasScriptedMotion(player))
        {
            committedWaypoint = null;
            waypointGeneration = -1;
            ResetObstacleEscape();
        }
        ushort held = input.Held;
        ushort pressed = (ushort)(held & ~session.Machine.Input.Held);
        session.SetInput(held, pressed);
        session.MovePlayer(input.Horizontal, input.Vertical, input.Run);
        if (input.Talk)
        {
            session.TryTalk();
        }
    }

    public Ff7AutoplayInput Gateway(int destinationField, bool run = true)
    {
        return Gateway(session.Field.Triggers.Gateways.Single(gateway => gateway.DestinationField == destinationField), run);
    }

    public Ff7AutoplayInput Gateway(int destinationField, int destinationTriangle, bool run = true)
    {
        return Gateway(session.Field.Triggers.Gateways.Single(gateway => gateway.DestinationField == destinationField && gateway.DestinationTriangle == destinationTriangle), run);
    }

    public Ff7AutoplayInput GatewayAt(int index, bool run = true)
    {
        if ((uint)index >= session.Field.Triggers.Gateways.Count || session.Field.Triggers.Gateways[index].DestinationField == short.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "The gateway index must identify an active current field gateway.");
        }
        return Gateway(session.Field.Triggers.Gateways[index], run);
    }

    internal Ff7AutoplayInput Gateway(PcFieldGateway gateway, bool run)
    {
        int index = Enumerable.Range(0, session.Field.Triggers.Gateways.Count).Single(index => ReferenceEquals(session.Field.Triggers.Gateways[index], gateway));
        return ChooseBoundaryInput(() => ApproachGateway(gateway, run), new HashSet<int> { index });
    }

    private Ff7AutoplayInput ApproachGateway(PcFieldGateway gateway, bool run)
    {
        Ff7Actor? player = session.Host.Process.Actors.SingleOrDefault(actor => actor.PlayerControlled);
        if (player is null || session.Host.Process.PlayerLocked || session.Host.Process.Join is not null)
        {
            return default;
        }
        Ff7Actor controlled = player with { MoveSpeed = unchecked((ushort)session.GetPlayerMovementSpeed(run)) };
        foreach (var direction in Directions)
        {
            Ff7NativeMoveResult step = session.Host.Movement!.StepDirection(controlled, unchecked((byte)(direction.Direction + session.Field.Triggers.ControlDirection)), session.Host.Process.Actors, session.Host.Process.BlockedTriangles, false, session.Host.Process.LineBlocked);
            if (step.Moved && Ff7Session.CrossesGateway(gateway, controlled, step.Actor) && AllowsPlannedMove(controlled, step))
            {
                return new(direction.X, direction.Y, Run: run);
            }
        }
        Vector3 position = Position3D(player);
        int distance = Ff7Session.DistanceToSegment(gateway.A, gateway.B, new(player.X4096 >> 12, player.Y4096 >> 12, player.Z4096 >> 12), out Vector3 nearest);
        if (distance >= 0 && distance < unchecked(player.CollisionRadius * player.CollisionRadius))
        {
            return Approach(ReadGatewayCrossingTarget(gateway, position, nearest, player.CollisionRadius), player.Triangle, run);
        }
        var contacts = DiscoverGatewayContacts(session.Field.Walkmesh, session.Host.Process.BlockedTriangles, player, gateway);
        if (contacts.Count == 0)
        {
            throw new Ff7AutoPlayNavigationUnavailableException(-1, "FFVII gateway has no reachable native floor contact.");
        }
        var selected = ChooseFloorContact(contacts, player);
        return Approach(selected.Point, selected.Triangle, run);
    }

    internal static Vector3 ReadGatewayCrossingTarget(PcFieldGateway gateway, Vector3 position, Vector3 nearest, int collisionRadius)
    {
        Vector2 edge = new(gateway.B.X - gateway.A.X, gateway.B.Y - gateway.A.Y);
        if (edge.LengthSquared() == 0)
        {
            throw new NotSupportedException("A zero-length native gateway cannot supply a crossing direction.");
        }
        Vector2 normal = Vector2.Normalize(new(-edge.Y, edge.X));
        if (Vector2.Dot(new(position.X - nearest.X, position.Y - nearest.Y), normal) > 0)
        {
            normal = -normal;
        }
        return nearest + new Vector3(normal * collisionRadius, 0);
    }

    internal static IReadOnlyList<(Vector3 Point, int Triangle)> DiscoverGatewayContacts(IReadOnlyList<PcWalkmeshTriangle> walkmesh, byte[] blockedTriangles, Ff7Actor player, PcFieldGateway gateway)
    {
        return gateway.DestinationField == short.MaxValue ? [] : DiscoverFloorSegmentContacts(walkmesh, blockedTriangles, player, gateway.A, gateway.B);
    }

    internal static bool HasGatewayTopologyPath(IReadOnlyList<PcWalkmeshTriangle> walkmesh, byte[] blockedTriangles, Ff7Actor player, PcFieldGateway gateway, out int targetTriangle)
    {
        var contacts = DiscoverGatewayContacts(walkmesh, blockedTriangles, player, gateway);
        targetTriangle = contacts.Count == 0 ? -1 : contacts[0].Triangle;
        return contacts.Count > 0;
    }
    public Ff7AutoplayInput Steer(Vector3 target, bool run = true)
    {
        Ff7Actor? player = session.Host.Process.Actors.SingleOrDefault(actor => actor.PlayerControlled);
        if (player is null)
        {
            return default;
        }
        if (player.Ladder is { Completed: false } ladder)
        {
            return new(Held: ForwardLadderKey(ladder.Mode));
        }
        int dx = (int)target.X - (player.X4096 >> 12);
        int dy = (int)target.Y - (player.Y4096 >> 12);
        if (dx == 0 && dy == 0)
        {
            return default;
        }
        int desired = session.Host.Movement!.Direction(dx, dy);
        var direction = Directions.MinBy(direction => Math.Abs(((direction.Direction + session.Field.Triggers.ControlDirection - desired + 128) & 255) - 128));
        return new(direction.X, direction.Y, Run: run);
    }

    public Ff7AutoplayInput TalkTo(int entity, bool run = true)
    {
        return ChooseBoundaryInput(() => ChooseTalkInput(entity, run));
    }

    private Ff7AutoplayInput ChooseTalkInput(int entity, bool run)
    {
        Ff7Actor target = session.Host.GetActor(entity);
        if (!target.TalkEnabled)
        {
            return default;
        }
        return ChooseNativeTalkContactInput(target, run) ?? ApproachTalkRange(target, run);
    }

    public Ff7AutoplayInput Approach(Vector3 target, int? targetTriangle = null, bool run = true)
    {
        Ff7Actor? player = session.Host.Process.Actors.SingleOrDefault(actor => actor.PlayerControlled);
        if (player is null || session.Host.Process.PlayerLocked || session.Host.Process.Join is not null)
        {
            return default;
        }
        if (player.Ladder is { Completed: false } ladder)
        {
            return new(Held: ForwardLadderKey(ladder.Mode));
        }
        IReadOnlyList<PcWalkmeshTriangle> mesh = session.Field.Walkmesh;
        int destination = targetTriangle ?? mesh.MinBy(triangle => Vector3.DistanceSquared((triangle.A + triangle.B + triangle.C) / 3, target))!.Index;
        UpdateNavigationModeGoal($"{session.FieldGeneration}:{navigationSelectedTarget?.Id}:{navigationTalkContactContext ?? navigationSelectedTarget?.ConditionFingerprint}:{target}:{destination}");
        finalTarget = target;
        string guideContext = ReadApproachGuideContext(player, target, destination);
        UpdateApproachGuideContext(guideContext);
        HashSet<(int From, int To)> adviceRestrictions = ReadNavigationOwnedPortals(mesh);
        var (previous, routeDistance, discardedAdvice) = ReadAdvisedApproachRoutes(mesh, session.Host.Process.BlockedTriangles, player.Triangle, destination, spatialBoundaryGeneration == session.FieldGeneration ? spatialBoundaryTriangles : null, admittedSpatialTriangle, adviceRestrictions, approachAdviceExclusions);
        if (discardedAdvice)
        {
            approachAdviceExclusions.Clear();
            obstacleEscape.Clear();
            committedWaypoint = null;
            CancelObstacleSearch();
        }
        int next = previous[player.Triangle];
        var (finalApproach, waypoint) = ReadPortalApproachStep(mesh, player.Triangle, destination, previous, Position3D(player), target, player.CollisionRadius);
        Vector2 currentPosition = new(player.X4096 / 4096f, player.Y4096 / 4096f);
        if (waypointGeneration == session.FieldGeneration && waypointDestination == destination && player.Triangle == waypointSourceTriangle && committedWaypoint is Vector3 retained && Vector2.DistanceSquared(currentPosition, new(retained.X, retained.Y)) > 24 * 24)
        {
            waypoint = retained;
        }
        else
        {
            committedWaypoint = finalApproach || next == player.Triangle ? null : waypoint;
            waypointSourceTriangle = player.Triangle;
            waypointGeneration = session.FieldGeneration;
            waypointDestination = destination;
        }
        Ff7Actor controlled = player with { MoveSpeed = unchecked((ushort)session.GetPlayerMovementSpeed(run)) };
        Ff7NativeMovement movement = session.Host.Movement ?? throw new NotSupportedException("FFVII navigation requires native movement.");
        float best = float.PositiveInfinity;
        int bestRouteDistance = int.MaxValue;
        Ff7AutoplayInput result = default;
        bool resultAdvances = false;
        List<(Ff7AutoplayInput Input, int[] Entities)> blockedInputs = [];
        bool advancing = false;
        bool admissionBlockedForward = false;
        bool LocalProgress(Ff7Actor actor)
        {
            return HasPhysicalRouteProgress(controlled, actor, waypoint, triangle => routeDistance[triangle]) || routeDistance[actor.Triangle] == routeDistance[controlled.Triangle] && Vector3.DistanceSquared(Position3D(actor), waypoint) < Vector3.DistanceSquared(Position3D(controlled), waypoint);
        }
        foreach (var direction in Directions)
        {
            byte nativeDirection = unchecked((byte)(direction.Direction + session.Field.Triggers.ControlDirection));
            Ff7NativeMoveResult probe = movement.StepDirection(controlled, nativeDirection, session.Host.Process.Actors, session.Host.Process.BlockedTriangles, false, session.Host.Process.LineBlocked);
            if (!AllowsPlannedMove(controlled, probe))
            {
                admissionBlockedForward |= probe.Moved && LocalProgress(probe.Actor);
                continue;
            }
            if (!probe.Moved)
            {
                if (probe.PushEntities.Length > 0 && (LocalProgress(probe.Actor) || LocalProgress(probe.ProbeActor ?? probe.Actor)))
                {
                    blockedInputs.Add((new(direction.X, direction.Y), probe.PushEntities));
                }
                if (!HasNativePhysicalStateChange(controlled, probe.Actor))
                {
                    continue;
                }
            }
            advancing |= LocalProgress(probe.Actor);
            Vector2 position = new(probe.Actor.X4096 / 4096f, probe.Actor.Y4096 / 4096f);
            float distance = Vector2.DistanceSquared(position, new(waypoint.X, waypoint.Y));
            int remaining = finalApproach && targetTriangle is null ? 0 : routeDistance[probe.Actor.Triangle];
            if (remaining < bestRouteDistance || remaining == bestRouteDistance && distance < best)
            {
                best = distance;
                bestRouteDistance = remaining;
                result = new(direction.X, direction.Y);
                resultAdvances = LocalProgress(probe.Actor);
            }
        }
        bool blockedForward = (blockedInputs.Count > 0 || admissionBlockedForward) && !advancing;
        HashSet<int> blockers = [];
        if (blockedForward)
        {
            foreach (var blocked in blockedInputs)
            {
                Ff7Actor[] without = session.Host.Process.Actors.Where(actor => !blocked.Entities.Contains(actor.Entity)).ToArray();
                var direction = Directions.Single(value => value.X == blocked.Input.Horizontal && value.Y == blocked.Input.Vertical);
                Ff7NativeMoveResult counterfactual = movement.StepDirection(controlled, unchecked((byte)(direction.Direction + session.Field.Triggers.ControlDirection)), without, session.Host.Process.BlockedTriangles, false, session.Host.Process.LineBlocked);
                if (counterfactual.Moved && AllowsPlannedMove(controlled, counterfactual) && LocalProgress(counterfactual.Actor))
                {
                    blockers.UnionWith(blocked.Entities);
                }
            }
        }
        if (TryObstacleEscape(controlled, target, waypoint, destination, next, routeDistance, blockedForward, blockers.ToArray(), movement, out Ff7AutoplayInput escape))
        {
            if (PlanningPending)
            {
                return default;
            }
            navigationActionProgress = true;
            navigationEscapeAction = true;
            return escape with { Run = run };
        }
        navigationActionProgress = resultAdvances;
        return result with { Run = run };
    }

    private string? approachGuideContext;
    private readonly HashSet<(int From, int To)> approachAdviceExclusions = [];

    private string ReadApproachGuideContext(Ff7Actor player, Vector3 target, int destination)
    {
        return ReadApproachAdvisoryContext(session.FieldGeneration, target, destination, navigationSelectedTarget, player, session.GetPlayerMovementSpeed(navigationRunMode ?? true), navigationRunMode, session.Host.Process.MapJumpDisabled, session.Host.Process.BlockedTriangles, CaptureSpatialBoundaryTriangles(), admittedSpatialTriangle, admittedGateways ?? new HashSet<int>(), ReadNavigationOwnedPortals(session.Field.Walkmesh), session.Host.Process.Actors, session.Host.Process.Lines, navigationTalkContactContext);
    }

    internal void UpdateApproachGuideContext(string context)
    {
        if (approachGuideContext == context)
        {
            return;
        }
        approachGuideContext = context;
        approachAdviceExclusions.Clear();
        committedWaypoint = null;
        ResetObstacleEscape();
    }

    internal static string ReadApproachAdvisoryContext(long generation, Vector3 point, int destination, Ff7AutoPlayTarget? selected, Ff7Actor player, int speed, bool? run, bool mapJumpDisabled, byte[] blocked, IEnumerable<int> boundaries, int? admittedTriangle, IEnumerable<int> gateways, IEnumerable<(int From, int To)> ownedPortals, IReadOnlyList<Ff7Actor> actors, IReadOnlyList<Ff7Line> lines, string? stableContactContext = null)
    {
        string target = selected is null ? $"point:{point}" : $"event:{selected.Id}:{selected.Entity}:{selected.Script}:{selected.Kind}:{stableContactContext ?? selected.ConditionFingerprint}:{selected.EntryInputContract}:{selected.DestinationField}:{selected.GatewayIndex}";
        StringBuilder facts = new($"{generation}:{target}:{destination}:{player.Slot}:{player.Entity}:{player.Model}:{player.CollisionRadius}:{player.MovementFacingDisabled}:{speed}:{run}:{mapJumpDisabled}:{admittedTriangle}:{Convert.ToHexString(blocked)}");
        foreach (var edge in ownedPortals.OrderBy(edge => edge.From).ThenBy(edge => edge.To))
        {
            facts.Append($"|owned:{edge.From}:{edge.To}");
        }
        foreach (int triangle in boundaries.Order())
        {
            facts.Append($"|boundary:{triangle}");
        }
        foreach (Ff7Actor actor in actors.Where(actor => !actor.PlayerControlled && actor.CollisionEnabled).OrderBy(actor => actor.Slot))
        {
            facts.Append($"|actor:{actor.Slot}:{actor.Entity}:{actor.Model}:{actor.CollisionRadius}");
        }
        foreach (var line in lines.OrderBy(line => line.Slot))
        {
            facts.Append($"|line:{line.Slot}:{line.Entity}:{line.Enabled}:{line.SlipDisabled}");
        }
        facts.Append($"|gateways:{string.Join(',', gateways.Order())}");
        return facts.ToString();
    }

    private string ReadPhysicalApproachContext(Ff7Actor player, Vector3 target, int destination)
    {
        return ReadPhysicalGeometryContext(ReadApproachGuideContext(player, target, destination) + "|native-condition:" + navigationSelectedTarget?.ConditionFingerprint, session.Host.Process.Actors, session.Host.Process.Lines, session.Host.Process.LineBlocked);
    }

    internal static string ReadPhysicalGeometryContext(string advisory, IReadOnlyList<Ff7Actor> actors, IReadOnlyList<Ff7Line> lines, bool lineBlocked)
    {
        StringBuilder facts = new($"{advisory}|line-blocked:{lineBlocked}");
        foreach (Ff7Actor actor in actors.Where(actor => actor.CollisionEnabled).OrderBy(actor => actor.Slot))
        {
            facts.Append($"|pose:{actor.Slot}:{actor.X4096}:{actor.Y4096}:{actor.Z4096}:{actor.Triangle}:{actor.Direction}");
        }
        foreach (Ff7Line line in lines.OrderBy(line => line.Slot))
        {
            facts.Append($"|geometry:{line.Slot}:{line.X1}:{line.Y1}:{line.Z1}:{line.X2}:{line.Y2}:{line.Z2}:{line.Touch}:{line.Enter}:{line.Leave}:{line.Across}:{line.Push}:{line.Talk}:{line.OnLine}:{line.ProximityAngle}");
        }
        return facts.ToString();
    }

    internal static (int[] Previous, int[] Distance, bool DiscardedAdvice) ReadAdvisedApproachRoutes(IReadOnlyList<PcWalkmeshTriangle> mesh, byte[] blocked, int source, int destination, IReadOnlySet<int>? boundaries, int? admittedTriangle, IReadOnlySet<(int From, int To)> nativePortals, IReadOnlySet<(int From, int To)> advice)
    {
        HashSet<(int From, int To)> combined = new(nativePortals);
        combined.UnionWith(advice);
        var advised = ReadApproachRoutes(mesh, blocked, source, destination, boundaries, admittedTriangle, combined);
        if (advised.Previous[source] >= 0 || advice.Count == 0)
        {
            return (advised.Previous, advised.Distance, false);
        }
        var native = ReadApproachRoutes(mesh, blocked, source, destination, boundaries, admittedTriangle, nativePortals);
        return native.Previous[source] >= 0 ? (native.Previous, native.Distance, true) : (advised.Previous, advised.Distance, false);
    }

    internal static (int[] Previous, int[] Distance) ReadApproachRoutes(IReadOnlyList<PcWalkmeshTriangle> mesh, byte[] blockedTriangles, int source, int destination, IReadOnlySet<int>? spatialBoundaries = null, int? admittedSpatialTriangle = null, IReadOnlySet<(int From, int To)>? ownedPortals = null)
    {
        int[] previous = Enumerable.Repeat(-1, mesh.Count).ToArray();
        int[] distance = Enumerable.Repeat(int.MaxValue, mesh.Count).ToArray();
        bool IsRestricted(int triangle)
        {
            return (blockedTriangles[triangle >> 3] & (1 << (triangle & 7))) != 0 || spatialBoundaries?.Contains(triangle) == true && triangle != admittedSpatialTriangle;
        }
        if (destination != source && IsRestricted(destination))
        {
            return (previous, distance);
        }
        List<int>[] incoming = Enumerable.Range(0, mesh.Count).Select(_ => new List<int>()).ToArray();
        foreach (PcWalkmeshTriangle triangle in mesh)
        {
            foreach (int adjacent in new[] { triangle.AdjacentAB, triangle.AdjacentBC, triangle.AdjacentCA })
            {
                if (adjacent >= 0)
                {
                    incoming[adjacent].Add(triangle.Index);
                }
            }
        }
        Queue<int> queue = new();
        previous[destination] = destination;
        distance[destination] = 0;
        if (!IsRestricted(destination))
        {
            queue.Enqueue(destination);
        }
        while (queue.TryDequeue(out int current))
        {
            foreach (int adjacent in incoming[current])
            {
                if (adjacent < 0 || previous[adjacent] >= 0 || adjacent != source && IsRestricted(adjacent) || ownedPortals?.Contains((adjacent, current)) == true)
                {
                    continue;
                }
                previous[adjacent] = current;
                distance[adjacent] = distance[current] + 1;
                if (!IsRestricted(adjacent))
                {
                    queue.Enqueue(adjacent);
                }
            }
        }
        return (previous, distance);
    }
}
