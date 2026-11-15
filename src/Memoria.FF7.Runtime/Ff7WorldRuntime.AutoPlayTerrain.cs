using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

internal sealed record Ff7AutoPlayWorldTerrainPoint(int X, int Z, int Height, int Terrain, Ff7WorldTerrainReference Triangle, PcWorldTriangle Face);

public sealed partial class Ff7WorldRuntime
{
    /// <summary>Samples the native terrain selector for the current controlled model without changing its execution cache. A point sample does not prove that a complete movement or collision footprint can reach it.</summary>
    internal Ff7AutoPlayWorldTerrainPoint? ReadAutoPlayTerrain(int x, int z)
    {
        if (controlledModel < 0)
        {
            return null;
        }
        Ff7WorldEntity player = RequireEntity(controlledModel);
        bool dismount = IsWorldChocobo(player.Model) && chocobo is { DismountPhase: 2 };
        Ff7WorldInputState retainedInput = input;
        try
        {
            (int Height, int Terrain)? sampled = QueryTerrain(x, z, unchecked((short)player.Y), player.Terrain, player.Model, dismount, out Ff7WorldTerrainReference? reference);
            if (sampled is not { } point || reference is null)
            {
                return null;
            }
            PcWorldMesh mesh = GetWorldCollisionMesh(reference.ChunkX, reference.ChunkZ);
            return new(x, z, point.Height, point.Terrain, reference, mesh.Triangles[reference.Triangle]);
        }
        finally
        {
            // QueryTerrain's overlap-selection cache affects later native collision decisions.
            // Keep that execution state exact; decoded and deformed resource mesh caches may warm.
            input = retainedInput;
        }
    }

    /// <summary>Reads the same world-state remapping and current bridge deformation used by native collision.</summary>
    internal PcWorldMesh ReadAutoPlayCollisionMesh(int chunkX, int chunkZ)
    {
        return GetWorldCollisionMesh(chunkX, chunkZ);
    }
}
