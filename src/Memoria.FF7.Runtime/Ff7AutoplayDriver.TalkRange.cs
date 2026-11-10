using System.Numerics;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoplayDriver
{
    private HashSet<int> ReachableTriangles(int start)
    {
        HashSet<int> reachable = [start];
        Queue<int> queue = new();
        queue.Enqueue(start);
        while (queue.TryDequeue(out int index))
        {
            var triangle = session.Field.Walkmesh[index];
            foreach (int adjacent in new int[] { triangle.AdjacentAB, triangle.AdjacentBC, triangle.AdjacentCA })
            {
                if (adjacent >= 0 && (session.Host.Process.BlockedTriangles[adjacent >> 3] & (1 << (adjacent & 7))) == 0 && reachable.Add(adjacent))
                {
                    queue.Enqueue(adjacent);
                }
            }
        }
        return reachable;
    }

    private Ff7AutoplayInput ApproachTalkRange(Ff7Actor target, bool run)
    {
        Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        HashSet<int> reachable = ReachableTriangles(player.Triangle);
        Vector3 targetPoint = new(target.X4096 >> 12, target.Y4096 >> 12, target.Z4096 >> 12);
        if (reachable.Contains(target.Triangle))
        {
            return Approach(targetPoint, target.Triangle, run);
        }
        List<(Vector3 Point, int Triangle)> candidates = [];
        foreach (int index in reachable.Order())
        {
            var triangle = session.Field.Walkmesh[index];
            Vector3 center = (triangle.A + triangle.B + triangle.C) / 3;
            List<Vector3> points = [center, (center + triangle.A) / 2, (center + triangle.B) / 2, (center + triangle.C) / 2];
            if (Contains(triangle, targetPoint, out float floorHeight))
            {
                points.Add(new(targetPoint.X, targetPoint.Y, floorHeight));
            }
            foreach (var edge in new[] { (triangle.A, triangle.B), (triangle.B, triangle.C), (triangle.C, triangle.A) })
            {
                Vector2 delta = new(edge.Item2.X - edge.Item1.X, edge.Item2.Y - edge.Item1.Y);
                float ratio = delta.LengthSquared() == 0 ? 0 : Math.Clamp(Vector2.Dot(new(targetPoint.X - edge.Item1.X, targetPoint.Y - edge.Item1.Y), delta) / delta.LengthSquared(), 0, 1);
                Vector3 nearest = Vector3.Lerp(edge.Item1, edge.Item2, ratio);
                points.Add(nearest);
                float length = Vector2.Distance(new(nearest.X, nearest.Y), new(center.X, center.Y));
                points.Add(Vector3.Lerp(nearest, center, length == 0 ? 1 : Math.Min(1, player.CollisionRadius / length)));
            }
            foreach (Vector3 point in points)
            {
                Ff7Actor projected = player with { X4096 = (int)(point.X * 4096), Y4096 = (int)(point.Y * 4096), Z4096 = (int)(point.Z * 4096), Triangle = index };
                int dx = (target.X4096 >> 12) - (projected.X4096 >> 12);
                int dy = (target.Y4096 >> 12) - (projected.Y4096 >> 12);
                if (dx == 0 && dy == 0)
                {
                    continue;
                }
                projected = projected with { Direction = session.Host.Movement!.Direction(dx, dy) };
                if (session.PreviewTalkTarget(projected)?.Entity == target.Entity)
                {
                    candidates.Add((point, index));
                }
            }
        }
        if (candidates.Count == 0)
        {
            throw new NotSupportedException("FFVII talk target has no reachable native floor point satisfying its original interaction predicate.");
        }
        Vector3 current = new(player.X4096 / 4096f, player.Y4096 / 4096f, player.Z4096 / 4096f);
        var goal = candidates.OrderBy(candidate => Vector3.DistanceSquared(current, candidate.Point)).ThenBy(candidate => candidate.Triangle).First();
        return Approach(goal.Point, goal.Triangle, run);
    }
}
