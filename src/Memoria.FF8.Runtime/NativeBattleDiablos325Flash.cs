using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleDiablos325Flash
{
    public static NativeBattleGfGeometryBatch Emit659A10(ushort counter)
    {
        if (counter is < 402 or > 413)
        {
            throw new ArgumentOutOfRangeException(nameof(counter));
        }
        int age = counter - 402;
        int ratio = age < 4 ? (age + 1) * 4096 / 4 : 4096 + (3 - age) * 4096 / 8;
        uint color = unchecked((byte)(255 * ratio >> 12));
        uint rgb = color | color << 8 | color << 16;
        ImmutableArray<uint> words = [0x05000000, 0x2A000000 | rgb, 0, 320, 216u << 16, 216u << 16 | 320];
        byte[] bytes = new byte[24];
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), words[index]);
        }
        NativeBattleGeometryPrimitive primitive = new(8, words, [0, 0, 0, 0]);
        return new([new(0, bytes.ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, bytes.Length).ToImmutableArray(), primitive)], bytes.Length);
    }
}
