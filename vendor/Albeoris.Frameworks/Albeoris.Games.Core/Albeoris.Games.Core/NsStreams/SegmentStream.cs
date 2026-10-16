namespace Albeoris.Games.Core.NsStreams;

/// <summary>
/// Represents a stream that is limited to a specific segment (start offset + length) of an underlying stream.
/// </summary>
public class SegmentStream : Stream
{
    private readonly Stream _baseStream;
    private readonly Int64 _baseOffset;
    private readonly Int64 _length;
    private Int64 _position; // Position relative to the segment's start

    /// <summary>
    /// Initializes a new instance of the <see cref="SegmentStream"/> class.
    /// </summary>
    /// <param name="baseStream">The underlying stream to segment.</param>
    /// <param name="baseOffset">The start position (offset) in the base stream.</param>
    /// <param name="length">The length of the segment.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="baseStream"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if <paramref name="baseOffset"/> or <paramref name="length"/> are negative,
    /// or if <paramref name="baseOffset"/> goes beyond the end of the base stream.
    /// </exception>
    public SegmentStream(Stream baseStream, Int64 baseOffset, Int64 length)
    {
        ArgumentNullException.ThrowIfNull(baseStream);
        ArgumentOutOfRangeException.ThrowIfNegative(baseOffset, nameof(baseOffset));
        ArgumentOutOfRangeException.ThrowIfNegative(length, nameof(length));

        if (baseStream.CanSeek && baseOffset > baseStream.Length)
            throw new ArgumentOutOfRangeException(nameof(baseOffset), "Start is beyond the end of the base stream.");
        if (!baseStream.CanSeek && baseOffset != baseStream.Position)
            throw new ArgumentOutOfRangeException(nameof(baseOffset), "Base stream does not support seeking.");

        _baseStream = baseStream;
        _baseOffset = baseOffset;
        _length = length;
        _position = 0;

        if (baseOffset != baseStream.Position)
            _baseStream.Seek(_baseOffset, SeekOrigin.Begin);
    }

    public override Boolean CanRead => _baseStream.CanRead;
    public override Boolean CanSeek => _baseStream.CanSeek;
    public override Boolean CanWrite => _baseStream.CanWrite;
    public override Int64 Length => _length;

    /// <inheritdoc cref="Stream.Position"/>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the position is set outside the segment bounds.</exception>
    public override Int64 Position
    {
        get => _position;
        set
        {
            if (value < 0 || value > _length)
                throw new ArgumentOutOfRangeException(nameof(value), "Position must stay within the segment.");
            _position = value;
            if (_baseStream.CanSeek)
                _baseStream.Seek(_baseOffset + _position, SeekOrigin.Begin);
        }
    }

    /// <summary>
    /// Reads a sequence of bytes from the segment and advances the position within the segment.
    /// </summary>
    /// <param name="buffer">An array of bytes. When this method returns, the buffer contains the specified byte array with the values between <paramref name="offset"/> and (<paramref name="offset"/> + <paramref name="count"/> - 1) replaced by the bytes read from the segment.</param>
    /// <param name="offset">The zero-based byte offset in <paramref name="buffer"/> at which to begin storing the data read from the current stream.</param>
    /// <param name="count">The maximum number of bytes to be read from the current segment.</param>
    /// <returns>The total number of bytes read into the buffer. This might be less than the number of bytes requested if that many bytes are not currently available.</returns>
    public override Int32 Read(Byte[] buffer, Int32 offset, Int32 count)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        if (offset < 0 || offset > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(offset));
        if (count < 0 || offset + count > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(count));

        Int64 remaining = _length - _position;
        if (remaining <= 0)
            return 0;

        if (count > remaining)
            count = checked((Int32)remaining);

        Int32 bytesRead = _baseStream.Read(buffer, offset, count);
        _position += bytesRead;
        return bytesRead;
    }

    /// <summary>
    /// Writes a sequence of bytes to the segment and advances the position within the segment.
    /// </summary>
    /// <param name="buffer">An array of bytes. This method copies <paramref name="count"/> bytes from <paramref name="buffer"/> to the current stream.</param>
    /// <param name="offset">The zero-based byte offset in <paramref name="buffer"/> at which to begin copying bytes to the current segment.</param>
    /// <param name="count">The number of bytes to be written to the current segment.</param>
    public override void Write(Byte[] buffer, Int32 offset, Int32 count)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        if (offset < 0 || offset > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(offset));
        if (count < 0 || offset + count > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(count));

        Int64 remaining = _length - _position;
        if (count > remaining)
            throw new IOException("Write operation exceeds the segment length.");

        _baseStream.Write(buffer, offset, count);
        _position += count;
    }

    /// <summary>
    /// Sets the position within the current segment.
    /// </summary>
    /// <param name="offset">A byte offset relative to the <paramref name="origin"/> parameter.</param>
    /// <param name="origin">A value of type <see cref="SeekOrigin"/> indicating the reference point used to obtain the new position.</param>
    /// <returns>The new position within the current segment.</returns>
    public override Int64 Seek(Int64 offset, SeekOrigin origin)
    {
        Int64 newPos;
        switch (origin)
        {
            case SeekOrigin.Begin:
                newPos = offset;
                break;
            case SeekOrigin.Current:
                newPos = _position + offset;
                break;
            case SeekOrigin.End:
                newPos = _length + offset;
                break;
            default:
                throw new ArgumentException($"Invalid seek origin: {origin}.", nameof(origin));
        }

        if (newPos < 0 || newPos > _length)
            throw new IOException("Attempted to seek outside the segment.");

        _position = newPos;
        return _baseStream.Seek(_baseOffset + _position, SeekOrigin.Begin) - _baseOffset;
    }

    /// <summary>
    /// Sets the length of the current segment. Not supported in <see cref="SegmentStream"/>.
    /// </summary>
    /// <param name="value">The desired length of the segment in bytes.</param>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public override void SetLength(Int64 value)
    {
        throw new NotSupportedException("Setting length is not supported on a segment stream.");
    }

    public override void Flush()
    {
        _baseStream.Flush();
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        return _baseStream.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// Reads a byte from the segment and advances the position within the segment by one byte.
    /// </summary>
    /// <returns>The unsigned byte cast to an Int32, or -1 if at the end of the segment.</returns>
    public override Int32 ReadByte()
    {
        if (_position >= _length)
            return -1; // end of segment

        Int32 b = _baseStream.ReadByte();
        if (b >= 0)
        {
            _position++;
        }

        return b;
    }

    /// <summary>
    /// Writes a byte to the current segment at the current position, and advances the position within the segment by one byte.
    /// </summary>
    /// <param name="value">The byte to write to the segment.</param>
    /// <exception cref="NotSupportedException">Thrown if the underlying stream does not support writing.</exception>
    /// <exception cref="IOException">Thrown if writing goes beyond the segment bounds.</exception>
    public override void WriteByte(Byte value)
    {
        if (!CanWrite)
            throw new NotSupportedException("Writing is not supported by the underlying stream.");

        if (_position >= _length)
            throw new IOException("Cannot write beyond the end of the segment.");

        _baseStream.WriteByte(value);
        _position++;
    }

    /// <summary>
    /// Asynchronously reads a sequence of bytes from the current segment, advances the position within the segment,
    /// and monitors cancellation requests.
    /// </summary>
    /// <param name="buffer">The buffer to write the data into.</param>
    /// <param name="offset">The byte offset in <paramref name="buffer"/> at which to begin writing data read from the stream.</param>
    /// <param name="count">The maximum number of bytes to read.</param>
    /// <param name="cancellationToken">A token used to cancel the async operation.</param>
    /// <returns>A task that represents the asynchronous Read operation. The value of its <c>Result</c> parameter
    /// contains the total number of bytes read into the buffer.</returns>
    public override async Task<Int32> ReadAsync(
        Byte[] buffer,
        Int32 offset,
        Int32 count,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        if (offset < 0 || offset > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(offset));
        if (count < 0 || offset + count > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(count));

        Int64 remaining = _length - _position;
        if (remaining <= 0)
            return 0;

        if (count > remaining)
            count = (Int32)remaining;

        Int32 bytesRead = await _baseStream.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
        _position += bytesRead;
        return bytesRead;
    }

    /// <summary>
    /// Asynchronously writes a sequence of bytes to the current segment, advances the position within the segment,
    /// and monitors cancellation requests.
    /// </summary>
    /// <param name="buffer">The buffer to write the data from.</param>
    /// <param name="offset">The byte offset in <paramref name="buffer"/> at which to begin writing data.</param>
    /// <param name="count">The maximum number of bytes to write.</param>
    /// <param name="cancellationToken">A token used to cancel the async operation.</param>
    /// <returns>A task that represents the asynchronous Write operation.</returns>
    /// <exception cref="NotSupportedException">Thrown if the underlying stream does not support writing.</exception>
    /// <exception cref="IOException">Thrown if writing goes beyond the segment bounds.</exception>
    public override async Task WriteAsync(
        Byte[] buffer,
        Int32 offset,
        Int32 count,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (offset < 0 || offset > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(offset));
        if (count < 0 || offset + count > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(count));

        Int64 remaining = _length - _position;
        if (count > remaining)
            throw new IOException("Write operation exceeds the segment length.");

        await _baseStream.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
        _position += count;
    }

    /// <summary>
    /// Reads a sequence of bytes from the segment into a span and advances the position within the segment.
    /// </summary>
    /// <param name="buffer">The span to write the data into.</param>
    /// <returns>The total number of bytes read into the span. This might be less than the number of bytes requested.</returns>
    public override Int32 Read(Span<Byte> buffer)
    {
        if (_position >= _length)
            return 0;

        Int64 remaining = _length - _position;
        Int32 toRead = (Int32)Math.Min(buffer.Length, remaining);

        Int32 readBytes = _baseStream.Read(buffer.Slice(0, toRead));
        _position += readBytes;
        return readBytes;
    }

    /// <summary>
    /// Writes a sequence of bytes from the provided read-only span to the segment and advances the position.
    /// </summary>
    /// <param name="buffer">The span containing the data to write.</param>
    public override void Write(ReadOnlySpan<Byte> buffer)
    {
        if (!CanWrite)
            throw new NotSupportedException("Writing is not supported by the underlying stream.");

        Int64 remaining = _length - _position;
        if (buffer.Length > remaining)
            throw new IOException("Write operation exceeds the segment length.");

        _baseStream.Write(buffer);
        _position += buffer.Length;
    }

    /// <summary>
    /// Asynchronously reads a sequence of bytes from the segment into a memory region
    /// and advances the position within the segment.
    /// </summary>
    /// <param name="buffer">The memory region to write the data into.</param>
    /// <param name="cancellationToken">A token used to cancel the async operation.</param>
    /// <returns>A value task that represents the asynchronous read operation.
    /// The <c>Result</c> contains the total number of bytes read.</returns>
    public override ValueTask<Int32> ReadAsync(Memory<Byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_position >= _length)
            return ValueTask.FromResult(0);

        Int64 remaining = _length - _position;
        Int32 toRead = (Int32)Math.Min(buffer.Length, remaining);

        return ReadAsyncCore(buffer.Slice(0, toRead), cancellationToken);

        async ValueTask<Int32> ReadAsyncCore(Memory<Byte> subBuffer, CancellationToken ct)
        {
            Int32 readBytes = await _baseStream.ReadAsync(subBuffer, ct).ConfigureAwait(false);
            _position += readBytes;
            return readBytes;
        }
    }

    /// <summary>
    /// Asynchronously writes a sequence of bytes from the provided read-only memory region
    /// to the segment and advances the position.
    /// </summary>
    /// <param name="buffer">The memory region containing the data to write.</param>
    /// <param name="cancellationToken">A token used to cancel the async operation.</param>
    /// <returns>A value task that represents the asynchronous write operation.</returns>
    public override ValueTask WriteAsync(ReadOnlyMemory<Byte> buffer, CancellationToken cancellationToken = default)
    {
        if (!CanWrite)
            throw new NotSupportedException("Writing is not supported by the underlying stream.");

        Int64 remaining = _length - _position;
        if (buffer.Length > remaining)
            throw new IOException("Write operation exceeds the segment length.");

        return WriteAsyncCore(buffer, cancellationToken);

        async ValueTask WriteAsyncCore(ReadOnlyMemory<Byte> subBuffer, CancellationToken ct)
        {
            await _baseStream.WriteAsync(subBuffer, ct).ConfigureAwait(false);
            _position += subBuffer.Length;
        }
    }
}