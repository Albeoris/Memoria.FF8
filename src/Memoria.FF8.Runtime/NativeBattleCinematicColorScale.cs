using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicColorScaleResult(uint Color, NativeBattleScriptedColorRegisters Registers);

public static class NativeBattleCinematicColorScale
{
    public static NativeBattleCinematicColorScaleResult ScaleB66A20(uint command, short factor, NativeBattleScriptedColorRegisters registers)
    {
        return ScaleRawIr0(command, unchecked((short)(factor << 4)), registers);
    }

    public static NativeBattleCinematicColorScaleResult ScaleRawIr0(uint command, short ir0, NativeBattleScriptedColorRegisters registers)
    {
        registers.Validate();
        double coefficient = ir0 * (double)(1f / 4096f);
        int[] mac = new int[3];
        int[] ir = new int[3];
        uint flags = 0;
        uint returned = command & 0xFF000000;
        uint rgb = 0;
        uint[] irFlags = [0x81000000, 0x80800000, 0x00400000];
        uint[] rgbFlags = [0x00200000, 0x00100000, 0x00080000];
        for (int lane = 0; lane < 3; lane++)
        {
            mac[lane] = NativePrecision53.RoundInteger((byte)(command >> (lane * 8)) * coefficient);
            ir[lane] = Math.Clamp(mac[lane], short.MinValue, short.MaxValue);
            if (ir[lane] != mac[lane])
            {
                flags |= irFlags[lane];
            }
            if ((uint)mac[lane] > 255)
            {
                flags |= rgbFlags[lane];
            }
            returned |= (uint)unchecked((byte)ir[lane]) << (lane * 8);
            rgb |= (uint)Math.Clamp(mac[lane], 0, 255) << (lane * 8);
        }
        uint? fifo = registers.Material is uint material ? rgb | (material & 0xFF000000) : null;
        NativeBattleScriptedColorRegisters result = registers with { Mac = mac.Select(value => (int?)value).ToImmutableArray(), Ir = ir.Select(value => (int?)value).ToImmutableArray(), Rgb = [registers.Rgb[1], registers.Rgb[2], fifo], Flags = flags };
        return new(returned, result);
    }
}
