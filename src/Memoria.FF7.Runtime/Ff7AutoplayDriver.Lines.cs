using System.Numerics;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoplayDriver
{
    public Ff7AutoplayInput ApproachLine(int entity, Ff7AutoPlayTargetKind kind, bool run = true)
    {
        return ChooseBoundaryInput(() => ChooseLineApproachInput(entity, kind, run));
    }

    private Ff7AutoplayInput ChooseLineApproachInput(int entity, Ff7AutoPlayTargetKind kind, bool run)
    {
        Ff7FieldProcess process = session.Host.Process;
        Ff7Actor? player = process.Actors.SingleOrDefault(actor => actor.PlayerControlled);
        Ff7Line line = process.Lines.Last(line => line.Entity == entity);
        if (player is null || process.PlayerLocked || process.Join is not null || line.Enabled != 1)
        {
            return default;
        }
        if (kind == Ff7AutoPlayTargetKind.LineTalk)
        {
            return TalkToLine(entity, run);
        }
        if (kind == Ff7AutoPlayTargetKind.LineLeave)
        {
            return LeaveLine(entity, run);
        }
        Vector3 a = new(line.X1, line.Y1, line.Z1);
        Vector3 b = new(line.X2, line.Y2, line.Z2);
        Vector3 current = new(player.X4096 >> 12, player.Y4096 >> 12, player.Z4096 >> 12);
        var contacts = DiscoverLineContacts(session.Field.Walkmesh, process.BlockedTriangles, player, line);
        if (contacts.Count == 0)
        {
            throw new Ff7AutoPlayNavigationUnavailableException(entity, "FFVII LINE has no reachable native floor point satisfying its contact predicate.");
        }
        int distance = Ff7Session.DistanceToSegment(a, b, current, out Vector3 nearest);
        bool touching = distance >= 0 && distance < unchecked(player.CollisionRadius * player.CollisionRadius);
        if (touching && kind == Ff7AutoPlayTargetKind.LinePush)
        {
            int desired = current.X == nearest.X && current.Y == nearest.Y ? player.Direction : session.Host.Movement!.Direction((int)(nearest.X - current.X), (int)(nearest.Y - current.Y));
            var direction = Directions.MinBy(direction => Math.Abs(((direction.Direction + session.Field.Triggers.ControlDirection - desired + 128) & 255) - 128));
            return new(direction.X, direction.Y, Run: run);
        }
        if (touching && kind == Ff7AutoPlayTargetKind.LineCross)
        {
            int side = Ff7Session.Side(a, b, current);
            var opposite = contacts.Where(candidate => side == 0 ? Ff7Session.Side(a, b, candidate.Point) != 0 : (long)side * Ff7Session.Side(a, b, candidate.Point) <= 0).OrderBy(candidate => Vector3.DistanceSquared(current, candidate.Point)).ThenBy(candidate => candidate.Triangle).ToArray();
            if (opposite.Length == 0)
            {
                throw new Ff7AutoPlayNavigationUnavailableException(entity, "FFVII LINE crossing has no reachable floor contact on the opposite segment side.");
            }
            return Approach(opposite[0].Point, opposite[0].Triangle, run);
        }
        var goal = contacts.OrderBy(candidate => Vector3.DistanceSquared(current, candidate.Point)).ThenBy(candidate => candidate.Triangle).First();
        return Approach(goal.Point, goal.Triangle, run);
    }

    internal static IReadOnlyList<(Vector3 Point, int Triangle)> DiscoverLineContacts(IReadOnlyList<PcWalkmeshTriangle> mesh, byte[] blockedTriangles, Ff7Actor player, Ff7Line line)
    {
        if (line.Enabled != 1)
        {
            return [];
        }
        Vector3 a = new(line.X1, line.Y1, line.Z1);
        Vector3 b = new(line.X2, line.Y2, line.Z2);
        Vector3 center = (a + b) / 2;
        Vector2 segment = new(b.X - a.X, b.Y - a.Y);
        Vector2 normal = segment.LengthSquared() == 0 ? Vector2.UnitX : Vector2.Normalize(new(-segment.Y, segment.X));
        List<(Vector3 Point, int Triangle)> contacts = [];
        foreach (int index in ReachableTriangles(mesh, blockedTriangles, player.Triangle).Order())
        {
            PcWalkmeshTriangle triangle = mesh[index];
            Vector3 midpoint = (triangle.A + triangle.B + triangle.C) / 3;
            List<Vector3> points = [midpoint, (midpoint + triangle.A) / 2, (midpoint + triangle.B) / 2, (midpoint + triangle.C) / 2, a, b, center];
            foreach (Vector3 anchor in new[] { a, center, b })
            {
                points.Add(anchor + new Vector3(normal * (player.CollisionRadius / 2f), 0));
                points.Add(anchor - new Vector3(normal * (player.CollisionRadius / 2f), 0));
                foreach (var edge in new[] { (triangle.A, triangle.B), (triangle.B, triangle.C), (triangle.C, triangle.A) })
                {
                    Vector2 delta = new(edge.Item2.X - edge.Item1.X, edge.Item2.Y - edge.Item1.Y);
                    float ratio = delta.LengthSquared() == 0 ? 0 : Math.Clamp(Vector2.Dot(new(anchor.X - edge.Item1.X, anchor.Y - edge.Item1.Y), delta) / delta.LengthSquared(), 0, 1);
                    Vector3 nearest = Vector3.Lerp(edge.Item1, edge.Item2, ratio);
                    points.Add(nearest);
                    points.Add(Vector3.Lerp(nearest, midpoint, 0.25f));
                }
            }
            foreach (Vector3 point in points.Distinct())
            {
                if (!Contains(triangle, point, out float height))
                {
                    continue;
                }
                Vector3 floor = new(point.X, point.Y, height);
                Vector3 native = new((int)(floor.X * 4096) >> 12, (int)(floor.Y * 4096) >> 12, (int)(floor.Z * 4096) >> 12);
                int distance = Ff7Session.DistanceToSegment(a, b, native, out _);
                if (distance >= 0 && distance < unchecked(player.CollisionRadius * player.CollisionRadius))
                {
                    contacts.Add((floor, index));
                }
            }
        }
        return contacts.Distinct().ToArray();
    }

    private long avoidedLineGeneration = -1;
    private HashSet<int> avoidedLineEntities = [];

    public void AvoidLinePush(params int[] entities)
    {
        avoidedLineGeneration = session.FieldGeneration;
        avoidedLineEntities = entities.ToHashSet();
    }

    public Ff7AutoplayInput TalkToLine(int entity, bool run = true)
    {
        return ChooseBoundaryInput(() => ChooseLineTalkInput(entity, run));
    }

    private Ff7AutoplayInput ChooseLineTalkInput(int entity, bool run)
    {
        Ff7FieldProcess process = session.Host.Process;
        Ff7Actor? player = process.Actors.SingleOrDefault(actor => actor.PlayerControlled);
        Ff7Line line = process.Lines.Last(line => line.Entity == entity);
        if (player is null || process.PlayerLocked || process.Join is not null || line.Enabled != 1)
        {
            return default;
        }
        Vector3 a = new(line.X1, line.Y1, line.Z1);
        Vector3 b = new(line.X2, line.Y2, line.Z2);
        Vector3 position = new(player.X4096 >> 12, player.Y4096 >> 12, player.Z4096 >> 12);
        int distance = Ff7Session.DistanceToSegment(a, b, position, out Vector3 nearest);
        if (distance == -1 || distance >= unchecked(player.CollisionRadius * player.CollisionRadius))
        {
            return ApproachLine(entity, Ff7AutoPlayTargetKind.LineTouch, run);
        }
        int desired = line.OnLine || position.X == nearest.X && position.Y == nearest.Y ? line.ProximityAngle : session.Host.Movement!.Direction((int)(nearest.X - position.X), (int)(nearest.Y - position.Y));
        var direction = Directions.MinBy(direction => Math.Abs(((direction.Direction + session.Field.Triggers.ControlDirection - desired + 128) & 255) - 128));
        bool talk = line.OnLine && ((line.ProximityAngle - player.Direction + 32) & 255) < 64;
        return new(direction.X, direction.Y, Held: talk ? (ushort)0x20 : (ushort)0, Talk: talk, Run: run);
    }

    private bool AllowsPlannedMove(Ff7Actor from, Ff7NativeMoveResult move)
    {
        if (spatialBoundaryGeneration == session.FieldGeneration && !AllowsSpatialMove(from, move.Actor, spatialBoundaryTriangles, admittedSpatialTriangle))
        {
            return false;
        }
        if (!session.Host.Process.MapJumpDisabled && !AllowsGatewayMove(session.Field.Triggers.Gateways, from, move.Actor, admittedGateways))
        {
            return false;
        }
        if (avoidedLineGeneration != session.FieldGeneration)
        {
            return true;
        }
        Ff7Actor to = move.ProbeActor ?? move.Actor;
        Vector3 start = new(from.X4096 >> 12, from.Y4096 >> 12, from.Z4096 >> 12);
        Vector3 end = new(move.Actor.X4096 >> 12, move.Actor.Y4096 >> 12, move.Actor.Z4096 >> 12);
        int radiusSquared = from.CollisionRadius * from.CollisionRadius;
        foreach (Ff7Line line in session.Host.Process.Lines.Where(line => line.Enabled == 1 && avoidedLineEntities.Contains(line.Entity)))
        {
            Vector3 a = new(line.X1, line.Y1, line.Z1);
            Vector3 b = new(line.X2, line.Y2, line.Z2);
            int before = Ff7Session.DistanceToSegment(a, b, start, out Vector3 nearest);
            if (before >= 0 && before < radiusSquared)
            {
                bool exact = start.X == nearest.X && start.Y == nearest.Y;
                int angle = exact ? line.ProximityAngle : session.Host.Movement!.Direction((int)(nearest.X - start.X), (int)(nearest.Y - start.Y));
                if (exact || ((angle - to.Direction + 64) & 255) < 128)
                {
                    return false;
                }
            }
            int after = Ff7Session.DistanceToSegment(a, b, end, out _);
            if (after >= 0 && after < radiusSquared && (before < 0 || after < before))
            {
                return false;
            }
        }
        return true;
    }

    internal static bool AllowsGatewayMove(IReadOnlyList<PcFieldGateway> gateways, Ff7Actor from, Ff7Actor to, IReadOnlySet<int>? admitted)
    {
        return !Enumerable.Range(0, gateways.Count).Any(index => admitted?.Contains(index) != true && Ff7Session.CrossesGateway(gateways[index], from, to));
    }

    internal static IReadOnlySet<int> ReadTargetGatewayIndices(Ff7AutoPlayTarget target, IReadOnlyList<PcFieldGateway> gateways)
    {
        if (target.GatewayIndex is int index)
        {
            return new HashSet<int> { index };
        }
        return target.AnalysisComplete && target.ChangesField && target.DestinationField is int destination
            ? Enumerable.Range(0, gateways.Count).Where(index => gateways[index].DestinationField == destination).ToHashSet()
            : new HashSet<int>();
    }
}
