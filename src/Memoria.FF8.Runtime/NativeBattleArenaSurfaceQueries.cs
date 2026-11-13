using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime;

/// <summary>Queries original arena topology without assigning meanings to mesh parts or materials.</summary>
public static partial class NativeBattleArenaSurfaceQueries
{
    /// <summary>Selects the nearest horizontal sheet under the anchor's vertical line. Nonhorizontal native ground deformation remains unsupported.</summary>
    public static NativeBattleArenaGeometry Select(NativeBattleArenaGeometry geometry, NativeBattleVector anchor)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        if (geometry.Faces.IsDefaultOrEmpty)
        {
            throw new NotSupportedException("The host arena has no surface geometry.");
        }
        if (geometry.Faces.Select(face => face.Id).Distinct().Count() != geometry.Faces.Length)
        {
            throw new InvalidDataException("Host arena primitive identities must be unique.");
        }
        List<int> nearest = [];
        double nearestDistance = double.PositiveInfinity;
        for (int index = 0; index < geometry.Faces.Length; index++)
        {
            NativeBattleArenaFace face = geometry.Faces[index];
            double area = Cross(face.A, face.B, face.C);
            if (area == 0)
            {
                continue;
            }
            Vector3 point = new(anchor.X, anchor.Y, anchor.Z);
            double a = Cross(face.B, face.C, point);
            double b = Cross(face.C, face.A, point);
            double c = Cross(face.A, face.B, point);
            if (area > 0 ? a < 0 || b < 0 || c < 0 : a > 0 || b > 0 || c > 0)
            {
                continue;
            }
            double y = HorizontalAt(face, face.A.Y) ? face.A.Y : (a * face.A.Y + b * face.B.Y + c * face.C.Y) / area;
            double distance = Math.Abs(y - anchor.Y);
            if (distance < nearestDistance)
            {
                nearest.Clear();
                nearestDistance = distance;
            }
            if (distance == nearestDistance)
            {
                nearest.Add(index);
            }
        }
        if (nearest.Count == 0)
        {
            throw new NotSupportedException("The host arena has no surface at the requested anchor.");
        }
        NativeBattleArenaFace seed = geometry.Faces[nearest[0]];
        float height = seed.A.Y;
        if (nearest.Any(index => !HorizontalAt(geometry.Faces[index], height)))
        {
            throw new NotSupportedException("Native ground deformation requires one unambiguous horizontal host surface at the requested anchor.");
        }
        Dictionary<(Vector3 A, Vector3 B), List<int>> edges = [];
        for (int index = 0; index < geometry.Faces.Length; index++)
        {
            NativeBattleArenaFace face = geometry.Faces[index];
            if (!HorizontalAt(face, height) || Cross(face.A, face.B, face.C) == 0)
            {
                continue;
            }
            AddEdge(face.A, face.B, index);
            AddEdge(face.B, face.C, index);
            AddEdge(face.C, face.A, index);
        }
        HashSet<int> selected = [];
        Queue<int> pending = new();
        pending.Enqueue(nearest[0]);
        while (pending.TryDequeue(out int index))
        {
            if (!selected.Add(index))
            {
                continue;
            }
            NativeBattleArenaFace face = geometry.Faces[index];
            VisitEdge(face.A, face.B);
            VisitEdge(face.B, face.C);
            VisitEdge(face.C, face.A);
        }
        if (nearest.Any(index => !selected.Contains(index)))
        {
            throw new NotSupportedException("The requested host surface overlaps disconnected arena sheets.");
        }
        return new(geometry.Faces.Where((_, index) => selected.Contains(index)).ToImmutableArray());

        void AddEdge(Vector3 a, Vector3 b, int index)
        {
            var key = Edge(a, b);
            if (!edges.TryGetValue(key, out List<int>? faces))
            {
                faces = [];
                edges.Add(key, faces);
            }
            faces.Add(index);
        }
        void VisitEdge(Vector3 a, Vector3 b)
        {
            var selectedEdge = Edge(a, b);
            foreach (var entry in edges)
            {
                if (entry.Key != selectedEdge && !EdgesOverlap(a, b, entry.Key.A, entry.Key.B))
                {
                    continue;
                }
                foreach (int index in entry.Value)
                {
                    if (!selected.Contains(index))
                    {
                        pending.Enqueue(index);
                    }
                }
            }
        }
    }

    private static bool HorizontalAt(NativeBattleArenaFace face, float height) => face.A.Y == height && face.B.Y == height && face.C.Y == height;

    private static double Cross(Vector3 a, Vector3 b, Vector3 point) => ((double)b.X - a.X) * ((double)point.Z - a.Z) - ((double)b.Z - a.Z) * ((double)point.X - a.X);

    private static bool EdgesOverlap(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        if (Cross(a, b, c) != 0 || Cross(a, b, d) != 0)
        {
            return false;
        }
        return a.X != b.X ? Math.Max(Math.Min(a.X, b.X), Math.Min(c.X, d.X)) < Math.Min(Math.Max(a.X, b.X), Math.Max(c.X, d.X)) : Math.Max(Math.Min(a.Z, b.Z), Math.Min(c.Z, d.Z)) < Math.Min(Math.Max(a.Z, b.Z), Math.Max(c.Z, d.Z));
    }

    private static (Vector3 A, Vector3 B) Edge(Vector3 a, Vector3 b)
    {
        int order = a.X.CompareTo(b.X);
        if (order == 0)
        {
            order = a.Y.CompareTo(b.Y);
        }
        if (order == 0)
        {
            order = a.Z.CompareTo(b.Z);
        }
        return order <= 0 ? (a, b) : (b, a);
    }
}
