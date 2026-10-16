namespace Albeoris.Games.Core.NSCompression.LZS;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

/// <summary>
/// Provides a stream for decompressing data compressed with the LZS (LZSS) algorithm.
/// It uses a 4096-byte ring buffer and a control flag mechanism.
/// </summary>
public class LZSDecompressionStream : Stream
{
    private readonly Stream _input;
    private readonly Boolean _leaveOpen;
    private Int64 _remaining; // remaining decompressed bytes
    private readonly Byte[] _circularBuffer = new Byte[4096];
    private Int32 _circularBufferPos = 0;
    private Byte _flagBits = 0;
    private Int32 _flagCount = 0;
    private readonly Queue<Byte> _pending = new Queue<Byte>();
    private Boolean _disposed;

    public LZSDecompressionStream(Stream compressedStream, Int64 compressedSize, Int64 decompressedSize, Boolean leaveOpen)
    {
        if (compressedStream == null)
            throw new ArgumentNullException(nameof(compressedStream));
        if (!compressedStream.CanRead)
            throw new ArgumentException("The compressed stream does not support reading.", nameof(compressedStream));

        _input = compressedStream;
        _remaining = decompressedSize;
        _leaveOpen = leaveOpen;
    }

    public override Boolean CanRead => !_disposed;
    public override Boolean CanSeek => false;
    public override Boolean CanWrite => false;
    public override Int64 Length => throw new NotSupportedException();
    public override Int64 Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override void Flush() { }

    public override Int32 Read(Byte[] buffer, Int32 offset, Int32 count)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(LZSDecompressionStream));
        if (buffer == null) throw new ArgumentNullException(nameof(buffer));
        if (offset < 0 || count < 0 || offset + count > buffer.Length)
            throw new ArgumentOutOfRangeException();

        Int32 bytesRead = 0;
        while (bytesRead < count && _remaining > 0)
        {
            // Return pending bytes first if available
            if (_pending.Count > 0)
            {
                buffer[offset + bytesRead] = _pending.Dequeue();
                bytesRead++;
                _remaining--;
                continue;
            }

            EnsureFlag();
            if ((_flagBits & 1) != 0)
            {
                // Literal byte
                Int32 b = _input.ReadByte();
                if (b == -1)
                    throw new Exception("Unexpected end of stream.");
                Byte literal = (Byte)b;
                WriteToCircularBuffer(literal);
                buffer[offset + bytesRead] = literal;
                bytesRead++;
                _remaining--;
            }
            else
            {
                // Back-reference
                Int32 offByte = _input.ReadByte();
                if (offByte == -1)
                    throw new Exception("Unexpected end of stream.");
                Int32 second = _input.ReadByte();
                if (second == -1)
                    throw new Exception("Unexpected end of stream.");

                Int16 off = (Int16)offByte;
                off += (Int16)(((second & 0xF0) << 4)); // offset adjustment: add extra bits
                Int32 len = (second & 0xF) + 3;
                Int32 refIndex = (off + 18) & 0xFFF;
                for (Int32 i = 0; i < len; i++)
                {
                    Byte b = _circularBuffer[refIndex];
                    WriteToCircularBuffer(b);
                    _pending.Enqueue(b);
                    refIndex = (refIndex + 1) & 0xFFF;
                }
            }
            _flagBits >>= 1;
            _flagCount--;
        }
        return bytesRead;
    }

    public override async Task<Int32> ReadAsync(Byte[] buffer, Int32 offset, Int32 count, CancellationToken cancellationToken)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(LZSDecompressionStream));
        if (buffer == null) throw new ArgumentNullException(nameof(buffer));
        if (offset < 0 || count < 0 || offset + count > buffer.Length)
            throw new ArgumentOutOfRangeException();

        Int32 bytesRead = 0;
        while (bytesRead < count && _remaining > 0)
        {
            if (_pending.Count > 0)
            {
                buffer[offset + bytesRead] = _pending.Dequeue();
                bytesRead++;
                _remaining--;
                continue;
            }

            await EnsureFlagAsync(cancellationToken).ConfigureAwait(false);
            if ((_flagBits & 1) != 0)
            {
                Byte[] one = new Byte[1];
                Int32 r = await _input.ReadAsync(one, 0, 1, cancellationToken).ConfigureAwait(false);
                if (r == 0)
                    throw new Exception("Unexpected end of stream.");
                Byte literal = one[0];
                WriteToCircularBuffer(literal);
                buffer[offset + bytesRead] = literal;
                bytesRead++;
                _remaining--;
            }
            else
            {
                Byte[] two = new Byte[2];
                Int32 r = await ReadFullyAsync(_input, two, 0, 2, cancellationToken).ConfigureAwait(false);
                if (r != 2)
                    throw new Exception("Unexpected end of stream.");

                Int16 off = (Int16)two[0];
                off += (Int16)(((two[1] & 0xF0) << 4));
                Int32 len = (two[1] & 0xF) + 3;
                Int32 refIndex = (off + 18) & 0xFFF;
                for (Int32 i = 0; i < len; i++)
                {
                    Byte b = _circularBuffer[refIndex];
                    WriteToCircularBuffer(b);
                    _pending.Enqueue(b);
                    refIndex = (refIndex + 1) & 0xFFF;
                }
            }
            _flagBits >>= 1;
            _flagCount--;
        }
        return bytesRead;
    }

    private void EnsureFlag()
    {
        if (_flagCount == 0)
        {
            Int32 flag = _input.ReadByte();
            if (flag == -1)
                throw new Exception("Unexpected end of stream.");
            _flagBits = (Byte)flag;
            _flagCount = 8;
        }
    }

    private async Task EnsureFlagAsync(CancellationToken cancellationToken)
    {
        if (_flagCount == 0)
        {
            Byte[] flag = new Byte[1];
            Int32 r = await _input.ReadAsync(flag, 0, 1, cancellationToken).ConfigureAwait(false);
            if (r == 0)
                throw new Exception("Unexpected end of stream.");
            _flagBits = flag[0];
            _flagCount = 8;
        }
    }

    private static async Task<Int32> ReadFullyAsync(Stream stream, Byte[] buffer, Int32 offset, Int32 count, CancellationToken cancellationToken)
    {
        Int32 total = 0;
        while (total < count)
        {
            Int32 r = await stream.ReadAsync(buffer, offset + total, count - total, cancellationToken).ConfigureAwait(false);
            if (r == 0) break;
            total += r;
        }
        return total;
    }

    private void WriteToCircularBuffer(Byte b)
    {
        _circularBuffer[_circularBufferPos] = b;
        _circularBufferPos = (_circularBufferPos + 1) & 0xFFF;
    }

    public override Int64 Seek(Int64 offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(Int64 value) => throw new NotSupportedException();
    public override void Write(Byte[] buffer, Int32 offset, Int32 count) => throw new NotSupportedException();

    protected override void Dispose(Boolean disposing)
    {
        if (!_disposed)
        {
            if (disposing && !_leaveOpen)
                _input.Dispose();
            _disposed = true;
        }
        base.Dispose(disposing);
    }
}