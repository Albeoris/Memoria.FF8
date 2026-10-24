using System.Numerics;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Rendering;

public sealed partial class WorldTerrainRenderer
{
    private void DrawWorldClouds(WorldCamera camera, Vector3 position, WorldSky sky, byte processMode)
    {
        if (!camera.NativeProjection || processMode == 12)
        {
            return;
        }
        WorldSkyGradient gradient = WorldSkyProjection.Native(camera, position, sky);
        int nativeYaw = (int)MathF.Round(camera.Heading * 4096 / MathF.Tau);
        foreach (WorldMapLayer layer in WorldCloudPresentation.Create(nativeYaw, gradient, processMode))
        {
            DrawWorldScreenLayer(layer);
        }
    }
}
