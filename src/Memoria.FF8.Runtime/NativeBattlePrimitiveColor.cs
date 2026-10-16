using System.Numerics;

namespace Memoria.FF8.Runtime;

public readonly record struct NativeBattlePrimitiveColor(Vector3 Rgb, byte Alpha);

/// <summary>PC diffuse-color dispatch B7CEE8, including the retained460E80 subtraction selector.</summary>
public sealed record NativeBattlePrimitiveColorState(bool SubtractUsesNormalizedColor)
{
    public byte? AlternatePaletteShift { get; init; }

    public static NativeBattlePrimitiveColorState FromColdProcess() => new(false) { AlternatePaletteShift = 0 };

    public NativeBattlePrimitiveColor Untextured(uint rgb, bool semi, int blend)
    {
        byte red = (byte)rgb;
        byte green = (byte)(rgb >> 8);
        byte blue = (byte)(rgb >> 16);
        if (blend == 2 && !SubtractUsesNormalizedColor)
        {
            byte alpha = (byte)Math.Min((red + green + blue) >> 1, 255);
            return new(new(red == 0 && green != 0 && green == blue ? 255 : 0, green == 0 && red != 0 && red == blue ? 255 : 0, blue == 0 && red != 0 && red == green ? 255 : 0), alpha);
        }
        return new(new(red, green, blue), blend == 0 ? (byte)127 : (byte)255);
    }

    public NativeBattlePrimitiveColor Textured(uint rgb, bool semi, int blend, bool rawTexture)
    {
        Vector3 input = new((byte)rgb, (byte)(rgb >> 8), (byte)(rgb >> 16));
        Vector3 output;
        byte alpha;
        if (semi && blend == 2 && !SubtractUsesNormalizedColor)
        {
            output = Vector3.Zero;
            alpha = (byte)Math.Clamp(((byte)rgb + (byte)(rgb >> 8) + (byte)(rgb >> 16)) >> 1, 70, 110);
        }
        else if (semi && blend == 3)
        {
            output = new((byte)rgb >> 1, (byte)(rgb >> 8) >> 1, (byte)(rgb >> 16) >> 1);
            alpha = 255;
        }
        else
        {
            float maximum = Math.Max(input.X, Math.Max(input.Y, input.Z));
            output = maximum <= 128 ? new(Channel((byte)rgb), Channel((byte)(rgb >> 8)), Channel((byte)(rgb >> 16))) : new(Normalize((byte)rgb, maximum), Normalize((byte)(rgb >> 8), maximum), Normalize((byte)(rgb >> 16), maximum));
            alpha = semi && blend == 0 ? (byte)127 : (byte)255;
        }
        return new(rawTexture ? new(255) : output, alpha);

        static int Channel(byte value) => value == 128 ? 255 : value * 2;
        static int Normalize(byte value, float maximum) => (int)Math.Round(value * 255d / maximum, MidpointRounding.ToEven);
    }
}
