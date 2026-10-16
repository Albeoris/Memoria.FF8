namespace Albeoris.Games.Core.NsStreams;

/// <summary>
/// Exposes every low and high nibble of the underlying stream as a separate byte.
/// </summary>
public sealed class HalfByteStream : Stream
{
    private readonly Boolean _leaveOpen;
    private readonly Int64 _origin;
    private Byte? _pendingHighNibble;
    private Byte? _pendingLowNibble;
    private Boolean _disposed;
    private Int64 _position;

    public HalfByteStream(Stream baseStream, Boolean leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(baseStream);
        BaseStream = baseStream;
        _leaveOpen = leaveOpen;
        _origin = baseStream.CanSeek ? baseStream.Position : 0;
    }

    public Stream BaseStream { get; }

    public override Boolean CanRead => !_disposed && BaseStream.CanRead;
    public override Boolean CanSeek => false;
    public override Boolean CanWrite => !_disposed && BaseStream.CanWrite;
    public override Int64 Length => BaseStream.CanSeek
        ? checked((BaseStream.Length - _origin) * 2)
        : throw new NotSupportedException();

    public override Int64 Position
    {
        get => _position;
        set => throw new NotSupportedException();
    }

    public override Int32 ReadByte()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!BaseStream.CanRead)
            throw new NotSupportedException("The underlying stream is not readable.");
        if (_pendingLowNibble is not null)
            throw new InvalidOperationException("Cannot read while a half-written byte is buffered.");

        if (_pendingHighNibble is Byte highNibble)
        {
            _pendingHighNibble = null;
            _position++;
            return highNibble;
        }

        Int32 value = BaseStream.ReadByte();
        if (value < 0)
            return value;

        _pendingHighNibble = (Byte)(value >> 4);
        _position++;
        return value & 0x0F;
    }

    public override Int32 Read(Byte[] buffer, Int32 offset, Int32 count)
    {
        ValidateBuffer(buffer, offset, count);
        return Read(buffer.AsSpan(offset, count));
    }

    public override Int32 Read(Span<Byte> buffer)
    {
        Int32 index = 0;
        while (index < buffer.Length)
        {
            Int32 value = ReadByte();
            if (value < 0)
                break;
            buffer[index++] = (Byte)value;
        }
        return index;
    }

    public override void WriteByte(Byte value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!BaseStream.CanWrite)
            throw new NotSupportedException("The underlying stream is not writable.");
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, (Byte)0x0F);
        if (_pendingHighNibble is not null)
            throw new InvalidOperationException("Cannot write while a half-read byte is buffered.");

        if (_pendingLowNibble is null)
        {
            _pendingLowNibble = value;
        }
        else
        {
            BaseStream.WriteByte((Byte)(_pendingLowNibble.Value | value << 4));
            _pendingLowNibble = null;
        }
        _position++;
    }

    public override void Write(Byte[] buffer, Int32 offset, Int32 count)
    {
        ValidateBuffer(buffer, offset, count);
        Write(buffer.AsSpan(offset, count));
    }

    public override void Write(ReadOnlySpan<Byte> buffer)
    {
        foreach (Byte value in buffer)
            WriteByte(value);
    }

    public override void Flush()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        FlushPendingNibble();
        BaseStream.Flush();
    }

    public override Int64 Seek(Int64 offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(Int64 value) => throw new NotSupportedException();

    protected override void Dispose(Boolean disposing)
    {
        if (disposing && !_disposed)
        {
            FlushPendingNibble();
            if (!_leaveOpen)
                BaseStream.Dispose();
            _disposed = true;
        }
        base.Dispose(disposing);
    }

    private void FlushPendingNibble()
    {
        if (_pendingLowNibble is not Byte lowNibble)
            return;
        BaseStream.WriteByte(lowNibble);
        _pendingLowNibble = null;
    }

    private static void ValidateBuffer(Byte[] buffer, Int32 offset, Int32 count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (offset > buffer.Length - count)
            throw new ArgumentException("Offset and count exceed the buffer bounds.");
    }
}
