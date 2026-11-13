using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed record Ff7WorldChocoboPathFace(Ff7WorldTerrainReference Triangle, int TriedEdges = 0);
public sealed record Ff7WorldChocoboRelease(bool TowardFarm, Ff7WorldChocoboPathFace[] Path, Ff7WorldTerrainReference[] Visited, bool SearchComplete = false, int RunPhase = 0, int PathIndex = 0, int TargetX = 0, int TargetZ = 0, int Direction = 0, int PreviousDistance = 0);

public sealed partial class Ff7WorldRuntime
{
    private const int ChocoboFarmX = 0x3A681;
    private const int ChocoboFarmZ = 0x2195F;
    private const int ChocoboPathCapacity = 40;

    private static Ff7WorldChocoboState? CopyChocoboState(Ff7WorldChocoboState? state)
    {
        return state?.Release is { } release ? state with { Release = release with { Path = release.Path.ToArray(), Visited = release.Visited.ToArray() } } : state;
    }

    private void AdvanceReleasedChocobo()
    {
        Ff7WorldChocoboState state = chocobo!;
        Ff7WorldChocoboRelease? release = state.Release;
        if (release is null)
        {
            Ff7WorldEntity rider = RequireEntity(controlledModel);
            if (QueryTerrain(rider.X, rider.Z, rider.GroundHeight, rider.Terrain, rider.Model, false, out Ff7WorldTerrainReference? triangle) is null)
            {
                return;
            }
            bool towardFarm = rider.X is >= 0x36000 and < 0x40000 && rider.Z is >= 0x1C000 and < 0x26000;
            release = new(towardFarm, [new(triangle!)], [triangle!]);
        }
        if (!release.SearchComplete)
        {
            release = SearchChocoboReleasePath(release);
        }
        if (state.ReleaseRequested && release.SearchComplete)
        {
            Ff7WorldEntity bird = RequireEntity(YellowChocoboModel);
            if (release.RunPhase == 0)
            {
                release = release with { RunPhase = 7 };
            }
            if ((release.RunPhase & 4) != 0)
            {
                release = release with { RunPhase = release.RunPhase + 4 };
            }
            else
            {
                release = RunReleasedChocobo(bird, release);
                if (release.RunPhase == 0)
                {
                    Machine.RemoveModel(YellowChocoboModel);
                    entities.Remove(YellowChocoboModel);
                    chocobo = null;
                    return;
                }
            }
        }
        chocobo = state with { Release = release };
    }

    private Ff7WorldChocoboRelease SearchChocoboReleasePath(Ff7WorldChocoboRelease release)
    {
        if (!release.TowardFarm && release.Path.Length > 1)
        {
            return release with { SearchComplete = true };
        }
        Ff7WorldChocoboPathFace current = release.Path[^1];
        if (current.TriedEdges == 7)
        {
            if (release.Path.Length == 1)
            {
                return release with { SearchComplete = true };
            }
            return release with { Path = release.Path[..^1] };
        }
        Ff7WorldTerrainReference triangle = current.Triangle;
        PcWorldMesh mesh = GetWorldCollisionMesh(triangle.ChunkX, triangle.ChunkZ);
        PcWorldTriangle face = mesh.Triangles[triangle.Triangle];
        int[] vertices = [face.A, face.B, face.C];
        Vector3 center = (mesh.Vertices[face.A] + mesh.Vertices[face.B] + mesh.Vertices[face.C]) / 3;
        int heading = release.TowardFarm ? inputMath.Direction(ChocoboFarmX - (triangle.ChunkX * 8192 + (int)center.X), ChocoboFarmZ - (triangle.ChunkZ * 8192 + (int)center.Z)) : camera.Heading;
        Ff7WorldEntity rider = RequireEntity(controlledModel);
        int edge = 0;
        int best = int.MaxValue;
        for (int index = 0; index < 3; index++)
        {
            if ((current.TriedEdges & (1 << index)) != 0)
            {
                continue;
            }
            Vector3 a = mesh.Vertices[vertices[index]];
            Vector3 b = mesh.Vertices[vertices[(index + 1) % 3]];
            int x = ((int)a.X + (int)b.X) >> 1;
            int z = ((int)a.Z + (int)b.Z) >> 1;
            int score = current.TriedEdges == 0
                ? Math.Abs(WrappedDifference((inputMath.Direction(x - (int)center.X, z - (int)center.Z) - heading) & 4095, 4096))
                : release.TowardFarm
                    ? Math.Abs(ChocoboFarmX - (triangle.ChunkX * 8192 + x)) + Math.Abs(ChocoboFarmZ - (triangle.ChunkZ * 8192 + z))
                    : -Math.Abs(rider.X - (triangle.ChunkX * 8192 + x)) - Math.Abs(rider.Z - (triangle.ChunkZ * 8192 + z));
            if (score < best)
            {
                edge = index;
                best = score;
            }
        }
        Ff7WorldChocoboPathFace[] path = release.Path.ToArray();
        path[^1] = current with { TriedEdges = current.TriedEdges | 1 << edge };
        release = release with { Path = path };
        int first = vertices[edge];
        int second = vertices[(edge + 1) % 3];
        (Ff7WorldTerrainReference Triangle, int IncomingEdge)? adjacent = FindChocoboReleaseFace(triangle, mesh, first, second);
        if (adjacent is not { } next || release.Visited.Contains(next.Triangle))
        {
            return release;
        }
        if (path.Length == ChocoboPathCapacity - 1)
        {
            return release with { SearchComplete = true };
        }
        return release with { Path = [.. path, new(next.Triangle, next.IncomingEdge)], Visited = [.. release.Visited, next.Triangle] };
    }

    private (Ff7WorldTerrainReference Triangle, int IncomingEdge)? FindChocoboReleaseFace(Ff7WorldTerrainReference triangle, PcWorldMesh mesh, int first, int second)
    {
        for (int index = 0; index < mesh.Triangles.Count; index++)
        {
            PcWorldTriangle face = mesh.Triangles[index];
            int edge = SharedWorldEdge(face, first, second);
            if (index != triangle.Triangle && edge != 0)
            {
                return CanUseChocoboReleaseFace(face) ? (triangle with { Triangle = index }, edge) : null;
            }
        }
        Vector3 a = mesh.Vertices[first];
        Vector3 b = mesh.Vertices[second];
        int x = (int)(a.X + b.X) >> 1;
        int z = (int)(a.Z + b.Z) >> 1;
        int chunkX = triangle.ChunkX;
        int chunkZ = triangle.ChunkZ;
        int exit;
        if (a.X < 10 && b.X < 10)
        {
            chunkX = Wrap(chunkX - 1, 36);
            x = 8182;
            exit = 1;
        }
        else if (a.Z < 10 && b.Z < 10)
        {
            chunkZ = Wrap(chunkZ - 1, 28);
            z = 8182;
            exit = 2;
        }
        else if (a.X > 8182 && b.X > 8182)
        {
            chunkX = Wrap(chunkX + 1, 36);
            x = 10;
            exit = 3;
        }
        else if (a.Z > 8182 && b.Z > 8182)
        {
            chunkZ = Wrap(chunkZ + 1, 28);
            z = 10;
            exit = 4;
        }
        else
        {
            return null;
        }
        if (QueryTerrain((chunkX << 13) + x, (chunkZ << 13) + z, 0, 0, 0, false, out Ff7WorldTerrainReference? adjacent) is null)
        {
            return null;
        }
        PcWorldMesh nextMesh = GetWorldCollisionMesh(chunkX, chunkZ);
        PcWorldTriangle candidate = nextMesh.Triangles[adjacent!.Triangle];
        if (!CanUseChocoboReleaseFace(candidate))
        {
            return null;
        }
        bool OnIncomingBoundary(Vector3 vertex) => exit switch { 1 => vertex.X > 8182, 2 => vertex.Z > 8182, 3 => vertex.X < 10, _ => vertex.Z < 10 };
        int[] vertices = [candidate.A, candidate.B, candidate.C];
        for (int index = 0; index < 3; index++)
        {
            if (OnIncomingBoundary(nextMesh.Vertices[vertices[index]]) && OnIncomingBoundary(nextMesh.Vertices[vertices[(index + 1) % 3]]))
            {
                return (adjacent, 1 << index);
            }
        }
        return null;
    }

    private static int SharedWorldEdge(PcWorldTriangle face, int first, int second)
    {
        bool IsEdge(int a, int b) => a == first && b == second || a == second && b == first;
        return IsEdge(face.A, face.B) ? 1 : IsEdge(face.B, face.C) ? 2 : IsEdge(face.C, face.A) ? 4 : 0;
    }

    private static bool CanUseChocoboReleaseFace(PcWorldTriangle face) => face.Script != 7 && (YellowChocoboTerrainMask & (1u << face.Terrain)) != 0;

    private (int X, int Z) ChocoboFaceCenter(Ff7WorldTerrainReference triangle)
    {
        PcWorldMesh mesh = GetWorldCollisionMesh(triangle.ChunkX, triangle.ChunkZ);
        PcWorldTriangle face = mesh.Triangles[triangle.Triangle];
        Vector3 center = (mesh.Vertices[face.A] + mesh.Vertices[face.B] + mesh.Vertices[face.C]) / 3;
        return ((triangle.ChunkX << 13) + (int)center.X, (triangle.ChunkZ << 13) + (int)center.Z);
    }

    private Ff7WorldChocoboRelease RunReleasedChocobo(Ff7WorldEntity bird, Ff7WorldChocoboRelease release)
    {
        if (release.PathIndex + 1 >= release.Path.Length)
        {
            return release with { RunPhase = 0 };
        }
        if ((release.RunPhase & 1) != 0)
        {
            int next = release.PathIndex + 1;
            (int x, int z) = ChocoboFaceCenter(release.Path[next].Triangle);
            if (next < release.Path.Length - 1)
            {
                (int nextX, int nextZ) = ChocoboFaceCenter(release.Path[next + 1].Triangle);
                x = (x + nextX) >> 1;
                z = (z + nextZ) >> 1;
            }
            release = release with { RunPhase = release.RunPhase & ~1, TargetX = x, TargetZ = z, Direction = inputMath.Direction(x - bird.X, z - bird.Z) & 4095, PreviousDistance = 0x7FFF };
        }
        Vector3 step = inputMath.TransformRotation(new(0, 0, 120), 0, release.Direction, 0);
        UpdateEntity(OffsetWorldEntity(bird with { TargetDirection = release.Direction, ScriptDirection = null, TurnOffset = 0 }, (int)step.X, (int)step.Z));
        int distance = Math.Abs(bird.X - release.TargetX) + Math.Abs(bird.Z - release.TargetZ);
        if (release.TowardFarm && Math.Abs(bird.X - ChocoboFarmX) + Math.Abs(bird.Z - ChocoboFarmZ) < 2000)
        {
            return release with { RunPhase = 0 };
        }
        return distance > release.PreviousDistance
            ? release with { PathIndex = release.PathIndex + 1, RunPhase = release.RunPhase | 1, PreviousDistance = distance }
            : release with { PreviousDistance = distance };
    }

    private void ValidateChocoboRelease(Ff7WorldChocoboRelease? release, int mapState)
    {
        if (release is null)
        {
            return;
        }
        bool Valid(Ff7WorldTerrainReference? triangle)
        {
            if (triangle is null || triangle.ChunkX is < 0 or >= 36 || triangle.ChunkZ is < 0 or >= 28 || triangle.Triangle < 0)
            {
                return false;
            }
            int block = Ff7WorldVisibility.ResolveBlockIndex((triangle.ChunkZ >> 2) * 9 + (triangle.ChunkX >> 2), mapState);
            return triangle.Triangle < Resources.Map.ReadMesh(block, (triangle.ChunkZ & 3) * 4 + (triangle.ChunkX & 3)).Triangles.Count;
        }
        if (release.Path is null || release.Path.Length is 0 or >= ChocoboPathCapacity || release.Visited is null || release.Visited.Length < release.Path.Length ||
            release.PathIndex < 0 || release.PathIndex >= release.Path.Length || release.RunPhase is not (0 or 10 or 11) || release.Direction is < 0 or > 4095 || release.RunPhase != 0 && !release.SearchComplete ||
            release.Path.Any(face => face is null || face.TriedEdges is < 0 or > 7 || !Valid(face.Triangle) || !release.Visited.Contains(face.Triangle)) ||
            release.Visited.Any(triangle => !Valid(triangle)) || release.Visited.Distinct().Count() != release.Visited.Length)
        {
            throw new InvalidDataException("FFVII world Chocobo release path has invalid terrain references.");
        }
    }
}
