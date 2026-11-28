using System.Buffers.Binary;
using System.Text;
using Albeoris.Games.FF8.MngrpBin.Abstractions;

namespace Albeoris.Games.FF8.MngrpBin.Internal;

/// <summary>
/// Reads and writes fixed eight-byte records at their physical indices. The companion slot
/// stores the occupied records' NUL-terminated texts.
/// </summary>
internal static class MngrpTextRecordCodec
{
    private const Int32 RecordLength = 8;

    public static MngrpTextRecordSection Read(Int32 slotIndex, Int32 textSlotIndex, ReadOnlySpan<Byte> recordBody, ReadOnlySpan<Byte> textBody, Encoding encoding)
    {
        MngrpTextRecordSection section = new(slotIndex, textSlotIndex);
        Int32 recordEnd = recordBody.Length - recordBody.Length % RecordLength;
        while (recordEnd > 0 && recordBody.Slice(recordEnd - RecordLength, RecordLength).IndexOfAnyExcept((Byte)0) < 0)
        {
            recordEnd -= RecordLength;
        }

        List<(MngrpTextRecord Record, Int32 Offset)> occupied = [];
        for (Int32 position = 0; position < recordEnd; position += RecordLength)
        {
            ReadOnlySpan<Byte> record = recordBody.Slice(position, RecordLength);
            if (record.IndexOfAnyExcept((Byte)0) < 0)
            {
                section.Records.Add(null);
                continue;
            }
            Int32 textOffset = BinaryPrimitives.ReadUInt16LittleEndian(record);
            if (occupied.Count > 0 && textOffset <= occupied[^1].Offset || textOffset > textBody.Length)
            {
                throw new InvalidDataException($"The record at offset {position} has text offset {textOffset}, which is out of order or out of bounds.");
            }

            MngrpTextRecord decoded = new() { Payload = record[2..].ToArray() };
            section.Records.Add(decoded);
            occupied.Add((decoded, textOffset));
        }

        section.TrailingData = MngrpFormat.TrimTrailingZeros(recordBody[recordEnd..]).ToArray();
        section.LeadingTextBytes = (occupied.Count == 0 ? MngrpFormat.TrimTrailingZeros(textBody) : textBody[..occupied[0].Offset]).ToArray();
        for (Int32 i = 0; i < occupied.Count; i++)
        {
            (MngrpTextRecord record, Int32 offset) = occupied[i];
            Boolean isLast = i == occupied.Count - 1;
            Int32 end = isLast ? textBody.Length : occupied[i + 1].Offset;
            MngrpTextEntry entry = MngrpTextSpanCodec.ReadEntry(textBody[offset..end], encoding, trimTrailingZeros: isLast);
            record.Text = entry.Text ?? new MngrpText(String.Empty);
            record.TextTrailingBytes = entry.TrailingBytes;
        }

        return section;
    }

    public static void WriteRecords(MngrpTextRecordSection section, Encoding encoding, MngrpByteWriter writer)
    {
        Int32 textPosition = section.LeadingTextBytes.Length;
        foreach (MngrpTextRecord? record in section.Records)
        {
            if (record is null)
            {
                writer.WriteZeros(RecordLength);
                continue;
            }
            writer.WriteUInt16(MngrpFormat.ToUInt16(textPosition, "record text offset"));
            writer.WriteBytes(record.Payload);
            textPosition += MngrpTextSpanCodec.GetBytes(record.Text, encoding).Length + record.TextTrailingBytes.Length;
        }

        writer.WriteBytes(section.TrailingData);
    }

    public static void WriteTexts(MngrpTextRecordSection section, Encoding encoding, MngrpByteWriter writer)
    {
        writer.WriteBytes(section.LeadingTextBytes);
        foreach (MngrpTextRecord? record in section.Records)
        {
            if (record is null)
            {
                continue;
            }
            writer.WriteBytes(MngrpTextSpanCodec.GetBytes(record.Text, encoding).Span);
            writer.WriteBytes(record.TextTrailingBytes);
        }
    }
}
