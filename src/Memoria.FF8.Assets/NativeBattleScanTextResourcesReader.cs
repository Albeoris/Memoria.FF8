using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleScanTextResourcesReader
{
    public static NativeBattleScanTextResources Read(IGameNativeMemorySource memory)
    {
        ArgumentNullException.ThrowIfNull(memory);
        byte[] directory = memory.Read(0x1887474, 320);
        byte[] descriptions = memory.Read(0x18875B4, 0x3A5F);
        ImmutableArray<ushort> offsets = Enumerable.Range(0, 160).Select(index => BinaryPrimitives.ReadUInt16LittleEndian(directory.AsSpan(index * 2, 2))).ToImmutableArray();
        foreach (ushort offset in offsets)
        {
            if (offset >= descriptions.Length || descriptions.AsSpan(offset).IndexOf((byte)0) < 0)
            {
                throw new InvalidDataException("Original Scan description directory lacks a bounded terminated entry.");
            }
        }
        return new(offsets, descriptions.ToImmutableArray());
    }
}
