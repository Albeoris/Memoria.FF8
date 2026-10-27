using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Rendering;

public static class NativeWorldParticleTile
{
    public static (int Offset, int Size) Dimensions(ushort scale, short halfSize)
    {
        int half = unchecked(scale * halfSize) >> 13;
        short size = unchecked((short)(2 * half));
        return (-half, size == 0 ? 8 : size);
    }
}

public sealed partial class WorldTerrainRenderer
{
    private void DrawForestTile(WorldForestParticleFrame visual, NativeWorldForestEffect effect, Vector3 position)
    {
        float nearX = position.X + WorldAssets.Wrap(visual.Position.X - position.X, 262144);
        float nearZ = position.Z + WorldAssets.Wrap(visual.Position.Z - position.Z, 196608);
        Vertex center = Transform(new(new Vector3(nearX, -visual.Position.Y, nearZ) / 16, Vector2.Zero));
        if (center.Position.Z < 1 || center.Position.Z * 16 > 8191)
        {
            return;
        }
        ScreenVertex projected = Project(center);
        (int offset, int size) = NativeWorldParticleTile.Dimensions(visual.Particle.Scale, effect.HalfSize);
        float scale = Height / 224f;
        float left = projected.X + offset * scale;
        float top = projected.Y + offset * scale;
        float right = left + size * scale;
        float bottom = top + size * scale;
        if (right < 0 || left > Width || bottom < 0 || top > Height)
        {
            return;
        }
        PixelImage texture = new(1, 1);
        texture.Pixels[0] = new(effect.Red, effect.Green, effect.Blue, 255);
        ScreenVertex a = new(left, top, projected.ReciprocalZ, Vector2.Zero);
        ScreenVertex b = new(right, top, projected.ReciprocalZ, Vector2.Zero);
        ScreenVertex c = new(left, bottom, projected.ReciprocalZ, Vector2.Zero);
        ScreenVertex d = new(right, bottom, projected.ReciprocalZ, Vector2.Zero);
        if (TriangleRenderer is not null)
        {
            DrawGpuTriangle(a, b, c, texture, false, null);
            DrawGpuTriangle(c, b, d, texture, false, null);
            DrawnTriangles += 2;
        }
        else
        {
            rasterTriangles.Add(new(a, b, c, texture, false));
            rasterTriangles.Add(new(c, b, d, texture, false));
        }
    }
}
