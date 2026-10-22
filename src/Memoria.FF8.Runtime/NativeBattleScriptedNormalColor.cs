namespace Memoria.FF8.Runtime;

/// <summary>4601B0's PC floating normal-light/color operation, including its float stores and register saturation.</summary>
public static class NativeBattleScriptedNormalColor
{
    public static NativeBattleScriptedColorRegisters Transform4601B0(NativeBattleScriptedLightingMemory lighting, NativeBattleVector normal, uint material, NativeBattleScriptedColorRegisters registers)
    {
        lighting.Validate();
        registers.Validate();
        NativeBattleScriptedLightingMatrix light = lighting.LightMatrix ?? throw new NotSupportedException("Native normal color requires its current light matrix controls.");
        NativeBattleScriptedLightingMatrix color = lighting.ColorMatrix ?? throw new NotSupportedException("Native normal color requires its current color matrix controls.");
        NativeBattleScriptedBackColor back = (lighting.BackColor ?? throw new NotSupportedException("Native normal color requires its current back-color controls.")).RequireKnown();
        var lightWords = light.Rotation;
        var colorWords = color.Rotation;
        float[] lit = new float[3];
        for (int row = 0; row < 3; row++)
        {
            double z = (float)(lightWords[row * 3 + 2] / 4096d) * (double)normal.Z;
            double y = (float)(lightWords[row * 3 + 1] / 4096d) * (double)normal.Y;
            double x = (float)(lightWords[row * 3] / 4096d) * (double)normal.X;
            float value = (float)((z + y) + x);
            lit[row] = BitConverter.SingleToInt32Bits(value) < 0 ? 0f : value;
        }
        int?[] mac = new int?[3];
        int?[] ir = new int?[3];
        double[] backgrounds = [back.Red, (float)back.Green, (float)back.Blue];
        uint[] irFlags = [0x81000000, 0x80800000, 0x00400000];
        uint[] rgbFlags = [0x00200000, 0x00100000, 0x00080000];
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
        return new([.. mac], [.. ir], [registers.Rgb[1], registers.Rgb[2], output], flags) { NormalInput = registers.NormalInput, NormalInputKnown = registers.NormalInputKnown, Material = material };
    }
}
