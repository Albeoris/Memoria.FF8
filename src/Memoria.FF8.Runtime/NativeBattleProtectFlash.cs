using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleProtectFlash
{
    public static void Emit(uint color, Action<NativeBattleProtectPackets> emit)
    {
        emit(Packet([0x05000000, 0x2A000000 | (color & 0xFFFFFF), 0, 320, 216u << 16, (216u << 16) | 320], 1));
        emit(Packet([0x02000000, 0xE100002A, 0], 0));
    }

    private static NativeBattleProtectPackets Packet(ImmutableArray<uint> words, ushort flags)
    {
        byte[] bytes = new byte[words.Length * 4];
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), words[index]);
        }
        NativeBattleGeometryPrimitive primitive = new(7, words, [0, 0, 0, 0]);
        NativeBattleGfGeometryAttempt attempt = new(0, bytes.ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, bytes.Length).ToImmutableArray(), primitive);
        return new(new([attempt], bytes.Length), flags);
    }
}
