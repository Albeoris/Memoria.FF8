using Memoria.FF8.Assets;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class OriginalSaveExportPlace
{
    public static short? Read(GameState state, string gameDirectory, ResourceService? resources = null)
    {
        if (state.Scene != SceneKind.WorldMap)
        {
            return null;
        }
        WorldState world = state.World ?? throw new InvalidDataException("Original world save preview requires the exported world's current coordinates.");
        using WorldAssets assets = new(gameDirectory, resources);
        return assets.GetNativeMapLocationId(world.X, world.Z);
    }
}
