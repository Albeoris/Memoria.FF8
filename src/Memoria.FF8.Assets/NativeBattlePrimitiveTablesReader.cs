using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattlePrimitiveTablesReader
{
    public static NativeBattlePrimitiveTables Read(byte[] executable)
    {
        ImmutableArray<byte> bytes = NativeBattleEffectStaticResourcesReader.Read(executable, 0xB7CC28, 0xEE0);
        return new(bytes.AsSpan(0, 256).ToArray().ToImmutableArray(), bytes.AsSpan(0x100, 256).ToArray().ToImmutableArray(), Words(0xAE0), Words(0x2E0), Words(0x6E0));

        ImmutableArray<uint> Words(int offset) => Enumerable.Range(0, 256).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + index * 4, 4))).ToImmutableArray();
    }
}
