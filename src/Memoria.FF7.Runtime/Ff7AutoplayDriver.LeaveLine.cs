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
        HashSet<int> reachable = [player.Triangle];
        Queue<int> queue = new();
        queue.Enqueue(player.Triangle);
        while (queue.TryDequeue(out int index))
        {
            PcWalkmeshTriangle triangle = mesh[index];
            foreach (int adjacent in new int[] { triangle.AdjacentAB, triangle.AdjacentBC, triangle.AdjacentCA })
            {
                if (adjacent >= 0 && (process.BlockedTriangles[adjacent >> 3] & (1 << (adjacent & 7))) == 0 && reachable.Add(adjacent))
                {
                    queue.Enqueue(adjacent);
                }
            }
        }
        if (reachable.Any(index => Contains(mesh[index], direct)))
        {
            return Steer(direct, run);
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
        var goal = candidates.OrderBy(value => Vector3.DistanceSquared(value.Point, center)).ThenBy(value => value.Triangle).First();
        return Approach(goal.Point, goal.Triangle, run);
    }

    private static bool Contains(PcWalkmeshTriangle triangle, Vector3 point)
    {
        static float Cross(Vector3 a, Vector3 b, Vector3 p) => (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);
        float ab = Cross(triangle.A, triangle.B, point);
        float bc = Cross(triangle.B, triangle.C, point);
        float ca = Cross(triangle.C, triangle.A, point);
        return ab >= 0 && bc >= 0 && ca >= 0 || ab <= 0 && bc <= 0 && ca <= 0;
    }
}
