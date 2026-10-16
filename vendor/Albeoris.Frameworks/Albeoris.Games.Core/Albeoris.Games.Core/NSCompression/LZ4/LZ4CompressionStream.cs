namespace Albeoris.Games.Core.NSCompression.LZ4;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// A stream that compresses data using the raw LZ4 block format.
/// The entire input is buffered and, upon flush or disposal, compressed into a single raw LZ4 block.
/// The output format is compatible with LLxx.LZ4
/// </summary>
public class LZ4CompressionStream : Stream
{
    private readonly Stream _baseStream;
    private readonly Boolean _leaveOpen;
    private readonly List<Byte> _inputBuffer = new();
    private Boolean _isFlushed;

    /// <summary>
    /// Initializes a new instance of the <see cref="LZ4CompressionStream"/> class.
    /// </summary>
    /// <param name="baseStream">The underlying stream to write compressed data to.</param>
    /// <param name="leaveOpen">If true, leaves the underlying stream open when this stream is closed.</param>
    public LZ4CompressionStream(Stream baseStream, Boolean leaveOpen)
    {
        _baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
        if (!_baseStream.CanWrite)
            throw new ArgumentException("Base stream must be writable.", nameof(baseStream));
        _leaveOpen = leaveOpen;
    }

    public override Boolean CanRead => false;
    public override Boolean CanSeek => false;
    public override Boolean CanWrite => true;
    public override Int64 Length => throw new NotSupportedException();

    public override Int64 Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
        FlushBlock();
        _baseStream.Flush();
    }

    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        FlushBlock();
        await _baseStream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public override Int32 Read(Byte[] buffer, Int32 offset, Int32 count)
    {
        throw new NotSupportedException();
    }

    public override void Write(Byte[] buffer, Int32 offset, Int32 count)
    {
        if (buffer == null)
            throw new ArgumentNullException(nameof(buffer));
        if (offset < 0 || offset > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(offset));
        if (count < 0 || offset + count > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(count));

        for (Int32 i = offset; i < offset + count; i++)
            _inputBuffer.Add(buffer[i]);
        _isFlushed = false;
    }

    public override async Task WriteAsync(Byte[] buffer, Int32 offset, Int32 count, CancellationToken cancellationToken)
    {
        Write(buffer, offset, count);
        await Task.CompletedTask;
    }

    public override Int64 Seek(Int64 offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(Int64 value) => throw new NotSupportedException();

    protected override void Dispose(Boolean disposing)
    {
        if (disposing)
        {
            try
            {
                Flush();
            }
            catch
            {
                // Ignore exceptions on flush during dispose.
            }

            if (!_leaveOpen)
                _baseStream.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Compresses the buffered input data using LZ4 and writes the raw block to the underlying stream.
    /// </summary>
    private void FlushBlock()
    {
        if (_isFlushed)
            return;
        if (_inputBuffer.Count > 0)
        {
            Byte[] input = _inputBuffer.ToArray();
            Byte[] compressed = CompressBlock(input);
            _baseStream.Write(compressed, 0, compressed.Length);
            _inputBuffer.Clear();
        }

        _isFlushed = true;
    }

    /// <summary>
    /// Compresses a block using a simple greedy LZ4 algorithm.
    /// The output is a raw LZ4 block (token stream) with no header.
    /// </summary>
    /// <param name="input">The data to compress.</param>
    /// <returns>The compressed raw block.</returns>
    private Byte[] CompressBlock(Byte[] input)
    {
        const Int32 MIN_MATCH = 4;
        using (var ms = new MemoryStream())
        {
            Int32 anchor = 0;
            Int32 i = 0;
            while (i <= input.Length - MIN_MATCH)
            {
                Int32 bestLength = 0;
                Int32 bestMatchIndex = 0;
                Int32 searchStart = Math.Max(0, i - 65535);
                for (Int32 j = searchStart; j < i; j++)
                {
                    Int32 length = 0;
                    while (i + length < input.Length && input[j + length] == input[i + length])
                        length++;
                    if (length >= MIN_MATCH && length > bestLength)
                    {
                        bestLength = length;
                        bestMatchIndex = j;
                    }
                }

                if (bestLength >= MIN_MATCH)
                {
                    Int32 literalLength = i - anchor;
                    Int32 tokenLiteral = literalLength >= 15 ? 15 : literalLength;
                    Int32 tokenMatch = (bestLength - MIN_MATCH) >= 15 ? 15 : (bestLength - MIN_MATCH);
                    Byte token = (Byte)((tokenLiteral << 4) | tokenMatch);
                    ms.WriteByte(token);
                    if (literalLength >= 15)
                    {
                        Int32 len = literalLength - 15;
                        while (len >= 255)
                        {
                            ms.WriteByte(255);
                            len -= 255;
                        }

                        ms.WriteByte((Byte)len);
                    }

                    ms.Write(input, anchor, literalLength);
                    Int32 offset = i - bestMatchIndex;
                    ms.WriteByte((Byte)(offset & 0xFF));
                    ms.WriteByte((Byte)((offset >> 8) & 0xFF));
                    if (bestLength - MIN_MATCH >= 15)
                    {
                        Int32 len = bestLength - MIN_MATCH - 15;
                        while (len >= 255)
                        {
                            ms.WriteByte(255);
                            len -= 255;
                        }

                        ms.WriteByte((Byte)len);
                    }

                    i += bestLength;
                    anchor = i;
                }
                else
                {
                    i++;
                }
            }

            Int32 remaining = input.Length - anchor;
            if (remaining > 0)
            {
                Int32 tokenLiteral = remaining >= 15 ? 15 : remaining;
                Byte token = (Byte)(tokenLiteral << 4); // no match follows
                ms.WriteByte(token);
                if (remaining >= 15)
                {
                    Int32 len = remaining - 15;
                    while (len >= 255)
                    {
                        ms.WriteByte(255);
                        len -= 255;
                    }

                    ms.WriteByte((Byte)len);
                }

                ms.Write(input, anchor, remaining);
            }

            return ms.ToArray();
        }
    }
}