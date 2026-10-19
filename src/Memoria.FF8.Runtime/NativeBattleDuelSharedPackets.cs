using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleDuelSharedPackets
{
    public static uint InterpolateColor56CC00(uint first, uint second, int firstWeight, int secondWeight, uint destination)
    {
        uint result = destination & 0xFF000000;
        for (int channel = 0; channel < 3; channel++)
        {
            int shift = channel * 8;
            int a = (byte)(first >> shift);
            int b = (byte)(second >> shift);
            int mac = unchecked(NativePrecision53.RoundInteger(a * (unchecked((short)firstWeight) / 4096.0)) + NativePrecision53.RoundInteger(b * (unchecked((short)secondWeight) / 4096.0)));
            result |= unchecked((uint)(byte)Math.Clamp(mac, short.MinValue, short.MaxValue)) << shift;
        }
        return result;
    }

    public static NativeBattleModulePacketBatch Flash6CBF90()
    {
        NativeBattleGeometryPrimitive before = new(8, [0x01000000, 0xE1000020], []) { PhysicalWriteMask = Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 8).ToImmutableArray() };
        NativeBattleGeometryPrimitive flash = new(8, [0x05000000, 0x2A808080, 0, 320, 216u << 16, 216u << 16 | 320], [0, 0, 0, 0]) { PhysicalWriteMask = Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 24).ToImmutableArray() };
        NativeBattleGeometryPrimitive after = new(8, [0x01000000, 0xE1000040], []) { PhysicalWriteMask = Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 8).ToImmutableArray() };
        return new([new(before, 24, 1), new(flash, 0, 1), new(after, 32, 1)], 40);
    }

    public static ImmutableArray<short> Rotation6D9510(short angle)
    {
        short sine = unchecked((short)NativeBattleCameraMath.Sine(angle));
        short cosine = unchecked((short)NativeBattleCameraMath.Cosine(angle));
        return [cosine, 0, sine, 0, 4096, 0, unchecked((short)-sine), 0, cosine];
    }

    public static NativeBattleModulePacketBatch Flash6A83B0(int red, int green, int blue, int phase, int duration)
    {
        int remaining = unchecked(duration - phase);
        if (remaining <= 0)
        {
            return new([], 0);
        }
        byte r = unchecked((byte)(unchecked(remaining * red) / duration));
        byte g = unchecked((byte)(unchecked(remaining * green) / duration));
        byte b = unchecked((byte)(unchecked(remaining * blue) / duration));
        uint color = (uint)(r | g << 8 | b << 16);
        NativeBattleGeometryPrimitive primitive = new(8, [0x05000000, 0x2A000000 | color, 0, 320, 216u << 16, 216u << 16 | 320], [0, 0, 0, 0])
        {
            PhysicalWriteMask = Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 24).ToImmutableArray()
        };
        return new([new(primitive, 0, 1)], 24);
    }
}
