using System.Buffers.Binary;

namespace Albeoris.Games.FF8.KernelBin.Internal;

/// <summary>
/// Reads and builds the kernel.bin header: a section count followed by one absolute
/// file offset per section. A section's length is derived from the distance to the next
/// section's offset (or to the end of the file, for the last section).
/// </summary>
internal static class KernelSectionTable
{
    public static Int32[] ReadOffsets(Byte[] content)
    {
        Int32 declaredCount = BinaryPrimitives.ReadInt32LittleEndian(content);
        if (declaredCount != KernelSections.Count)
            throw new InvalidDataException($"Unexpected kernel.bin section count: expected {KernelSections.Count}, found {declaredCount}.");

        Int32[] offsets = new Int32[KernelSections.Count];
        for (Int32 i = 0; i < KernelSections.Count; i++)
            offsets[i] = BinaryPrimitives.ReadInt32LittleEndian(content.AsSpan(4 + i * 4, 4));

        return offsets;
    }

    public static Int32 GetLength(Int32[] offsets, Int32 index, Int32 fileLength)
    {
        Int32 nextOffset = index == offsets.Length - 1 ? fileLength : offsets[index + 1];
        return nextOffset - offsets[index];
    }
}
