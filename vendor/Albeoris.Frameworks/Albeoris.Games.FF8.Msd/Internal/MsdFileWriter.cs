using System.Buffers.Binary;
using System.Text;

namespace Albeoris.Games.FF8.Msd.Internal;

/// <summary>Encodes all texts and rebuilds their 32-bit offset table.</summary>
internal static class MsdFileWriter
{
    private const Int32 OffsetSize = sizeof(Int32);

    public static Byte[] Write(MsdFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        List<String> texts = file.Texts;
        Encoding encoding = file.Encoding;
        Byte[][] encodedTexts = new Byte[texts.Count][];

        Int64 length = checked((Int64)texts.Count * OffsetSize);
        for (Int32 index = 0; index < texts.Count; index++)
        {
            String text = texts[index]
                ?? throw new InvalidOperationException($"MSD text {index} is null.");
            encodedTexts[index] = encoding.GetBytes(text);
            length = checked(length + encodedTexts[index].Length);
        }

        if (length > Array.MaxLength)
        {
            throw new InvalidOperationException(
                $"The encoded MSD file is {length} bytes long and cannot be held in a byte array.");
        }

        Byte[] content = new Byte[(Int32)length];
        Int32 position = checked(texts.Count * OffsetSize);

        for (Int32 index = 0; index < encodedTexts.Length; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(
                content.AsSpan(index * OffsetSize, OffsetSize),
                position);

            Byte[] encodedText = encodedTexts[index];
            encodedText.CopyTo(content, position);
            position += encodedText.Length;
        }

        return content;
    }
}
