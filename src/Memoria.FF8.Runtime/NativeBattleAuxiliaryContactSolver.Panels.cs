using System.Numerics;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleAuxiliaryContactSolver
{
    private sealed record Panel(int[][] Faces, IReadOnlyDictionary<int, int[]> OriginalFaces, bool HasOpenBoundary, bool Ambiguous);

    private static Dictionary<int, Panel> BuildPanels(int[][] triangles, int[] clothIds)
    {
        (int, int, int) FaceKey(int[] triangle)
        {
            int[] ordered = triangle.Order().ToArray();
            return (ordered[0], ordered[1], ordered[2]);
        }
        Dictionary<(int, int, int), int> unique = [];
        Dictionary<int, int> representative = [];
        foreach (int id in clothIds)
        {
            var key = FaceKey(triangles[id]);
            if (!unique.TryGetValue(key, out int first))
            {
                first = id;
                unique.Add(key, first);
            }
            representative.Add(id, first);
        }
        Dictionary<(int, int), HashSet<int>> edges = [];
        foreach (int id in unique.Values.Order())
        {
            int[] face = triangles[id];
            for (int corner = 0; corner < 3; corner++)
            {
                int a = face[corner];
                int b = face[(corner + 1) % 3];
                if (a == b)
                {
                    continue;
                }
                var key = a < b ? (a, b) : (b, a);
                if (!edges.TryGetValue(key, out HashSet<int>? owners))
                {
                    owners = [];
                    edges.Add(key, owners);
                }
                owners.Add(id);
            }
        }
        Dictionary<int, Panel> byRepresentative = [];
        foreach (int seed in unique.Values.Order())
        {
            if (byRepresentative.ContainsKey(seed))
            {
                continue;
            }
            Dictionary<int, int[]> oriented = new() { [seed] = (int[])triangles[seed].Clone() };
            Queue<int> pending = new();
            pending.Enqueue(seed);
            bool ambiguous = false;
            while (pending.Count != 0)
            {
                int current = pending.Dequeue();
                int[] face = oriented[current];
                for (int corner = 0; corner < 3; corner++)
                {
                    int a = face[corner];
                    int b = face[(corner + 1) % 3];
                    if (a == b)
                    {
                        continue;
                    }
                    var key = a < b ? (a, b) : (b, a);
                    HashSet<int> owners = edges[key];
                    ambiguous |= owners.Count > 2;
                    foreach (int neighbor in owners.Order())
                    {
                        if (!oriented.TryGetValue(neighbor, out int[]? neighborFace))
                        {
                            int[] raw = triangles[neighbor];
                            neighborFace = HasDirectedEdge(raw, a, b) ? [raw[0], raw[2], raw[1]] : (int[])raw.Clone();
                            oriented.Add(neighbor, neighborFace);
                            pending.Enqueue(neighbor);
                        }
                        else if (neighbor != current && HasDirectedEdge(neighborFace, a, b))
                        {
                            ambiguous = true;
                        }
                    }
                }
            }
            bool open = edges.Values.Any(owners => owners.Count == 1 && oriented.ContainsKey(owners.First()));
            Dictionary<int, int[]> originalFaces = representative.Where(pair => oriented.ContainsKey(pair.Value)).ToDictionary(pair => pair.Key, pair => oriented[pair.Value]);
            Panel panel = new(oriented.OrderBy(pair => pair.Key).Select(pair => pair.Value).ToArray(), originalFaces, open, ambiguous);
            foreach (int id in oriented.Keys)
            {
                byRepresentative.Add(id, panel);
            }
        }
        return representative.ToDictionary(pair => pair.Key, pair => byRepresentative[pair.Value]);
    }

    private static bool HasDirectedEdge(int[] face, int a, int b)
    {
        for (int corner = 0; corner < 3; corner++)
        {
            if (face[corner] == a && face[(corner + 1) % 3] == b)
            {
                return true;
            }
        }
        return false;
    }

    private static Constraint? ReadPanelWitness(Layer layer, Vector3[] points, Vector3 outward)
    {
        Constraint? selected = null;
        foreach (int[] face in layer.Panel.Faces)
        {
            Vector3 first = points[face[1]] - points[face[0]];
            Vector3 second = points[face[2]] - points[face[0]];
            Vector3 normal = Vector3.Cross(first, second);
            float crossing = Vector3.Dot(normal, outward);
            if (crossing * layer.OutwardPolarity <= 0 || !LineTriangle(points[layer.Body], outward, points[face[0]], points[face[1]], points[face[2]], out float distance, out Vector3 weights))
            {
                continue;
            }
            if (selected is null || MathF.Abs(distance) < Math.Abs(selected.Value))
            {
                // The body and ray direction are physical. Moving this finite plane changes ray distance by n·velocity / n·direction.
                selected = new Constraint { Body = layer.Body, Cloth = face, Weights = weights, Normal = normal / crossing, Value = distance, Kind = "OwnRestOutwardLayer", Hard = !layer.Panel.HasOpenBoundary };
            }
        }
        if (selected is null && !layer.Panel.HasOpenBoundary)
        {
            throw new NotSupportedException("An auxiliary closed covering panel has no current outward crossing witness.");
        }
        return selected;
    }
}
