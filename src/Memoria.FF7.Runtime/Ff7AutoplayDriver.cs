using System.Numerics;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public readonly record struct Ff7AutoplayInput(int Horizontal = 0, int Vertical = 0, ushort Held = 0, bool Talk = false, bool Run = true);

/// <summary>Native FFVII navigation and interaction adapter. The host owns scheduling, recording and presentation.</summary>
public sealed partial class Ff7AutoplayDriver(Ff7Session session)
{
    private long waypointGeneration = -1;
    private int waypointDestination = -1;
    private Vector3? committedWaypoint;
    private int waypointSourceTriangle = -1;
    private int waypointTargetTriangle = -1;
    private Vector3? finalTarget;
    private bool finalApproach;
    private long gatewayGeneration = -1;
    private int gatewayDestination = -1;
    private Vector3? gatewayCrossingTarget;
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
        PcFieldGateway gateway = session.Field.Triggers.Gateways.Single(gateway => gateway.DestinationField == destinationField);
        if (gatewayGeneration != session.FieldGeneration || gatewayDestination != destinationField)
        {
            gatewayGeneration = session.FieldGeneration;
            gatewayDestination = destinationField;
            gatewayCrossingTarget = null;
        }
        if (gatewayCrossingTarget is Vector3 crossingTarget)
        {
            return Steer(crossingTarget, run);
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
            return Steer(gatewayCrossingTarget.Value, run);
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
        int[] previous = Enumerable.Repeat(-1, mesh.Count).ToArray();
        int[] routeDistance = Enumerable.Repeat(int.MaxValue, mesh.Count).ToArray();
        Queue<int> queue = new();
        previous[destination] = destination;
        routeDistance[destination] = 0;
        queue.Enqueue(destination);
        while (queue.TryDequeue(out int current))
        {
            PcWalkmeshTriangle triangle = mesh[current];
            foreach (int adjacent in new int[] { triangle.AdjacentAB, triangle.AdjacentBC, triangle.AdjacentCA })
            {
                if (adjacent < 0 || previous[adjacent] >= 0 || (session.Host.Process.BlockedTriangles[adjacent >> 3] & (1 << (adjacent & 7))) != 0)
                {
                    continue;
                }
                previous[adjacent] = current;
                routeDistance[adjacent] = routeDistance[current] + 1;
                queue.Enqueue(adjacent);
            }
        }
        int next = previous[player.Triangle];
        if (next < 0 && !finalApproach)
        {
            throw new NotSupportedException("FFVII navigation target has no unblocked native walkmesh path.");
        }
        Vector3 waypoint = target;
        if (next != player.Triangle && !finalApproach)
        {
            waypoint = (mesh[next].A + mesh[next].B + mesh[next].C) / 3;
        }
        Vector2 currentPosition = new(player.X4096 / 4096f, player.Y4096 / 4096f);
        if (waypointGeneration == session.FieldGeneration && waypointDestination == destination && (player.Triangle == waypointSourceTriangle || player.Triangle == waypointTargetTriangle) && committedWaypoint is Vector3 retained && Vector2.DistanceSquared(currentPosition, new(retained.X, retained.Y)) > 24 * 24)
        {
            waypoint = retained;
        }
        else
        {
            committedWaypoint = finalApproach || next == player.Triangle ? null : waypoint;
            waypointSourceTriangle = player.Triangle;
            waypointTargetTriangle = next;
            waypointGeneration = session.FieldGeneration;
            waypointDestination = destination;
        }
        Ff7Actor controlled = player with { MoveSpeed = unchecked((ushort)session.GetPlayerMovementSpeed(run)) };
        Ff7NativeMovement movement = session.Host.Movement ?? throw new NotSupportedException("FFVII navigation requires native movement.");
        float best = float.PositiveInfinity;
        int bestRouteDistance = int.MaxValue;
        Ff7AutoplayInput result = default;
        foreach (var direction in Directions)
        {
            byte nativeDirection = unchecked((byte)(direction.Direction + session.Field.Triggers.ControlDirection));
            Ff7NativeMoveResult probe = movement.StepDirection(controlled, nativeDirection, session.Host.Process.Actors, session.Host.Process.BlockedTriangles, false, session.Host.Process.LineBlocked);
            if (!AllowsPlannedMove(controlled, probe.ProbeActor ?? probe.Actor))
            {
                continue;
            }
            if (!probe.Moved && probe.Actor.Triangle == controlled.Triangle)
            {
                continue;
            }
            Vector2 position = new(probe.Actor.X4096 / 4096f, probe.Actor.Y4096 / 4096f);
            float distance = Vector2.DistanceSquared(position, new(waypoint.X, waypoint.Y));
            int remaining = finalApproach ? 0 : routeDistance[probe.Actor.Triangle];
            if (remaining < bestRouteDistance || remaining == bestRouteDistance && distance < best)
            {
                best = distance;
                bestRouteDistance = remaining;
                result = new(direction.X, direction.Y);
            }
        }
        return TryObstacleEscape(controlled, target, waypoint, movement, out Ff7AutoplayInput escape) ? escape with { Run = run } : result with { Run = run };
    }
}
