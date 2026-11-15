using System.Numerics;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

internal sealed record Ff7AutoPlayWorldGuideFace(Ff7WorldTerrainReference Source, Vector3 A, Vector3 B, Vector3 C, int Script)
{
    public Vector3 Center => (A + B + C) / 3;
}

internal sealed record Ff7AutoPlayWorldGuide(Ff7AutoPlayWorldGuideFace[] Faces, int Expanded, bool Limited, string? Reason);

public sealed partial class Ff7WorldRuntime
{
    internal Ff7AutoPlayWorldGuide ReadAutoPlayNavigationGuide(Ff7AutoPlayWorldScriptObservation target, int maximumNodes = 16384, int maximumChunks = 128)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumChunks, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maximumChunks, 1008);
        Ff7WorldTerrainReference? start = input.GroundTriangle;
        if (start is null)
        {
            return new([], 0, false, "Native ground identity is unavailable for terrain advice.");
        }
        Dictionary<(int X, int Z), PcWorldMesh> chunks = [];
        Dictionary<Ff7WorldTerrainReference, Ff7AutoPlayWorldGuideFace> faces = [];
        Dictionary<(Vector3 A, Vector3 B), List<Ff7WorldTerrainReference>> edges = [];
        Dictionary<(Ff7WorldTerrainReference Source, int PreviousTerrain), bool> eligibility = [];
        bool limited = false;
        Ff7WorldEntity player = RequireEntity(controlledModel);
        HashSet<Ff7WorldTerrainReference> goals = target.Faces.Select(face => face.Source).ToHashSet();
        static Vector3 Canonical(Vector3 point) => new(Wrap((int)point.X, 0x48000), point.Y, Wrap((int)point.Z, 0x38000));
        static (Vector3 A, Vector3 B) Edge(Vector3 a, Vector3 b)
        {
            a = Canonical(a);
            b = Canonical(b);
            bool reverse = a.X > b.X || a.X == b.X && (a.Z > b.Z || a.Z == b.Z && a.Y > b.Y);
            return reverse ? (b, a) : (a, b);
        }
        static IEnumerable<(Vector3 A, Vector3 B)> FaceEdges(Ff7AutoPlayWorldGuideFace face)
        {
            yield return (face.A, face.B);
            yield return (face.B, face.C);
            yield return (face.C, face.A);
        }
        void Load(int x, int z)
        {
            x = Wrap(x, 36);
            z = Wrap(z, 28);
            if (chunks.ContainsKey((x, z)))
            {
                return;
            }
            if (chunks.Count >= maximumChunks)
            {
                limited = true;
                return;
            }
            PcWorldMesh mesh = GetWorldCollisionMesh(x, z);
            chunks.Add((x, z), mesh);
            Vector3 offset = new(x << 13, 0, z << 13);
            for (int index = 0; index < mesh.Triangles.Count; index++)
            {
                PcWorldTriangle triangle = mesh.Triangles[index];
                Ff7WorldTerrainReference reference = new(x, z, index);
                Ff7AutoPlayWorldGuideFace face = new(reference, mesh.Vertices[triangle.A] + offset, mesh.Vertices[triangle.B] + offset, mesh.Vertices[triangle.C] + offset, triangle.Script);
                faces.Add(reference, face);
                foreach (var edge in FaceEdges(face))
                {
                    var key = Edge(edge.A, edge.B);
                    if (!edges.TryGetValue(key, out var adjacent))
                    {
                        edges[key] = adjacent = [];
                    }
                    adjacent.Add(reference);
                }
            }
        }
        bool Eligible(Ff7WorldTerrainReference reference, int previousTerrain)
        {
            if (reference == start)
            {
                return true;
            }
            var key = (reference, previousTerrain);
            if (eligibility.TryGetValue(key, out bool known))
            {
                return known;
            }
            Ff7AutoPlayWorldGuideFace face = faces[reference];
            bool allowed = face.Script < 3 || goals.Contains(reference) || face.Script == input.TerrainScript;
            if (allowed)
            {
                Vector3 center = face.Center;
                Ff7WorldInputState retained = input;
                try
                {
                    // Geometry advice uses the candidate layer; only native movement can admit a crossing to it.
                    input = input with { TerrainCache = null };
                    var terrain = QueryTerrain(Wrap((int)center.X, 0x48000), Wrap((int)center.Z, 0x38000), (int)center.Y, previousTerrain, player.Model, false, out Ff7WorldTerrainReference? selected);
                    allowed = terrain is not null && selected == reference;
                }
                finally
                {
                    input = retained;
                }
            }
            eligibility[key] = allowed;
            return allowed;
        }
        IEnumerable<Ff7WorldTerrainReference> Adjacent(Ff7WorldTerrainReference reference)
        {
            Ff7AutoPlayWorldGuideFace face = faces[reference];
            foreach (var edge in FaceEdges(face))
            {
                if (edge.A.X == edge.B.X && (int)edge.A.X % 8192 == 0)
                {
                    int boundary = (int)edge.A.X >> 13;
                    Load(boundary - 1, reference.ChunkZ);
                    Load(boundary, reference.ChunkZ);
                }
                if (edge.A.Z == edge.B.Z && (int)edge.A.Z % 8192 == 0)
                {
                    int boundary = (int)edge.A.Z >> 13;
                    Load(reference.ChunkX, boundary - 1);
                    Load(reference.ChunkX, boundary);
                }
            }
            int previousTerrain = reference == start ? player.Terrain : chunks[(reference.ChunkX, reference.ChunkZ)].Triangles[reference.Triangle].Terrain;
            return FaceEdges(face).SelectMany(edge => edges[Edge(edge.A, edge.B)]).Where(next => next != reference).Distinct().Where(next => Eligible(next, previousTerrain)).ToArray();
        }
        Load(start.ChunkX, start.ChunkZ);
        Ff7AutoPlayWorldGuide result = PlanAutoPlayTerrainGuide(start, goals, reference => faces[reference], Adjacent, maximumNodes);
        return limited && result.Faces.Length == 0 ? result with { Limited = true, Reason = "The native terrain-advice chunk budget was exhausted." } : result;
    }

    internal static Ff7AutoPlayWorldGuide PlanAutoPlayTerrainGuide(Ff7WorldTerrainReference start, IReadOnlySet<Ff7WorldTerrainReference> goals, Func<Ff7WorldTerrainReference, Ff7AutoPlayWorldGuideFace> read, Func<Ff7WorldTerrainReference, IEnumerable<Ff7WorldTerrainReference>> adjacent, int maximumNodes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumNodes, 1);
        // Dijkstra over exact shared native polygon edges; this is advice, never movement authorization.
        Dictionary<Ff7WorldTerrainReference, double> costs = new() { [start] = 0 };
        Dictionary<Ff7WorldTerrainReference, Ff7WorldTerrainReference> parents = [];
        HashSet<Ff7WorldTerrainReference> closed = [];
        PriorityQueue<Ff7WorldTerrainReference, double> open = new();
        open.Enqueue(start, 0);
        int expanded = 0;
        while (expanded < maximumNodes && open.TryDequeue(out Ff7WorldTerrainReference? current, out _))
        {
            if (!closed.Add(current))
            {
                continue;
            }
            expanded++;
            if (goals.Contains(current))
            {
                List<Ff7AutoPlayWorldGuideFace> route = [read(current)];
                while (parents.TryGetValue(current, out Ff7WorldTerrainReference? parent))
                {
                    current = parent;
                    route.Add(read(current));
                }
                route.Reverse();
                return new(route.ToArray(), expanded, false, null);
            }
            foreach (Ff7WorldTerrainReference next in adjacent(current))
            {
                Vector3 a = read(current).Center;
                Vector3 b = read(next).Center;
                double dx = Math.Abs(a.X - b.X);
                double dz = Math.Abs(a.Z - b.Z);
                dx = Math.Min(dx, 0x48000 - dx);
                dz = Math.Min(dz, 0x38000 - dz);
                double cost = costs[current] + Math.Sqrt(dx * dx + dz * dz);
                if (closed.Contains(next) || costs.TryGetValue(next, out double known) && known <= cost)
                {
                    continue;
                }
                costs[next] = cost;
                parents[next] = current;
                open.Enqueue(next, cost);
            }
        }
        return new([], expanded, open.Count > 0, open.Count > 0 ? "The bounded native terrain-advice search is incomplete." : "No shared-edge advisory route exists in the observed native terrain component.");
    }
}
