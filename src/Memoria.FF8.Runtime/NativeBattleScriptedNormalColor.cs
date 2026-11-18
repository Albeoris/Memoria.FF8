using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Memoria.FF8.Runtime;

/// <summary>4601B0's PC floating normal-light/color operation, including its float stores and register saturation.</summary>
public static class NativeBattleScriptedNormalColor
{
    internal sealed record PreparedLighting(ImmutableArray<short> Light, ImmutableArray<short> Color, int Red, int Green, int Blue);

    public static NativeBattleScriptedColorRegisters Transform4601B0(NativeBattleScriptedLightingMemory lighting, NativeBattleVector normal, uint material, NativeBattleScriptedColorRegisters registers)
    {
        lighting.Validate();
        registers.Validate();
        NativeBattleScriptedLightingMatrix light = lighting.LightMatrix ?? throw new NotSupportedException("Native normal color requires its current light matrix controls.");
        NativeBattleScriptedLightingMatrix color = lighting.ColorMatrix ?? throw new NotSupportedException("Native normal color requires its current color matrix controls.");
        NativeBattleScriptedBackColor back = (lighting.BackColor ?? throw new NotSupportedException("Native normal color requires its current back-color controls.")).RequireKnown();
        Span<short> lightWords = stackalloc short[9];
        light.CopyRotation(lightWords);
        Span<short> colorWords = stackalloc short[9];
        color.CopyRotation(colorWords);
        return TransformCore(lightWords, colorWords, back.Red, back.Green, back.Blue, normal, material, registers, registers.NormalInput, registers.NormalInputKnown);
    }

    internal static PreparedLighting Prepare(NativeBattleScriptedLightingMemory lighting)
    {
        lighting.Validate();
        NativeBattleScriptedLightingMatrix light = lighting.LightMatrix ?? throw new NotSupportedException("Native normal color requires its current light matrix controls.");
        NativeBattleScriptedLightingMatrix color = lighting.ColorMatrix ?? throw new NotSupportedException("Native normal color requires its current color matrix controls.");
        NativeBattleScriptedBackColor back = (lighting.BackColor ?? throw new NotSupportedException("Native normal color requires its current back-color controls.")).RequireKnown();
        short[] lightWords = new short[9];
        light.CopyRotation(lightWords);
        short[] colorWords = new short[9];
        color.CopyRotation(colorWords);
        return new(ImmutableCollectionsMarshal.AsImmutableArray(lightWords), ImmutableCollectionsMarshal.AsImmutableArray(colorWords), back.Red, back.Green, back.Blue);
    }

    internal static NativeBattleScriptedColorRegisters Transform4601B0(PreparedLighting lighting, NativeBattleVector normal, uint material, NativeBattleScriptedColorRegisters registers, ImmutableArray<byte> normalInput)
    {
        if (normalInput.IsDefault || normalInput.Length != 8)
        {
            throw new InvalidDataException("Native normal color input requires both actual packed V0 DWORDs.");
        }
        return TransformCore(lighting.Light.AsSpan(), lighting.Color.AsSpan(), lighting.Red, lighting.Green, lighting.Blue, normal, material, registers, normalInput, null);
    }

    private static NativeBattleScriptedColorRegisters TransformCore(ReadOnlySpan<short> lightWords, ReadOnlySpan<short> colorWords, int red, int green, int blue, NativeBattleVector normal, uint material, NativeBattleScriptedColorRegisters registers, ImmutableArray<byte>? normalInput, ImmutableArray<bool>? normalInputKnown)
    {
        Span<float> lit = stackalloc float[3];
        for (int row = 0; row < 3; row++)
        {
            double z = (float)(lightWords[row * 3 + 2] / 4096d) * (double)normal.Z;
            double y = (float)(lightWords[row * 3 + 1] / 4096d) * (double)normal.Y;
            double x = (float)(lightWords[row * 3] / 4096d) * (double)normal.X;
            float value = (float)((z + y) + x);
            lit[row] = BitConverter.SingleToInt32Bits(value) < 0 ? 0f : value;
        }
        Span<int> mac = stackalloc int[3];
        Span<int> ir = stackalloc int[3];
        ReadOnlySpan<double> backgrounds = stackalloc double[3] { red, (float)green, (float)blue };
        ReadOnlySpan<uint> irFlags = stackalloc uint[3] { 0x81000000, 0x80800000, 0x00400000 };
        ReadOnlySpan<uint> rgbFlags = stackalloc uint[3] { 0x00200000, 0x00100000, 0x00080000 };
        uint flags = 0;
        uint output = material & 0xFF000000u;
        for (int channel = 0; channel < 3; channel++)
        {
            double first = (float)(colorWords[channel * 3] / 4096d) * (double)lit[0] + backgrounds[channel];
            double second = first + (float)(colorWords[channel * 3 + 1] / 4096d) * (double)lit[1];
            double third = second + (float)(colorWords[channel * 3 + 2] / 4096d) * (double)lit[2];
            int value = NativePrecision53.RoundInteger(third * ((byte)(material >> (channel * 8)) / 256d));
            mac[channel] = value;
            ir[channel] = Math.Clamp(value, 0, 32767);
            if ((uint)value > 32767)
            {
                flags |= irFlags[channel];
            }
            int rgb = value >> 4;
            if ((uint)rgb > 255)
            {
                flags |= rgbFlags[channel];
            }
            output |= (uint)Math.Clamp(rgb, 0, 255) << (channel * 8);
        }
        return new([mac[0], mac[1], mac[2]], [ir[0], ir[1], ir[2]], [registers.Rgb[1], registers.Rgb[2], output], flags) { NormalInput = normalInput, NormalInputKnown = normalInputKnown, Material = material };
    }
}
