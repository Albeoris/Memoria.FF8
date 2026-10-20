using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleRinoa89FadeDrawing
{
    public static int Factor6FC450(int age, int fadeIn, int hold, int fadeOut)
    {
        if (age < fadeIn)
        {
            return unchecked((age + 1) << 12) / (fadeIn == 0 ? 1 : fadeIn);
        }
        if (age < fadeIn + hold)
        {
            return 4096;
        }
        return unchecked(((fadeIn + hold - age) << 12) - 4096) / (fadeOut == 0 ? 1 : fadeOut) + 4096;
    }

    public static NativeBattleGfGeometryBatch Draw7040B0(int red, int green, int blue, int factor)
    {
        byte[] bytes = new byte[24];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x05000000);
        bytes[4] = unchecked((byte)(red * factor >> 12));
        bytes[5] = unchecked((byte)(green * factor >> 12));
        bytes[6] = unchecked((byte)(blue * factor >> 12));
        bytes[7] = 0x2A;
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12), 320);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(18), 216);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(20), 320);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(22), 216);
        ImmutableArray<uint> words = Enumerable.Range(0, 6).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 4))).ToImmutableArray();
        ImmutableArray<NativeBattleGfPacketByteState> mask = Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 24).ToImmutableArray();
        NativeBattleGeometryPrimitive primitive = new(8, words, [0, 0, 0, 0]) { PhysicalWriteMask = mask };
        return new([new(0, bytes.ToImmutableArray(), mask, primitive)], 24);
    }
}
