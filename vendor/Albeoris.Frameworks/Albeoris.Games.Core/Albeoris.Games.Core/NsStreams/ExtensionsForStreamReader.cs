using System.Runtime.CompilerServices;
using System.Text;

namespace Albeoris.Games.Core.NsStreams;

public static class ExtensionsForStreamReader
{
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_charBuffer")]
    private static extern ref Char[] GetCharBuffer(StreamReader reader);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_charPos")]
    private static extern ref Int32 GetCharPos(StreamReader reader);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_charLen")]
    private static extern ref Int32 GetCharLen(StreamReader reader);

    /// <summary>
    /// Gets the actual binary position in the underlying stream, accounting for buffered data in StreamReader.
    /// </summary>
    /// <remarks>
    /// StreamReader buffers characters internally, so the underlying stream position is ahead of the actual read position.
    /// This method calculates the real position by subtracting the byte count of buffered characters.
    /// </remarks>
    /// <param name="streamReader">The StreamReader instance.</param>
    /// <returns>The binary position in the underlying stream.</returns>
    public static Int64 GetBinaryPosition(this StreamReader streamReader)
    {
        ArgumentNullException.ThrowIfNull(streamReader);

        Stream stream = streamReader.BaseStream;
        Encoding encoding = streamReader.CurrentEncoding;

        Char[] charBuffer = GetCharBuffer(streamReader);
        Int32 charPos = GetCharPos(streamReader);
        Int32 charLen = GetCharLen(streamReader);

        // Calculate how many characters are still buffered and not yet consumed
        Int32 bufferedCharCount = charLen - charPos;

        if (bufferedCharCount <= 0)
            return stream.Position;

        // Calculate the byte count of the buffered characters
        // Optimization: if encoding uses 1 byte per character, no need to recalculate
        Int32 bufferedByteCount = encoding.GetMaxByteCount(1) == 1
            ? bufferedCharCount
            : encoding.GetByteCount(charBuffer.AsSpan(charPos, bufferedCharCount));

        // The binary position is the current stream position minus the buffered bytes
        return stream.Position - bufferedByteCount;
    }

    public static IReadOnlyList<String> ReadAllLines(this StreamReader streamReader)
    {
        ArgumentNullException.ThrowIfNull(streamReader);

        List<String> result = new();

        while (!streamReader.EndOfStream)
        {
            String? line = streamReader.ReadLine();
            if (line is null)
                continue;

            result.Add(line);
        }

        return result;
    }
}