using System.Numerics;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoplayDriver
{
    internal static IReadOnlyList<Vector3> ReadTalkRegionPoints(IReadOnlyList<PcWalkmeshTriangle> mesh, IReadOnlySet<int> reachable, int triangleIndex, Vector3 target, int range, int radius, IReadOnlyList<Vector2> footprint)
    {
        PcWalkmeshTriangle triangle = mesh[triangleIndex];
        Vector3[] vertices = [triangle.A, triangle.B, triangle.C];
        if (range <= 0 || vertices.Max(point => point.Z) < target.Z - 255 || vertices.Min(point => point.Z) >= target.Z + 256 || vertices.Max(point => point.X) < target.X - range || vertices.Min(point => point.X) > target.X + range || vertices.Max(point => point.Y) < target.Y - range || vertices.Min(point => point.Y) > target.Y + range)
        {
            return [];
        }
        List<(Vector2 A, Vector2 B)> lines = [];
        HashSet<(int X, int Y)> points = [];
        Vector2 center = new(target.X, target.Y);
        void Add(Vector2 point)
        {
            if (!float.IsFinite(point.X) || !float.IsFinite(point.Y) || Vector2.DistanceSquared(point, center) > (range + radius) * (float)(range + radius))
            {
                return;
            }
            points.Add(((int)MathF.Round(point.X * 4096), (int)MathF.Round(point.Y * 4096)));
        }
        void AddLine(Vector2 a, Vector2 b)
        {
            if (a != b)
            {
                lines.Add((a, b));
                Add(a);
                Add(b);
                Vector2 edge = b - a;
                Add(a + edge * (Vector2.Dot(center - a, edge) / edge.LengthSquared()));
            }
        }
        foreach (int index in reachable.Order())
        {
            PcWalkmeshTriangle current = mesh[index];
            foreach (var edge in new[] { (current.A, current.B, current.AdjacentAB), (current.B, current.C, current.AdjacentBC), (current.C, current.A, current.AdjacentCA) })
            {
                Vector2 a = new(edge.Item1.X, edge.Item1.Y);
                Vector2 b = new(edge.Item2.X, edge.Item2.Y);
                Vector2 delta = b - a;
                float ratio = delta.LengthSquared() == 0 ? 0 : Math.Clamp(Vector2.Dot(center - a, delta) / delta.LengthSquared(), 0, 1);
                if (Vector2.DistanceSquared(center, a + delta * ratio) > (range + radius) * (float)(range + radius))
                {
                    continue;
                }
                if (index == triangleIndex)
                {
                    AddLine(a, b);
                }
                if (edge.Item3 < 0 || !reachable.Contains(edge.Item3))
                {
                    foreach (Vector2 offset in footprint.Distinct())
                    {
                        AddLine(a - offset, b - offset);
                    }
                }
            }
        }
        // The floor-height slab and triangle borders are additional boundaries of the talk region.
        foreach (float height in new[] { target.Z - 255, target.Z + 255 })
        {
            List<Vector2> crossings = [];
            foreach (var edge in new[] { (triangle.A, triangle.B), (triangle.B, triangle.C), (triangle.C, triangle.A) })
            {
                if (edge.Item1.Z != edge.Item2.Z)
                {
                    float ratio = (height - edge.Item1.Z) / (edge.Item2.Z - edge.Item1.Z);
                    if (ratio >= 0 && ratio <= 1)
                    {
                        Vector3 point = Vector3.Lerp(edge.Item1, edge.Item2, ratio);
                        crossings.Add(new(point.X, point.Y));
                    }
                }
            }
            if (crossings.Count >= 2)
            {
                AddLine(crossings[0], crossings[1]);
            }
        }
        Add(center);
        Add(new((triangle.A.X + triangle.B.X + triangle.C.X) / 3, (triangle.A.Y + triangle.B.Y + triangle.C.Y) / 3));
        static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;
        for (int i = 0; i < lines.Count; i++)
        {
            var (a, b) = lines[i];
            Vector2 delta = b - a;
            Vector2 nearest = a + delta * (Vector2.Dot(center - a, delta) / delta.LengthSquared());
            float squared = range * (float)range - Vector2.DistanceSquared(nearest, center);
            if (squared >= 0)
            {
                Vector2 distance = Vector2.Normalize(delta) * MathF.Sqrt(squared);
                Add(nearest + distance);
                Add(nearest - distance);
            }
            for (int j = 0; j < i; j++)
            {
                Vector2 other = lines[j].B - lines[j].A;
                float divisor = Cross(delta, other);
                if (MathF.Abs(divisor) > 0.000001f)
                {
                    Add(a + delta * (Cross(lines[j].A - a, other) / divisor));
                }
            }
        }
        // Native talk truncates coordinates before its integer distance test. Derive the circle's
        // integer interval at each arrangement coordinate instead of sampling a dense radius grid.
        foreach (var point in points.ToArray())
        {
            float x = point.X / 4096f;
            float y = point.Y / 4096f;
            foreach (float integerX in new[] { MathF.Floor(x), MathF.Ceiling(x) })
            {
                float squared = range * (float)range - 1 - (integerX - center.X) * (integerX - center.X);
                if (squared >= 0)
                {
                    int extent = (int)MathF.Sqrt(squared);
                    Add(new(integerX, center.Y - extent));
                    Add(new(integerX, center.Y + extent));
                }
                Add(new(integerX, MathF.Floor(y)));
                Add(new(integerX, MathF.Ceiling(y)));
            }
            foreach (float integerY in new[] { MathF.Floor(y), MathF.Ceiling(y) })
            {
                float squared = range * (float)range - 1 - (integerY - center.Y) * (integerY - center.Y);
                if (squared >= 0)
                {
                    int extent = (int)MathF.Sqrt(squared);
                    Add(new(center.X - extent, integerY));
                    Add(new(center.X + extent, integerY));
                }
            }
        }
        List<Vector3> result = [];
        foreach (var point in points.OrderBy(point => point.X).ThenBy(point => point.Y))
        {
            Vector3 candidate = new(point.X / 4096f, point.Y / 4096f, 0);
            if (Contains(triangle, candidate, out float height) && height >= target.Z - 255 && height < target.Z + 256)
            {
                result.Add(candidate with { Z = height });
            }
        }
        return result;
    }
}
