using System.Buffers.Binary;
using System.Text;

namespace Albeoris.Games.FF8.NamedicBin;

/// <summary>
/// Reads and writes the string table stored in Final Fantasy VIII's <c>namedic.bin</c> file.
/// </summary>
public static class NamedicBin
{
    private const Int32 CountSize = sizeof(UInt16);
    private const Int32 OffsetSize = sizeof(UInt16);

    /// <summary>Reads all null-terminated strings from a <c>namedic.bin</c> file.</summary>
    /// <param name="content">The complete file content.</param>
    /// <param name="encoding">The encoding used by the file localization.</param>
    /// <returns>The strings in the order in which they occur in the file.</returns>
    /// <exception cref="InvalidDataException">The content does not have a valid contiguous string-table layout.</exception>
    public static String[] ReadStrings(Byte[] content, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(encoding);

        if (content.Length < CountSize)
        {
            throw new InvalidDataException("The namedic file is too short to contain a string count.");
        }

        UInt16 count = BinaryPrimitives.ReadUInt16LittleEndian(content);
        Int32 headerSize = CountSize + count * OffsetSize;
        if (content.Length < headerSize)
        {
            throw new InvalidDataException($"The namedic file declares {count} strings but is only {content.Length} bytes long.");
        }

        String[] values = new String[count];
        Int32 position = headerSize;
        for (Int32 index = 0; index < count; index++)
        {
            Int32 offsetPosition = CountSize + index * OffsetSize;
            UInt16 offset = BinaryPrimitives.ReadUInt16LittleEndian(content.AsSpan(offsetPosition, OffsetSize));
            if (offset != position)
            {
                throw new InvalidDataException($"String {index} starts at offset {offset}, but the next contiguous offset is {position}.");
            }

            Int32 terminatorOffset = content.AsSpan(position).IndexOf((Byte)0);
            if (terminatorOffset < 0)
            {
                throw new InvalidDataException($"String {index} is not null-terminated.");
            }

            values[index] = encoding.GetString(content, position, terminatorOffset);
            position += terminatorOffset + 1;
        }

        if (position != content.Length)
        {
            throw new InvalidDataException($"The string table ends at offset {position}, but the file is {content.Length} bytes long.");
        }

        return values;
    }

    /// <summary>Writes strings to the contiguous, null-terminated <c>namedic.bin</c> format.</summary>
    /// <param name="values">The strings to write.</param>
    /// <param name="encoding">The encoding used by the target localization.</param>
    /// <returns>The complete serialized file content.</returns>
    /// <exception cref="ArgumentException">A value contains a null byte after encoding.</exception>
    /// <exception cref="InvalidDataException">The number of values or a string offset cannot be represented by the format.</exception>
    public static Byte[] WriteStrings(String[] values, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(encoding);

        if (values.Length > UInt16.MaxValue)
            throw new InvalidDataException($"The namedic format cannot contain more than {UInt16.MaxValue} strings.");

        Byte[][] encodedValues = new Byte[values.Length][];
        Int32 contentLength = CountSize + values.Length * OffsetSize;
        for (Int32 index = 0; index < values.Length; index++)
        {
            String value = values[index] ?? throw new ArgumentException($"String {index} is null.", nameof(values));
            Byte[] encodedValue = encoding.GetBytes(value);

            encodedValues[index] = encodedValue;
            contentLength = checked(contentLength + encodedValue.Length + 1);
        }

        Byte[] content = new Byte[contentLength];
        BinaryPrimitives.WriteUInt16LittleEndian(content, (UInt16)values.Length);

        Int32 position = CountSize + values.Length * OffsetSize;
        for (Int32 index = 0; index < encodedValues.Length; index++)
        {
            if (position > UInt16.MaxValue)
                throw new InvalidDataException($"String {index} starts at offset {position}, which cannot be represented by the namedic format.");

            Int32 offsetPosition = CountSize + index * OffsetSize;
            BinaryPrimitives.WriteUInt16LittleEndian(content.AsSpan(offsetPosition, OffsetSize), (UInt16)position);

            Byte[] encodedValue = encodedValues[index];
            encodedValue.CopyTo(content, position);
            position += encodedValue.Length + 1;
        }

        return content;
    }
}
