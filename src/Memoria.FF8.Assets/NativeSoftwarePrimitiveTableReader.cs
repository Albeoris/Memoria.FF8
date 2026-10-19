using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeSoftwarePrimitiveTableReader
{
    public static NativeSoftwarePrimitiveTable Read(byte[] executable)
    {
        return Read(new NativeExecutableMemory(executable));
    }

    public static NativeSoftwarePrimitiveTable Read(IGameNativeMemorySource memory)
    {
        byte[] bytes = memory.Read(0xB7DC18, 256 * 4);
        return new(Enumerable.Range(0, 256).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 4, 4))).ToImmutableArray());
    }
}

public sealed partial class BattleAssets
{
    private NativeSoftwarePrimitiveTable? nativeSoftwarePrimitiveTable;
    public NativeSoftwarePrimitiveTable ReadNativeSoftwarePrimitiveTable() => nativeSoftwarePrimitiveTable ??= NativeSoftwarePrimitiveTableReader.Read(GameDataResources.ReadNativeMemory(this.resources));
}
