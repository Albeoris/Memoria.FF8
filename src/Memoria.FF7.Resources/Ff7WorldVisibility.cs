using System.Numerics;

namespace Memoria.FF7.Resources;

public sealed record Ff7WorldChunkPlacement(int ChunkX, int ChunkZ, int BlockIndex, int MeshIndex, Vector3 Origin);

public sealed class Ff7WorldVisibility(Ff7WorldPresentationData presentation)
{
    private readonly Ff7WorldMath math = new(presentation);

    public Vector3? VisiblePosition(Vector3 reference, Vector3 model, int flags, int heading)
    {
        if ((flags & 8) != 0 || (flags & 2) == 0)
        {
            return null;
        }
        int chunkX = (int)model.X >> 13;
        int chunkZ = (int)model.Z >> 13;
        if (!RequiredChunks((int)reference.X, (int)reference.Z, heading).Any(value => value.ChunkX == chunkX && value.ChunkZ == chunkZ))
        {
            return null;
        }
        float dx = model.X - reference.X;
        float dz = model.Z - reference.Z;
        if (dx < -0x24000)
        {
            dx += 0x48000;
        }
        else if (dx >= 0x24000)
        {
            dx -= 0x48000;
        }
        if (dz < -0x1C000)
        {
            dz += 0x38000;
        }
        else if (dz >= 0x1C000)
        {
            dz -= 0x38000;
        }
        return new Vector3(reference.X + dx, model.Y, reference.Z + dz);
    }

    public static int ResolveBlockIndex(int block, int worldState)
    {
        if (block is < 0 or >= 63)
        {
            throw new ArgumentOutOfRangeException(nameof(block));
        }
        return block switch
        {
            0x32 when worldState > 0 => 0x3F,
            0x29 when worldState > 1 => 0x40,
            0x2A when worldState > 1 => 0x41,
            0x3C when worldState > 2 => 0x42,
            0x2F when worldState > 3 => 0x43,
            0x30 when worldState > 3 => 0x44,
            _ => block
        };
    }

    public IReadOnlyList<Ff7WorldChunkPlacement> RequiredChunks(int x, int z, int heading, int worldState = 0)
    {
        if (x is < 0 or >= 0x48000 || z is < 0 or >= 0x38000)
        {
            throw new ArgumentOutOfRangeException(nameof(x));
        }
        Vector3 left = Vector3.Transform(new(0, 0, -4096), math.Rotation(0, -heading + 0x380, 0));
        Vector3 right = Vector3.Transform(new(0, 0, -4096), math.Rotation(0, -heading - 0x380, 0));
        int localX = (x & 8191) - 4096;
        int localZ = (z & 8191) - 4096;
        int chunkX = x >> 13;
        int chunkZ = z >> 13;
        List<Ff7WorldChunkPlacement> result = [];
        for (int dz = -2; dz <= 2; dz++)
        {
            for (int dx = -2; dx <= 2; dx++)
            {
                if (Math.Abs(dx) > 1 || Math.Abs(dz) > 1)
                {
                    int vx = (dx << 13) - localX;
                    int vz = (dz << 13) - localZ;
                    int crossLeft = unchecked((int)left.Z * vx - (int)left.X * vz) >> 12;
                    int crossRight = unchecked((int)right.Z * vx - (int)right.X * vz) >> 12;
                    if (crossLeft >= 0 || crossRight <= 0)
                    {
                        continue;
                    }
                }
                int actualX = (chunkX + dx + 36) % 36;
                int actualZ = (chunkZ + dz + 28) % 28;
                result.Add(new(actualX, actualZ, ResolveBlockIndex((actualZ >> 2) * 9 + (actualX >> 2), worldState), (actualZ & 3) * 4 + (actualX & 3), new((chunkX + dx) << 13, 0, (chunkZ + dz) << 13)));
            }
        }
        return result.AsReadOnly();
    }
}
