using System.Numerics;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7WorldRuntime
{
    private sealed class FootTerrainPoint(int x, int z)
    {
        public int X { get; } = x;
        public int Z { get; } = z;
        public int ChunkX => X >> 13;
        public int ChunkZ => Z >> 13;
        public int Status { get; set; } = 1;
        public int Height { get; set; }
        public int Terrain { get; set; }
    }

    private sealed record FootCandidate(int Angle, FootTerrainPoint[] Points)
    {
        public bool Valid => Points.All(point => point.Status == 2);
    }

    private sealed record FootCollisionResult(int X, int Z, int Height, int Terrain, int Angle, int Side);

    private FootCollisionResult? ResolveFootCollision(Ff7WorldEntity player)
    {
        int dx = unchecked((short)WrappedDifference(player.X - input.PreviousX, 0x48000));
        int dz = unchecked((short)WrappedDifference(player.Z - input.PreviousZ, 0x38000));
        int angleStep = input.BridgeCooldown == 0 ? 160 : 320;
        FootCandidate[][] candidates = new FootCandidate[2][];
        for (int side = 0; side < 2; side++)
        {
            candidates[side] = new FootCandidate[8];
            for (int candidate = 0; candidate < 8; candidate++)
            {
                int angle = candidate * angleStep * (side == 0 ? 1 : -1);
                Vector3 displacement = inputMath.TransformRotation(new(dx, 0, dz), 0, angle, 0);
                int x = input.PreviousX + (int)displacement.X;
                int z = input.PreviousZ + (int)displacement.Z;
                FootTerrainPoint Point(int offsetX, int offsetZ) => new(Wrap(x + offsetX, 0x48000), Wrap(z + offsetZ, 0x38000));
                candidates[side][candidate] = new(angle, [Point(0, 0), Point(-200, 0), Point(200, 0), Point(0, -200), Point(0, 200)]);
            }
        }
        FootTerrainPoint first = candidates[0][0].Points[0];
        List<(int X, int Z)> chunks = [(first.ChunkX, first.ChunkZ)];
        for (int chunkIndex = 0; chunkIndex < chunks.Count; chunkIndex++)
        {
            (int chunkX, int chunkZ) = chunks[chunkIndex];
            for (int pass = 0; pass < 2; pass++)
            {
                int side = pass == 0 ? input.CollisionSide : 1 - input.CollisionSide;
                (int X, int Z)? pendingChunk = null;
                for (int candidate = 0; candidate < 8; candidate++)
                {
                    foreach (FootTerrainPoint point in candidates[side][candidate].Points)
                    {
                        if (point.Status != 1)
                        {
                            continue;
                        }
                        if (point.ChunkX != chunkX || point.ChunkZ != chunkZ)
                        {
                            pendingChunk ??= (point.ChunkX, point.ChunkZ);
                            continue;
                        }
                        (int Height, int Terrain)? terrain = QueryTerrain(point.X, point.Z, unchecked((short)player.Y), player.Terrain);
                        if (terrain is { } found)
                        {
                            point.Status = 2;
                            point.Height = found.Height;
                            point.Terrain = found.Terrain;
                        }
                        else
                        {
                            point.Status = 3;
                        }
                    }
                    if (candidate == 0 && candidates[side][candidate].Valid)
                    {
                        break;
                    }
                }
                if (pendingChunk is { } pending && !chunks.Skip(chunkIndex + 1).Contains(pending))
                {
                    if (chunks.Count == 10)
                    {
                        throw new InvalidDataException("FFVII world collision exceeded the native pending-chunk allocation.");
                    }
                    chunks.Add(pending);
                }
            }
            foreach (FootTerrainPoint point in candidates.SelectMany(side => side).SelectMany(candidate => candidate.Points))
            {
                if (point.Status == 1 && point.ChunkX == chunkX && point.ChunkZ == chunkZ)
                {
                    point.Status = 4;
                }
            }
        }
        for (int pass = 0; pass < (input.CollisionAge >= 8 ? 2 : 1); pass++)
        {
            int side = pass == 0 ? input.CollisionSide : 1 - input.CollisionSide;
            FootCandidate? selected = candidates[side].FirstOrDefault(candidate => candidate.Valid);
            if (selected is not null)
            {
                FootTerrainPoint center = selected.Points[0];
                return new(center.X, center.Z, center.Height, center.Terrain, selected.Angle, side);
            }
        }
        return null;
    }
}
