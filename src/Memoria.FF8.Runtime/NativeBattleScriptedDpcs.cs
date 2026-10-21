using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleScriptedDpcsResult(NativeBattleScriptedColorRegisters Registers, uint PackedRgb);

public static class NativeBattleScriptedDpcs
{
    public static NativeBattleScriptedDpcsResult Transform45F270(NativeBattleScriptedColorRegisters registers, uint source, uint cue, NativeBattleModelRendererRegisters far, NativeBattleSummonColors constants)
    {
        registers.Validate();
        if (constants.FixedScale != 1f / 4096f || BitConverter.DoubleToInt64Bits(constants.IntegerBias) != 0x4330000080000000L)
        {
            throw new NotSupportedException("Native depth color interpolation requires its authenticated fixed-scale and integer-conversion constants.");
        }
        int[] distant =
        [
            unchecked((int)(far.FarRed ?? throw new NotSupportedException("Native depth color interpolation requires its actual far-red control."))),
            unchecked((int)(far.FarGreen ?? throw new NotSupportedException("Native depth color interpolation requires its actual far-green control."))),
            unchecked((int)(far.FarBlue ?? throw new NotSupportedException("Native depth color interpolation requires its actual far-blue control.")))
        ];
        uint[] irFlags = [0x81000000, 0x80800000, 0x00400000];
        uint[] rgbFlags = [0x00200000, 0x00100000, 0x00080000];
        int?[] mac = new int?[3];
        int?[] ir = new int?[3];
        uint flags = 0;
        uint output = source & 0xFF000000;
        double fraction = unchecked((short)cue) * (double)constants.FixedScale;
        for (int lane = 0; lane < 3; lane++)
        {
            int initial = (byte)(source >> (lane * 8)) << 4;
            int delta = unchecked(distant[lane] - initial);
            int value = unchecked(NativePrecision53.RoundInteger(delta * fraction) + initial);
            mac[lane] = value;
            ir[lane] = Math.Clamp(value, short.MinValue, short.MaxValue);
            if (value < short.MinValue || value > short.MaxValue)
            {
                flags |= irFlags[lane];
            }
            int visible = value >> 4;
            if ((uint)visible > 255)
            {
                flags |= rgbFlags[lane];
            }
            output |= (uint)Math.Clamp(visible, 0, 255) << (lane * 8);
        }
        NativeBattleScriptedColorRegisters result = registers with
        {
            Material = source,
            Ir0 = cue,
            Mac = mac.ToImmutableArray(),
            Ir = ir.ToImmutableArray(),
            Rgb = [registers.Rgb[1], registers.Rgb[2], output],
            Flags = flags
        };
        return new(result, output);
    }
}
