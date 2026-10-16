using System.Buffers.Binary;
using System.Text;

namespace Albeoris.Games.FF8.Msd.Internal;

/// <summary>Validates the offset table and decodes every offset-delimited text span.</summary>
internal static class MsdFileReader
{
    private const Int32 OffsetSize = sizeof(Int32);

    public static MsdFile Read(ReadOnlySpan<Byte> content, Encoding encoding)
    {
        if (content.IsEmpty)
        {
            return new MsdFile([], encoding);
        }

        if (content.Length < OffsetSize)
        {
            throw new InvalidDataException("The MSD file is too short to contain its first string offset.");
        }

        Int32 firstOffset = BinaryPrimitives.ReadInt32LittleEndian(content);
        if (firstOffset < OffsetSize || firstOffset % OffsetSize != 0)
        {
            throw new InvalidDataException(
                $"The first MSD string offset is {firstOffset}; it must be a positive multiple of {OffsetSize}.");
        }

        if (firstOffset > content.Length)
        {
            throw new InvalidDataException(
                $"The MSD header ends at offset {firstOffset}, beyond the {content.Length}-byte file.");
        }

        Int32 count = firstOffset / OffsetSize;
        Int32[] offsets = ReadOffsets(content[..firstOffset], count, firstOffset, content.Length);
        List<String> texts = new(count);

        for (Int32 index = 0; index < count; index++)
        {
            Int32 start = offsets[index];
            Int32 end = index + 1 < count ? offsets[index + 1] : content.Length;
            texts.Add(encoding.GetString(content[start..end]));
        }

        return new MsdFile(texts, encoding);
    }

    private static Int32[] ReadOffsets(
        ReadOnlySpan<Byte> header,
        Int32 count,
        Int32 firstOffset,
        Int32 fileLength)
    {
        Int32[] offsets = new Int32[count];
        Int32 previous = firstOffset;

        for (Int32 index = 0; index < count; index++)
        {
            Int32 offset = BinaryPrimitives.ReadInt32LittleEndian(
                header.Slice(index * OffsetSize, OffsetSize));

            if (offset < previous || offset > fileLength)
            {
                throw new InvalidDataException(
                    $"MSD string {index} starts at offset {offset}, outside its ordered range {previous}..{fileLength}.");
            }

            offsets[index] = offset;
            previous = offset;
        }

        return offsets;
    }
}
