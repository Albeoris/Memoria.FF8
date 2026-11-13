namespace Memoria.FF7.Runtime;

public sealed partial class Ff7WorldRuntime
{
    private const int SubmarineModel = 13;
    private const uint SubmarineTerrainMask = 0x04048008;
    private int? submarineTint;

    private static int ResolveUnderwaterBlock(int chunkX, int chunkZ)
    {
        int x = (chunkX >> 2) - 3;
        int z = (chunkZ >> 2) - 2;
        x = x < 0 ? x + 3 : x > 2 ? x - 3 : x;
        z = z < 0 ? z + 4 : z > 3 ? z - 4 : z;
        return z * 3 + x;
    }

    private void RestoreUnderwaterBattleVehicle()
    {
        if (Resources.World != 2 || entryModule != 1)
        {
            return;
        }
        if (persistent[0x3FD] != SubmarineModel || !entities.TryGetValue(SubmarineModel, out Ff7WorldEntity? submarine))
        {
            return;
        }
        controlledModel = submarine.Model;
        SetWorldViewMode(2);
        UpdateEntity(submarine with { Y = -3000, Flags = submarine.Flags | 128 });
    }

    private void ResolveUnderwaterSubmarineTerrain(Ff7WorldEntity submarine)
    {
        (int Height, int Terrain)? terrain = QueryTerrain(submarine.X, submarine.Z, submarine.Y, submarine.Terrain, submarine.Model);
        if (terrain is { } bottom)
        {
            int height = movementEnabled && submarine.Y <= bottom.Height + 200 ? Math.Min(-3000, bottom.Height + 200) : submarine.Y;
            UpdateEntity(submarine with { Y = height, GroundHeight = bottom.Height, Terrain = bottom.Terrain, TerrainResolved = true, Flags = submarine.Flags | 2 });
        }
    }
}
