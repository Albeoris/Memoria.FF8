using System.Numerics;

namespace Memoria.FF8.Rendering;

/// <summary>Original underwater terrain palette selection and per-corner lighting.</summary>
public static class Ff7WorldUnderwaterTerrain
{
    public static Vector3 Palette(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, 255);
        return new(Math.Max(0, 187 - 2 * Math.Min(index, 128)), Math.Max(0, 187 - index), 255 - 2 * Math.Max(0, index - 128));
    }

    public static Vector4 Color(Vector3 cameraNormal, Vector3 depths, int corner)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(corner);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(corner, 2);
        int largest = depths.X < depths.Y ? 1 : 0;
        largest = depths[largest] < depths.Z ? 2 : largest;
        int other = largest == 1 ? 2 : 1;
        int nearest = largest == 0 ? depths.Y < depths.Z ? 1 : 2 : depths.X < depths[other] ? 0 : other;
        int middle = 3 - nearest - largest;
        float maximum = depths.Z;
        if (MathF.Round(depths.X - depths.Y, MidpointRounding.ToEven) < 0)
        {
            if (MathF.Round(depths.Y - depths.Z, MidpointRounding.ToEven) >= 0)
            {
                maximum = depths.Y;
            }
        }
        else if (MathF.Round(depths.X - depths.Z, MidpointRounding.ToEven) >= 0)
        {
            maximum = depths.X;
        }
        int delta = (int)MathF.Round(maximum, MidpointRounding.ToEven) - 16000;
        Vector3 palette;
        if (delta <= 0)
        {
            palette = Palette(corner == nearest ? 0 : corner == middle ? 4 : 8);
        }
        else
        {
            int step = (delta >> 3) & 0xFFC0;
            int index = step > 960 ? 255 : (step + 64) >> 2;
            palette = new(Palette(index).X, Palette(index - 4).Y, Palette(index - 8).Z);
        }
        float factor = cameraNormal.Y * (110f / 255) + 32f / 255;
        float Channel(float value) => MathF.Round(Math.Clamp(value * factor, 0, 255), MidpointRounding.ToEven) / 255;
        return new(Channel(palette.X), Channel(palette.Y), Channel(palette.Z), 1);
    }
}
