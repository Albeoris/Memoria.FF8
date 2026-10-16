using System.Text;
using Albeoris.Games.FF8.MngrpBin.Abstractions;

namespace Albeoris.Games.FF8.MngrpBin.Internal;

/// <summary>
/// Serializes a <see cref="MngrpArchive"/> back into a <c>mngrp.bin</c>/<c>mngrphd.bin</c> pair,
/// recalculating every offset and padding every section to the sector size.
/// </summary>
internal static class MngrpArchiveWriter
{
    public static MngrpFilePair Write(MngrpArchive archive)
    {
        Encoding encoding = archive.Encoding;
        IReadOnlyList<List<Int32>> blockStartsBySectionNumber = [.. archive
            .SectionsOfType<MngrpTextBlockSection>()
            .OrderBy(section => section.SlotIndex)
            .Select(section => MngrpTextBlockCodec.ComputeBlockStarts(section, encoding))];

        List<(Int32 SlotIndex, Byte[] Body)> bodies = [];
        foreach (IMngrpSection section in archive.Sections)
        {
            switch (section)
            {
                case MngrpOpaqueSection opaque:
                    bodies.Add((section.SlotIndex, Pad(opaque.Content, section.ReservedSize)));
                    break;

                case MngrpStringTableSection stringTable:
                    bodies.Add((section.SlotIndex, Serialize(writer => MngrpStringTableCodec.Write(stringTable.Table, encoding, writer), section.ReservedSize)));
                    break;

                case MngrpStringTableGroupSection group:
                    bodies.Add((section.SlotIndex, Serialize(writer => MngrpStringTableGroupCodec.Write(group, encoding, writer), section.ReservedSize)));
                    break;

                case MngrpTextBlockSection blocks:
                    bodies.Add((section.SlotIndex, Serialize(writer => MngrpTextBlockCodec.Write(blocks, encoding, writer), section.ReservedSize)));
                    break;

                case MngrpTextBlockMapSection map:
                    bodies.Add((section.SlotIndex, Serialize(writer => MngrpTextBlockMapCodec.Write(map, blockStartsBySectionNumber, writer), section.ReservedSize)));
                    break;

                case MngrpTextRecordSection records:
                    bodies.Add((section.SlotIndex, Serialize(writer => MngrpTextRecordCodec.WriteRecords(records, encoding, writer), section.ReservedSize)));
                    bodies.Add((records.TextSlotIndex, Serialize(writer => MngrpTextRecordCodec.WriteTexts(records, encoding, writer), records.TextReservedSize)));
                    break;

                default:
                    throw new NotSupportedException($"Slot {section.SlotIndex}: unsupported section type '{section.GetType().Name}'.");
            }
        }

        bodies.Sort((left, right) => left.SlotIndex.CompareTo(right.SlotIndex));

        Byte[] content = new Byte[bodies.Sum(body => body.Body.Length)];
        List<MngrpSlotLocation> occupied = new(bodies.Count);
        Int32 offset = 0;
        foreach ((Int32 slotIndex, Byte[] body) in bodies)
        {
            body.CopyTo(content, offset);
            occupied.Add(new MngrpSlotLocation(slotIndex, offset, body.Length));
            offset += body.Length;
        }

        return new MngrpFilePair(content, archive.SlotDirectory.Write(occupied));
    }

    private static Byte[] Serialize(Action<MngrpByteWriter> write, Int32 reservedSize)
    {
        MngrpByteWriter writer = new();
        write(writer);
        writer.PadTo(GetPaddedLength(writer.Length, reservedSize));
        return writer.ToArray();
    }

    private static Byte[] Pad(Byte[] body, Int32 reservedSize)
    {
        Int32 paddedLength = GetPaddedLength(body.Length, reservedSize);
        return paddedLength == body.Length ? body : [.. body, .. new Byte[paddedLength - body.Length]];
    }

    private static Int32 GetPaddedLength(Int32 contentLength, Int32 reservedSize)
    {
        return Math.Max(MngrpFormat.AlignToSector(contentLength), MngrpFormat.AlignToSector(reservedSize));
    }
}
