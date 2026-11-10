using System.Numerics;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoplayDriver
{
    public Ff7AutoplayInput LeaveLine(int entity, bool run = true)
    {
        Ff7FieldProcess process = session.Host.Process;
        Ff7Actor player = process.Actors.Single(actor => actor.PlayerControlled);
        Ff7Line line = process.Lines.Single(value => value.Entity == entity);
        Vector3 a = new(line.X1, line.Y1, line.Z1);
        Vector3 b = new(line.X2, line.Y2, line.Z2);
        Vector2 segment = new(line.X2 - line.X1, line.Y2 - line.Y1);
        Vector2 normal = segment.LengthSquared() == 0 ? Vector2.UnitX : Vector2.Normalize(new(-segment.Y, segment.X));
        Vector3 center = (a + b) / 2;
        Vector2 position = new(player.X4096 / 4096f, player.Y4096 / 4096f);
        if (Vector2.Dot(position - new Vector2(center.X, center.Y), normal) < 0)
        {
            normal = -normal;
        }
        Vector3 direct = center + new Vector3(normal * (player.CollisionRadius * 2 + 1), 0);
        IReadOnlyList<PcWalkmeshTriangle> mesh = session.Field.Walkmesh;
        HashSet<int> reachable = ReachableTriangles(player.Triangle);
        List<(int Triangle, float HeightDifference)> directTriangles = [];
        foreach (int index in reachable)
        {
            if (Contains(mesh[index], direct, out float height))
            {
                directTriangles.Add((index, Math.Abs(height - direct.Z)));
            }
        }
        bool MakesNativeProgress(Vector3 goal)
        {
            Ff7AutoplayInput proposed = Steer(goal, run);
            if (proposed.Horizontal == 0 && proposed.Vertical == 0)
            {
                return false;
            }
            var direction = Directions.Single(value => value.X == proposed.Horizontal && value.Y == proposed.Vertical);
            Ff7Actor controlled = player with { MoveSpeed = unchecked((ushort)session.GetPlayerMovementSpeed(run)) };
            Ff7NativeMoveResult probe = session.Host.Movement!.StepDirection(controlled, (byte)((direction.Direction + session.Field.Triggers.ControlDirection) & 255), process.Actors, process.BlockedTriangles, playerLocked: false, lineBlocked: process.LineBlocked);
            Vector2 after = new(probe.Actor.X4096 / 4096f, probe.Actor.Y4096 / 4096f);
            Vector2 destination = new(goal.X, goal.Y);
            return probe.Moved && Vector2.DistanceSquared(after, destination) < Vector2.DistanceSquared(position, destination);
        }
        if (directTriangles.Count > 0 && MakesNativeProgress(direct))
        {
            var target = directTriangles.OrderBy(value => value.HeightDifference).ThenBy(value => value.Triangle).First();
            return Approach(direct, target.Triangle, run);
        }
        List<(Vector3 Point, int Triangle)> candidates = [];
        foreach (int index in reachable.Order())
        {
            PcWalkmeshTriangle triangle = mesh[index];
            Vector3 midpoint = (triangle.A + triangle.B + triangle.C) / 3;
            foreach (Vector3 point in new[] { midpoint, (midpoint + triangle.A) / 2, (midpoint + triangle.B) / 2, (midpoint + triangle.C) / 2 })
            {
                int distance = Ff7Session.DistanceToSegment(a, b, point, out _);
                if (distance == -1 || distance >= player.CollisionRadius * player.CollisionRadius)
                {
                    candidates.Add((point, index));
                }
            }
        }
        if (candidates.Count == 0)
        {
            throw new NotSupportedException("FFVII LINE leave requires a reachable walkmesh point outside its native contact predicate.");
        }
        var ordered = candidates.OrderBy(value => Vector3.DistanceSquared(value.Point, center)).ThenBy(value => value.Triangle).ToArray();
        foreach (var candidate in ordered)
        {
            if (MakesNativeProgress(candidate.Point))
            {
                return Approach(candidate.Point, candidate.Triangle, run);
            }
        }
        var goal = ordered[0];
        return Approach(goal.Point, goal.Triangle, run);
    }

    private static bool Contains(PcWalkmeshTriangle triangle, Vector3 point, out float height)
    {
        static float Cross(Vector3 a, Vector3 b, Vector3 p) => (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);
        float ab = Cross(triangle.A, triangle.B, point);
        float bc = Cross(triangle.B, triangle.C, point);
        float ca = Cross(triangle.C, triangle.A, point);
        float area = Cross(triangle.A, triangle.B, triangle.C);
        height = area == 0 ? 0 : (bc * triangle.A.Z + ca * triangle.B.Z + ab * triangle.C.Z) / area;
        return area != 0 && (ab >= 0 && bc >= 0 && ca >= 0 || ab <= 0 && bc <= 0 && ca <= 0);
    }
}
