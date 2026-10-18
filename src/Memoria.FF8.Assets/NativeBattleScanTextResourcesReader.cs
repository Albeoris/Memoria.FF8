using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleScanTextResourcesReader
{
    public static NativeBattleScanCameraProfile ReadCameraProfile(IGameNativeMemorySource memory, ushort category)
    {
        ArgumentNullException.ThrowIfNull(memory);
        uint address = unchecked(0x15D0B40u + (uint)(unchecked((short)category) * 16));
        return new(category, address, memory.Read(address, 16).ToImmutableArray());
    }

    public static NativeBattleScanTextResources Read(IGameNativeMemorySource memory, byte[] kernel)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(kernel);
        if (kernel.Length < 228 || BinaryPrimitives.ReadInt32LittleEndian(kernel) != 56)
        {
            throw new InvalidDataException("Native Scan result text requires the complete original56-section kernel directory.");
        }
        int miscDirectory = BinaryPrimitives.ReadInt32LittleEndian(kernel.AsSpan(4 + 30 * 4));
        int miscDirectoryEnd = BinaryPrimitives.ReadInt32LittleEndian(kernel.AsSpan(4 + 31 * 4));
        int miscText = BinaryPrimitives.ReadInt32LittleEndian(kernel.AsSpan(4 + 55 * 4));
        if (miscDirectory < 228 || miscDirectoryEnd < miscDirectory || miscDirectoryEnd > kernel.Length || (miscDirectoryEnd - miscDirectory) % 2 != 0 || miscText < miscDirectoryEnd || miscText > kernel.Length)
        {
            throw new InvalidDataException("Native Scan miscellaneous text directory or raw text bank leaves its kernel sections.");
        }
        ImmutableArray<ushort> miscOffsets = Enumerable.Range(0, (miscDirectoryEnd - miscDirectory) / 2).Select(index => BinaryPrimitives.ReadUInt16LittleEndian(kernel.AsSpan(miscDirectory + index * 2, 2))).ToImmutableArray();
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
        NativeBattleScanTextResources resources = new(offsets, descriptions.ToImmutableArray())
        {
            KernelMiscOffsets = miscOffsets,
            KernelMiscBytes = kernel.AsSpan(miscText).ToArray().ToImmutableArray(),
            KernelMiscDirectoryFileOffset = miscDirectory,
            KernelMiscTextFileOffset = miscText
        };
        for (int id = 0; id < miscOffsets.Length; id++)
        {
            resources.ReadEncodedMiscText(id);
        }
        return resources;
    }
}
