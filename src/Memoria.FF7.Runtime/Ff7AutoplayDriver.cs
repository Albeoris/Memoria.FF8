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
    private bool finalApproach;
    private long gatewayGeneration = -1;
    private PcFieldGateway? gatewayTarget;
    private Vector3? gatewayCrossingTarget;
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
            finalApproach = false;
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
        if (gatewayGeneration != session.FieldGeneration || gatewayTarget != gateway)
        {
            gatewayGeneration = session.FieldGeneration;
            gatewayTarget = gateway;
            gatewayCrossingTarget = null;
        }
        if (gatewayCrossingTarget is Vector3 crossingTarget)
        {
            return Approach(crossingTarget, run: run);
        }
        Vector3 midpoint = (gateway.A + gateway.B) / 2;
        PcWalkmeshTriangle interior = session.Field.Walkmesh.MinBy(triangle => Vector3.DistanceSquared((triangle.A + triangle.B + triangle.C) / 3, midpoint))!;
        Ff7Actor? player = session.Host.Process.Actors.SingleOrDefault(actor => actor.PlayerControlled);
        if (player is not null && player.Triangle == interior.Index && Math.Abs(player.Z4096 / 4096f - midpoint.Z) < 32 && Vector2.DistanceSquared(new(player.X4096 / 4096f, player.Y4096 / 4096f), new(midpoint.X, midpoint.Y)) < 64 * 64)
        {
            Vector2 edge = new(gateway.B.X - gateway.A.X, gateway.B.Y - gateway.A.Y);
            Vector2 normal = Vector2.Normalize(new(-edge.Y, edge.X));
            if (Vector2.Dot(new Vector2(player.X4096 / 4096f - midpoint.X, player.Y4096 / 4096f - midpoint.Y), normal) > 0)
            {
                normal = -normal;
            }
            gatewayCrossingTarget = midpoint + new Vector3(normal * 96, 0);
            return Approach(gatewayCrossingTarget.Value, interior.Index, run);
        }
        return Approach(midpoint, interior.Index, run);
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
            return new(Held: ladder.Mode switch { 0 => 0x4000, 1 => 0x1000, 2 => 0x2000, 3 => 0x8000, _ => throw new InvalidDataException("Invalid native ladder mode.") });
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
        Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        Ff7Actor target = session.Host.GetActor(entity);
        if (!target.TalkEnabled)
        {
            return default;
        }
        int dx = (target.X4096 >> 12) - (player.X4096 >> 12);
        int dy = (target.Y4096 >> 12) - (player.Y4096 >> 12);
        if (session.GetTalkTarget()?.Entity == entity)
        {
            return new(Talk: true, Run: run);
        }
        int range = target.TalkRadius + player.CollisionRadius;
        if (dx * dx + dy * dy >= range * range)
        {
            return ApproachTalkRange(target, run);
        }
        if (dx == 0 && dy == 0)
        {
            return default;
        }
        int desired = session.Host.Movement!.Direction(dx, dy);
        var direction = Directions.MinBy(direction => Math.Abs(((direction.Direction + session.Field.Triggers.ControlDirection - desired + 128) & 255) - 128));
        return new(direction.X, direction.Y, Run: run);
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
            return new(Held: ladder.Mode switch { 0 => 0x4000, 1 => 0x1000, 2 => 0x2000, 3 => 0x8000, _ => throw new InvalidDataException("Invalid native ladder mode.") });
        }
        IReadOnlyList<PcWalkmeshTriangle> mesh = session.Field.Walkmesh;
        int destination = targetTriangle ?? mesh.MinBy(triangle => Vector3.DistanceSquared((triangle.A + triangle.B + triangle.C) / 3, target))!.Index;
        if (waypointGeneration != session.FieldGeneration || waypointDestination != destination || finalTarget != target)
        {
            finalApproach = false;
            finalTarget = target;
        }
        finalApproach |= player.Triangle == destination;
        var (previous, routeDistance) = ReadApproachRoutes(mesh, session.Host.Process.BlockedTriangles, player.Triangle, destination, spatialBoundaryGeneration == session.FieldGeneration ? spatialBoundaryTriangles : null, admittedSpatialTriangle);
        int next = previous[player.Triangle];
        if (next < 0 && !finalApproach)
        {
            throw new Ff7AutoPlayNavigationUnavailableException(-1, "FFVII navigation target has no unblocked native walkmesh path.", destination: target, destinationTriangle: destination);
        }
        Vector3 waypoint = target;
        if (next != player.Triangle && !finalApproach)
        {
            waypoint = (mesh[next].A + mesh[next].B + mesh[next].C) / 3;
        }
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
                continue;
            }
            if (!probe.Moved)
            {
                if (probe.PushEntities.Length > 0 && (LocalProgress(probe.Actor) || LocalProgress(probe.ProbeActor ?? probe.Actor)))
                {
                    blockedInputs.Add((new(direction.X, direction.Y), probe.PushEntities));
                }
                continue;
            }
            advancing |= LocalProgress(probe.Actor);
            Vector2 position = new(probe.Actor.X4096 / 4096f, probe.Actor.Y4096 / 4096f);
            float distance = Vector2.DistanceSquared(position, new(waypoint.X, waypoint.Y));
            int remaining = finalApproach ? 0 : routeDistance[probe.Actor.Triangle];
            if (remaining < bestRouteDistance || remaining == bestRouteDistance && distance < best)
            {
                best = distance;
                bestRouteDistance = remaining;
                result = new(direction.X, direction.Y);
                resultAdvances = LocalProgress(probe.Actor);
            }
        }
        bool blockedForward = blockedInputs.Count > 0 && !advancing;
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
            navigationActionProgress = true;
            return escape with { Run = run };
        }
        navigationActionProgress = resultAdvances;
        return result with { Run = run };
    }

    internal static (int[] Previous, int[] Distance) ReadApproachRoutes(IReadOnlyList<PcWalkmeshTriangle> mesh, byte[] blockedTriangles, int source, int destination, IReadOnlySet<int>? spatialBoundaries = null, int? admittedSpatialTriangle = null)
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
        Queue<int> queue = new();
        previous[destination] = destination;
        distance[destination] = 0;
        if (!IsRestricted(destination))
        {
            queue.Enqueue(destination);
        }
        while (queue.TryDequeue(out int current))
        {
            PcWalkmeshTriangle triangle = mesh[current];
            foreach (int adjacent in new int[] { triangle.AdjacentAB, triangle.AdjacentBC, triangle.AdjacentCA })
            {
                if (adjacent < 0 || previous[adjacent] >= 0 || adjacent != source && IsRestricted(adjacent))
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
