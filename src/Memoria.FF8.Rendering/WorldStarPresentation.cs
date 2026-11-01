using System.Numerics;
using Albeoris.Games.FF8.Imaging;

namespace Memoria.FF8.Rendering;

public readonly record struct WorldStarPoint(Vector2 Position, Rgba32 Color);

/// <summary>The original fixed random sky points and their discrete color cycle.</summary>
public static class WorldStarPresentation
{
    public static IReadOnlyList<WorldStarPoint> Create(int nativeCameraYaw, WorldSkyGradient sky, uint mainLoopCounter, ReadOnlySpan<byte> random, bool enabled, byte processMode = 0, float left = 0, float right = 320)
    {
        if (!enabled || processMode == 12)
        {
            return Array.Empty<WorldStarPoint>();
        }
        if (random.Length != 256 || !float.IsFinite(left) || !float.IsFinite(right) || left >= right)
        {
            throw new ArgumentException("Sky points require the original random table and finite viewport bounds.");
        }
        int intensity = Math.Clamp(2 * (192 - sky.Top.B) - sky.Top.G - sky.Top.R, 0, 512);
        int start = -(nativeCameraYaw & 255);
        start += checked((int)MathF.Floor(MathF.Min(0, (left - start) / 256)) * 256);
        int top = sky.HorizonEighthY / 8 - 160;
        int counter = unchecked((int)mainLoopCounter);
        List<WorldStarPoint> result = [];
        for (int strip = start; strip < right; strip = checked(strip + 256))
        {
            for (int index = 0; index < 32; index++)
            {
                int x = strip + random[1 + 2 * index];
                int y = top + (random[2 + 2 * index] & (index % 2 == 0 ? 127 : 63));
                if (x < left || x >= right || (uint)y >= 224)
                {
                    continue;
                }
                int red;
                int green;
                int blue;
                if (index >= 16)
                {
                    red = green = blue = intensity * (64 + 8 * index) >> 9;
                }
                else
                {
                    int pulse = 4 * (index + 4 * Math.Abs((counter >> 1) % (index + 4) - 10));
                    red = green = blue = intensity * pulse >> 9;
                    int phase = unchecked((byte)pulse);
                    if ((index & 3) == 0)
                    {
                        red = intensity * (index & unchecked((byte)(phase + (counter >> 1))) & 7) >> 9;
                    }
                    else if ((index & 3) == 1)
                    {
                        green = intensity * (index & unchecked((byte)(phase + (counter >> 2))) & 5) >> 9;
                    }
                    else if ((index & 3) == 2)
                    {
                        blue = intensity * (index & unchecked((byte)(phase + (counter >> 3))) & 3) >> 9;
                    }
                }
                result.Add(new(new Vector2(x, y), new Rgba32(unchecked((byte)red), unchecked((byte)green), unchecked((byte)blue), 255)));
            }
        }
        return result.AsReadOnly();
    }
}
