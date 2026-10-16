using System.Buffers.Binary;
using Albeoris.Games.FF8.MngrpBin.Abstractions;

namespace Albeoris.Games.FF8.MngrpBin.Internal;

/// <summary>
/// Reads and writes the text-block-map layout: a 32-bit reference count followed by one
/// (16-bit block offset, 16-bit section number) pair per reference. Offsets are resolved to
/// block indexes on read and recalculated from the referenced sections on write; a stored
/// offset that matches no block start (a shipped-file corruption) is carried through verbatim.
/// </summary>
internal static class MngrpTextBlockMapCodec
{
    public static MngrpTextBlockMapSection Read(Int32 slotIndex, ReadOnlySpan<Byte> body, IReadOnlyList<List<Int32>> blockStartsBySectionNumber)
    {
        if (body.Length < 4)
        {
            throw new InvalidDataException("A text block map is too short to hold its reference count.");
        }

        UInt32 count = BinaryPrimitives.ReadUInt32LittleEndian(body);
        Int32 headerLength = checked(4 + (Int32)count * 4);
        if (count > Int32.MaxValue / 4 || headerLength > body.Length)
        {
            throw new InvalidDataException($"A text block map declares {count} references but is only {body.Length} bytes long.");
        }

        MngrpTextBlockMapSection section = new(slotIndex);
        for (Int32 i = 0; i < count; i++)
        {
            UInt16 storedOffset = BinaryPrimitives.ReadUInt16LittleEndian(body.Slice(4 + i * 4, 2));
            UInt16 sectionNumber = BinaryPrimitives.ReadUInt16LittleEndian(body.Slice(6 + i * 4, 2));
            section.References.Add(new MngrpTextBlockReference(sectionNumber, ResolveBlockIndex(sectionNumber, storedOffset, blockStartsBySectionNumber), storedOffset));
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
            return null;
        }

        Int32 blockIndex = blockStartsBySectionNumber[sectionNumber].BinarySearch(storedOffset);
        return blockIndex >= 0 ? blockIndex : null;
    }

    private static UInt16 GetBlockOffset(MngrpTextBlockReference reference, IReadOnlyList<List<Int32>> blockStartsBySectionNumber)
    {
        if (reference.BlockIndex is not Int32 blockIndex)
        {
            return reference.StoredOffset;
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
