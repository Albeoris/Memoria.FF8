using System.Numerics;
using Albeoris.Games.FF8.Imaging;

namespace Memoria.FF8.Resources;

/// <summary>An sRGB lookup cube encoded as red across a cell, green down, and successive blue cells.</summary>
public sealed class ColorLookupTable
{
    private ColorLookupTable(PixelImage strip, int size)
    {
        Strip = strip;
        Size = size;
        for (int green = 0; green < size; green++)
        {
            for (int blue = 0; blue < size; blue++)
            {
                for (int red = 0; red < size; red++)
                {
                    Vector3 difference = Vector3.Abs(At(red, green, blue) - new Vector3(red, green, blue) / (size - 1));
                    if (Math.Max(difference.X, Math.Max(difference.Y, difference.Z)) > 1f / 255 + 1e-7f)
                    {
                        return;
                    }
                }
            }
        }
        IsIdentity = true;
    }

    public PixelImage Strip { get; }
    public int Size { get; }
    public bool IsIdentity { get; }

    public static ColorLookupTable FromStrip(PixelImage source)
    {
        ArgumentNullException.ThrowIfNull(source);
        int size = Math.Min(source.Width, source.Height);
        if (size is < 2 or > 65 || Math.Max(source.Width, source.Height) != size * size)
        {
            throw new InvalidDataException("A color lookup strip must contain 2 to 65 square slices arranged in a row or column.");
        }
        PixelImage strip = new(size * size, size);
        for (int green = 0; green < size; green++)
        {
            for (int blue = 0; blue < size; blue++)
            {
                for (int red = 0; red < size; red++)
                {
                    strip[blue * size + red, green] = source.Width > source.Height ? source[blue * size + red, green] : source[red, blue * size + green];
                }
            }
        }
        return new(strip, size);
    }

    public Vector3 Apply(Vector3 color)
    {
        Vector3 point = Vector3.Clamp(color, Vector3.Zero, Vector3.One) * (Size - 1);
        int blue = (int)point.Z;
        return Vector3.Lerp(SampleSlice(point.X, point.Y, blue), SampleSlice(point.X, point.Y, Math.Min(blue + 1, Size - 1)), point.Z - blue);
    }

    private Vector3 SampleSlice(float red, float green, int blue)
    {
        int left = (int)red;
        int top = (int)green;
        int right = Math.Min(left + 1, Size - 1);
        int bottom = Math.Min(top + 1, Size - 1);
        Vector3 upper = Vector3.Lerp(At(left, top, blue), At(right, top, blue), red - left);
        Vector3 lower = Vector3.Lerp(At(left, bottom, blue), At(right, bottom, blue), red - left);
        return Vector3.Lerp(upper, lower, green - top);
    }

    private Vector3 At(int red, int green, int blue)
    {
        Rgba32 value = Strip[blue * Size + red, green];
        return new Vector3(value.R, value.G, value.B) / 255;
    }
}

public readonly record struct SceneColorGrade(ColorLookupTable? First, ColorLookupTable? Second, float Mix, float Strength)
{
    public Vector3? LinearTint { get; init; }
    private bool HasLookup => Strength > 0 && (First is not null || Second is not null);
    public bool Enabled => LinearTint.HasValue || HasLookup;

    public Vector3 Apply(Vector3 color)
    {
        if (!Enabled)
        {
            return color;
        }
        Vector3 input = Vector3.Clamp(color, Vector3.Zero, Vector3.One);
        if (LinearTint is { } tint)
        {
            static float Linear(float value) => value <= 0.04045f ? value / 12.92f : MathF.Pow((value + 0.055f) / 1.055f, 2.4f);
            static float Srgb(float value)
            {
                value = Math.Clamp(value, 0, 1);
                return value <= 0.0031308f ? value * 12.92f : 1.055f * MathF.Pow(value, 1 / 2.4f) - 0.055f;
            }
            input = new(Srgb(Linear(input.X) * tint.X), Srgb(Linear(input.Y) * tint.Y), Srgb(Linear(input.Z) * tint.Z));
        }
        if (!HasLookup)
        {
            return input;
        }
        Vector3 result = First?.Apply(input) ?? input;
        if (Mix > 0)
        {
            result = Vector3.Lerp(result, Second?.Apply(input) ?? input, Mix);
        }
        return Vector3.Lerp(input, result, Strength);
    }

    public Rgba32 Apply(Rgba32 color)
    {
        if (!Enabled)
        {
            return color;
        }
        Vector3 result = Apply(new Vector3(color.R, color.G, color.B) / 255) * 255;
        return new((byte)MathF.Round(result.X), (byte)MathF.Round(result.Y), (byte)MathF.Round(result.Z), color.A);
    }
}
