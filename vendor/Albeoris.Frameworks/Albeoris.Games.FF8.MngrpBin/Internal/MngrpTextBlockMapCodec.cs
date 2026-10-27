using System.Buffers.Binary;
using Albeoris.Games.FF8.MngrpBin.Abstractions;

namespace Albeoris.Games.FF8.MngrpBin.Internal;

/// <summary>
/// Reads and writes the text-block-map layout: a 32-bit reference count followed by one
/// (16-bit block offset, 16-bit section number) pair per reference. Offsets are resolved to
/// block indexes on read and recalculated from the referenced sections on write; every stored
/// offset must resolve to a complete native Information node.
/// </summary>
internal static class MngrpTextBlockMapCodec
{
    public static MngrpTextBlockMapSection Read(Int32 slotIndex, ReadOnlySpan<Byte> body, IReadOnlyList<List<Int32>> blockStartsBySectionNumber, Boolean resolveReferences = true)
    {
        if (body.Length < 4)
        {
            throw new InvalidDataException("A text block map is too short to hold its reference count.");
        }

        UInt32 count = BinaryPrimitives.ReadUInt32LittleEndian(body);
        if (count > (body.Length - 4) / 4)
        {
            throw new InvalidDataException($"A text block map declares {count} references but is only {body.Length} bytes long.");
        }

        Int32 headerLength = 4 + (Int32)count * 4;
        MngrpTextBlockMapSection section = new(slotIndex);
        for (Int32 i = 0; i < count; i++)
        {
            UInt16 storedOffset = BinaryPrimitives.ReadUInt16LittleEndian(body.Slice(4 + i * 4, 2));
            UInt16 sectionNumber = BinaryPrimitives.ReadUInt16LittleEndian(body.Slice(6 + i * 4, 2));
            section.References.Add(new MngrpTextBlockReference(sectionNumber, resolveReferences ? ResolveBlockIndex(sectionNumber, storedOffset, blockStartsBySectionNumber) : null, storedOffset));
        }

        section.TrailingData = MngrpFormat.TrimTrailingZeros(body[headerLength..]).ToArray();
        return section;
    }

    public static void Write(MngrpTextBlockMapSection section, IReadOnlyList<List<Int32>> blockStartsBySectionNumber, MngrpByteWriter writer)
    {
        writer.WriteUInt32((UInt32)section.References.Count);
        foreach (MngrpTextBlockReference reference in section.References)
        {
            writer.WriteUInt16(GetBlockOffset(reference, blockStartsBySectionNumber));
            writer.WriteUInt16(MngrpFormat.ToUInt16(reference.SectionNumber, "text block section number"));
        }

        writer.WriteBytes(section.TrailingData);
    }

    private static Int32? ResolveBlockIndex(Int32 sectionNumber, UInt16 storedOffset, IReadOnlyList<List<Int32>> blockStartsBySectionNumber)
    {
        if (sectionNumber >= blockStartsBySectionNumber.Count)
        {
            throw new InvalidDataException("An Information map selects an absent text resource.");
        }

        Int32 blockIndex = blockStartsBySectionNumber[sectionNumber].BinarySearch(storedOffset);
        return blockIndex >= 0 ? blockIndex : throw new InvalidDataException("An Information map offset does not address a complete native node.");
    }

    private static UInt16 GetBlockOffset(MngrpTextBlockReference reference, IReadOnlyList<List<Int32>> blockStartsBySectionNumber)
    {
        if (reference.BlockIndex is not Int32 blockIndex)
        {
            throw new InvalidOperationException("An Information map reference must resolve to a native node before writing.");
        }

        if (reference.SectionNumber >= blockStartsBySectionNumber.Count)
        {
            throw new InvalidOperationException($"A text block reference targets section number {reference.SectionNumber}, but the archive has no such text-block section.");
        }

        List<Int32> blockStarts = blockStartsBySectionNumber[reference.SectionNumber];
        if (blockIndex < 0 || blockIndex >= blockStarts.Count)
        {
            throw new InvalidOperationException($"A text block reference targets block {blockIndex} of section number {reference.SectionNumber}, which holds only {blockStarts.Count} blocks.");
        }

        return MngrpFormat.ToUInt16(blockStarts[blockIndex], "text block offset");
    }
}
