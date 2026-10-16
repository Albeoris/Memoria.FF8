using System.Buffers.Binary;
using System.Text;
using Albeoris.Games.FF8.MngrpBin.Abstractions;

namespace Albeoris.Games.FF8.MngrpBin.Internal;

/// <summary>
/// Reads and writes the text-record layout, which spans two slots: the record slot holds fixed
/// eight-byte records (a 16-bit text offset plus six payload bytes) terminated by an all-zero
/// record, and the companion text slot holds each record's NUL-terminated text. Text offsets are
/// recalculated from the records on write.
/// </summary>
internal static class MngrpTextRecordCodec
{
    private const Int32 RecordLength = 8;

    public static MngrpTextRecordSection Read(Int32 slotIndex, Int32 textSlotIndex, ReadOnlySpan<Byte> recordBody, ReadOnlySpan<Byte> textBody, Encoding encoding)
    {
        MngrpTextRecordSection section = new(slotIndex, textSlotIndex);
        List<Int32> textOffsets = [];

        Int32 position = 0;
        while (position + RecordLength <= recordBody.Length)
        {
            ReadOnlySpan<Byte> record = recordBody.Slice(position, RecordLength);
            if (position > 0 && record.IndexOfAnyExcept((Byte)0) < 0)
            {
                break;
            }

            Int32 textOffset = BinaryPrimitives.ReadUInt16LittleEndian(record);
            if (textOffsets.Count > 0 && textOffset <= textOffsets[^1] || textOffset > textBody.Length)
            {
                throw new InvalidDataException($"The record at offset {position} has text offset {textOffset}, which is out of order or out of bounds.");
            }

            textOffsets.Add(textOffset);
            section.Records.Add(new MngrpTextRecord { Payload = record[2..].ToArray() });
            position += RecordLength;
        }

        section.TrailingData = MngrpFormat.TrimTrailingZeros(recordBody[position..]).ToArray();

        for (Int32 i = 0; i < section.Records.Count; i++)
        {
            Boolean isLast = i == section.Records.Count - 1;
            Int32 end = isLast ? textBody.Length : textOffsets[i + 1];
            MngrpTextEntry entry = MngrpTextSpanCodec.ReadEntry(textBody[textOffsets[i]..end], encoding, trimTrailingZeros: isLast);
            section.Records[i].Text = entry.Text ?? new MngrpText(String.Empty);
            section.Records[i].TextTrailingBytes = entry.TrailingBytes;
        }

        return section;
    }

    public static void WriteRecords(MngrpTextRecordSection section, Encoding encoding, MngrpByteWriter writer)
    {
        Int32 textPosition = 0;
        foreach (MngrpTextRecord record in section.Records)
        {
            writer.WriteUInt16(MngrpFormat.ToUInt16(textPosition, "record text offset"));
            writer.WriteBytes(record.Payload);
            textPosition += MngrpTextSpanCodec.GetBytes(record.Text, encoding).Length + record.TextTrailingBytes.Length;
        }

        writer.WriteBytes(section.TrailingData);
    }

    public static void WriteTexts(MngrpTextRecordSection section, Encoding encoding, MngrpByteWriter writer)
    {
        foreach (MngrpTextRecord record in section.Records)
        {
            writer.WriteBytes(MngrpTextSpanCodec.GetBytes(record.Text, encoding).Span);
            writer.WriteBytes(record.TextTrailingBytes);
        }
    }
}
