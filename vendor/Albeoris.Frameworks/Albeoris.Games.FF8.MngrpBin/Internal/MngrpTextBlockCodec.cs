using System.Buffers.Binary;
using System.Text;
using Albeoris.Games.FF8.MngrpBin.Abstractions;

namespace Albeoris.Games.FF8.MngrpBin.Internal;

/// <summary>
/// Reads and writes the text-block layout: a sequence of blocks, each three 16-bit link ids, a
/// 16-bit total length and a payload of NUL-terminated texts, aligned to four bytes and
/// terminated by a zero length field.
/// </summary>
internal static class MngrpTextBlockCodec
{
    private const Int32 BlockHeaderLength = 8;

    /// <summary>
    /// Parses the section and reports each block's byte offset within the body, which a
    /// <see cref="MngrpTextBlockMapSection"/> uses to resolve its references.
    /// </summary>
    public static MngrpTextBlockSection Read(Int32 slotIndex, ReadOnlySpan<Byte> body, Encoding encoding, out List<Int32> blockStarts)
    {
        MngrpTextBlockSection section = new(slotIndex);
        blockStarts = [];

        Int32 position = 0;
        while (position + BlockHeaderLength <= body.Length)
        {
            Int32 length = BinaryPrimitives.ReadUInt16LittleEndian(body.Slice(position + 6, 2));
            if (length == 0)
            {
                break;
            }

            if (length < BlockHeaderLength || position + length > body.Length)
            {
                throw new InvalidDataException($"The text block at offset {position} declares an invalid length of {length} bytes.");
            }

            MngrpTextBlock block = new()
            {
                OriginId = BinaryPrimitives.ReadUInt16LittleEndian(body.Slice(position, 2)),
                LeftId = BinaryPrimitives.ReadUInt16LittleEndian(body.Slice(position + 2, 2)),
                RightId = BinaryPrimitives.ReadUInt16LittleEndian(body.Slice(position + 4, 2)),
            };

            ReadOnlySpan<Byte> payload = body[(position + BlockHeaderLength)..(position + length)];
            while (!payload.IsEmpty)
            {
                Int32 terminator = payload.IndexOf((Byte)0);
                if (terminator < 0)
                {
                    block.TrailingBytes = payload.ToArray();
                    break;
                }

                block.Texts.Add(MngrpTextSpanCodec.ReadText(payload[..terminator], encoding));
                payload = payload[(terminator + 1)..];
            }

            section.Blocks.Add(block);
            blockStarts.Add(position);
            position = MngrpFormat.AlignToFour(position + length);
        }

        section.TrailingData = MngrpFormat.TrimTrailingZeros(body[position..]).ToArray();
        return section;
    }

    public static void Write(MngrpTextBlockSection section, Encoding encoding, MngrpByteWriter writer)
    {
        foreach (MngrpTextBlock block in section.Blocks)
        {
            writer.WriteUInt16(block.OriginId);
            writer.WriteUInt16(block.LeftId);
            writer.WriteUInt16(block.RightId);
            writer.WriteUInt16(MngrpFormat.ToUInt16(MeasureBlock(block, encoding), "text block length"));
            foreach (MngrpText text in block.Texts)
            {
                writer.WriteBytes(MngrpTextSpanCodec.GetBytes(text, encoding).Span);
                writer.WriteBytes([0]);
            }

            writer.WriteBytes(block.TrailingBytes);
            writer.PadToFour();
        }

        writer.WriteBytes(section.TrailingData);
    }

    /// <summary>Computes each block's byte offset as it will be laid out by <see cref="Write"/>.</summary>
    public static List<Int32> ComputeBlockStarts(MngrpTextBlockSection section, Encoding encoding)
    {
        List<Int32> blockStarts = new(section.Blocks.Count);
        Int32 position = 0;
        foreach (MngrpTextBlock block in section.Blocks)
        {
            blockStarts.Add(position);
            position = MngrpFormat.AlignToFour(position + MeasureBlock(block, encoding));
        }

        return blockStarts;
    }

    private static Int32 MeasureBlock(MngrpTextBlock block, Encoding encoding)
    {
        return BlockHeaderLength + block.Texts.Sum(text => MngrpTextSpanCodec.GetBytes(text, encoding).Length + 1) + block.TrailingBytes.Length;
    }
}
