using System.Numerics;

namespace Memoria.FF8.Rendering;

/// <summary>The original repeating, Gouraud-tinted horizon cloud strip.</summary>
public static class WorldCloudPresentation
{
    public static IReadOnlyList<WorldMapLayer> Create(int nativeCameraYaw, WorldSkyGradient sky, byte processMode = 0, float left = 0, float right = 320)
    {
        if (!float.IsFinite(left) || !float.IsFinite(right) || left >= right)
        {
            throw new ArgumentOutOfRangeException(nameof(right));
        }
        if (processMode == 12)
        {
            return Array.Empty<WorldMapLayer>();
        }
        int lowYaw = nativeCameraYaw & 255;
        int start = lowYaw == 0 ? -256 : -lowYaw;
        start = checked(start + checked((int)MathF.Floor(MathF.Min(0, (left - start) / 256)) * 256));
        float top = sky.HorizonEighthY / 8f - 48;
        Vector4 upper = new(0.5f, 0.5f, 0.5f, 1);
        Vector4 lower = new((sky.Bottom.R >> 1) / 128f, (sky.Bottom.G >> 1) / 128f, (sky.Bottom.B >> 1) / 128f, 1);
        List<WorldMapTriangle> triangles = [];
        for (int x = start; x < right; x = checked(x + 256))
        {
            WorldMapVertex a = new(new Vector2(x, top), new Vector2(1.5f / 256, 1.5f / 64), upper);
            WorldMapVertex b = new(new Vector2(x + 256, top), new Vector2(254.5f / 256, 1.5f / 64), upper);
            WorldMapVertex c = new(new Vector2(x, top + 64), new Vector2(1.5f / 256, 62.5f / 64), lower);
            WorldMapVertex d = new(new Vector2(x + 256, top + 64), new Vector2(254.5f / 256, 62.5f / 64), lower);
            triangles.Add(new(a, b, c));
            triangles.Add(new(c, b, d));
        }
        return [new(10, 0, true, false, triangles.AsReadOnly()) { Additive = true }];
    }
}
