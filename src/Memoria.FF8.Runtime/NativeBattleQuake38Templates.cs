using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleQuake38Templates
{
    public static ImmutableArray<byte> Crack8D88C0(ImmutableArray<byte> retained)
    {
        byte[] result = Require(retained);
        BinaryPrimitives.WriteUInt32LittleEndian(result, 0x09000000);
        result[7] = 0x2E;
        result[12] = 0;
        result[13] = 0;
        result[20] = 31;
        result[21] = 0;
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(22), 0xB9);
        result[28] = 0;
        result[29] = 127;
        result[36] = 31;
        result[37] = 127;
        return result.ToImmutableArray();
    }

    public static ImmutableArray<byte> Wall8D88C0(ImmutableArray<byte> retained)
    {
        byte[] result = Require(retained);
        BinaryPrimitives.WriteUInt32LittleEndian(result, 0x09000000);
        result[7] = 0x2E;
        result[12] = 0;
        result[13] = 0;
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(14), 0x3C14);
        result[20] = 127;
        result[21] = 0;
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(22), 0xBA);
        result[28] = 0;
        result[29] = 255;
        result[36] = 127;
        result[37] = 255;
        return result.ToImmutableArray();
    }

    private static byte[] Require(ImmutableArray<byte> retained)
    {
        if (retained.IsDefault || retained.Length != 40)
        {
            throw new InvalidDataException("Native Quake requires its retained forty-byte FT4 template.");
        }
        return retained.ToArray();
    }
}
