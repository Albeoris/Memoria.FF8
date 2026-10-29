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
        float scale = Height / 224f;
        float offset = (Width - 320 * scale) / 2;
        Matrix3x2 viewport = Matrix3x2.CreateScale(scale) * Matrix3x2.CreateTranslation(offset, 0);
        foreach (WorldMapLayer layer in WorldCloudPresentation.Create(nativeYaw, gradient, processMode, -offset / scale, (Width - offset) / scale))
        {
            DrawWorldScreenLayer(layer, viewport);
        }
    }
}
