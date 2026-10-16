namespace Albeoris.Games.Core.NSCompression.LZ4;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// A stream that decompresses data in the raw LZ4 block format.
/// The underlying stream must contain a raw LZ4 block (starting with a token)
/// and the expected decompressed length is provided.
/// Data is decompressed on the fly as requested.
/// </summary>
public class LZ4DecompressionStream : Stream
{
    private readonly Stream _baseStream;
    private readonly Int64 _decompressedLength;
    private readonly Boolean _leaveOpen;
    private Int32 _decompressedPosition;

    // History buffer (64 KB) to support match copying.
    private readonly Byte[] _historyBuffer = new Byte[65536];
    private Int32 _historyPos;

    // State-machine fields.
    private enum Phase
    {
        NeedToken,
        CopyLiteral,
        CopyMatch,
        Finished
    }

    private Phase _phase = Phase.NeedToken;
    private Int32 _pendingLiteral; // literal bytes still to copy
    private Int32 _pendingMatch; // match bytes still to copy
    private Int32 _pendingMatchNibble; // initial match nibble from token (to compute match length)
    private Int32 _currentMatchOffset; // match offset

    /// <summary>
    /// Initializes a new instance of the <see cref="LZ4DecompressionStream"/> class.
    /// </summary>
    /// <param name="baseStream">
    /// The stream containing a raw LZ4 block (compatible with LLxx.LZ4).
    /// </param>
    /// <param name="decompressedLength">The expected decompressed length.</param>
    /// <param name="leaveOpen">If true, leaves the underlying stream open when this stream is closed.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="baseStream"/> is null.</exception>
    /// <exception cref="ArgumentException">If <paramref name="baseStream"/> is not readable.</exception>
    public LZ4DecompressionStream(Stream baseStream, Int64 decompressedLength, Boolean leaveOpen)
    {
        _baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
        if (!_baseStream.CanRead)
            throw new ArgumentException("Base stream must be readable.", nameof(baseStream));
        _decompressedLength = decompressedLength;
        _leaveOpen = leaveOpen;
        _decompressedPosition = 0;
    }

    /// <inheritdoc/>
    public override Boolean CanRead => true;

    /// <inheritdoc/>
    public override Boolean CanSeek => false;

    /// <inheritdoc/>
    public override Boolean CanWrite => false;

    /// <inheritdoc/>
    public override Int64 Length => _decompressedLength;

    /// <inheritdoc/>
    public override Int64 Position
    {
        get => _decompressedPosition;
        set => throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override void Flush()
    {
    }

    /// <inheritdoc/>
    public override Int32 Read(Byte[] buffer, Int32 offset, Int32 count)
    {
        if (buffer == null)
            throw new ArgumentNullException(nameof(buffer));
        if (offset < 0 || offset > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(offset));
        if (count < 0 || (offset + count) > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(count));

        Int32 totalOut = 0;
        while (count > 0 && _decompressedPosition < _decompressedLength)
        {
            switch (_phase)
            {
                case Phase.NeedToken:
                {
                    Int32 token = ReadByteInternal();
                    // High nibble = literal length.
                    Int32 literal = token >> 4;
                    if (literal == 15)
                    {
                        Int32 extra;
                        while ((extra = ReadByteInternal()) == 255)
                            literal += 255;
                        literal += extra;
                    }

                    _pendingLiteral = literal;
                    // Save match nibble for later.
                    _pendingMatchNibble = token & 0x0F;
                    _phase = Phase.CopyLiteral;
                }
                    break;

                case Phase.CopyLiteral:
                {
                    if (_pendingLiteral > 0)
                    {
                        Int32 toCopy = Math.Min(_pendingLiteral, count);
                        Int32 r = 0;
                        while (r < toCopy)
                        {
                            Int32 n = _baseStream.Read(buffer, offset + r, toCopy - r);
                            if (n == 0)
                                throw new EndOfStreamException("Unexpected end while reading literal bytes.");
                            r += n;
                        }

                        // Also update history.
                        for (Int32 i = 0; i < toCopy; i++)
                        {
                            _historyBuffer[_historyPos] = buffer[offset + i];
                            _historyPos = (_historyPos + 1) % _historyBuffer.Length;
                        }

                        _pendingLiteral -= toCopy;
                        _decompressedPosition += toCopy;
                        totalOut += toCopy;
                        offset += toCopy;
                        count -= toCopy;
                    }

                    if (_pendingLiteral == 0)
                    {
                        // If we have reached the end of decompressed data, finish.
                        if (_decompressedPosition == _decompressedLength)
                        {
                            _phase = Phase.Finished;
                            break;
                        }

                        // Else, process match.
                        Int32 offLsb = ReadByteInternal();
                        Int32 offMsb = ReadByteInternal();
                        _currentMatchOffset = offLsb | (offMsb << 8);
                        Int32 matchLength = _pendingMatchNibble + 4; // MIN_MATCH = 4
                        if (_pendingMatchNibble == 15)
                        {
                            Int32 extra;
                            while ((extra = ReadByteInternal()) == 255)
                                matchLength += 255;
                            matchLength += extra;
                        }

                        _pendingMatch = matchLength;
                        _phase = Phase.CopyMatch;
                    }
                }
                    break;

                case Phase.CopyMatch:
                {
                    if (_pendingMatch > 0)
                    {
                        Int32 toCopy = Math.Min(_pendingMatch, count);
                        for (Int32 i = 0; i < toCopy; i++)
                        {
                            // Calculate source index in history.
                            Int32 srcIndex = (_historyPos - _currentMatchOffset + _historyBuffer.Length) % _historyBuffer.Length;
                            Byte b = _historyBuffer[srcIndex];
                            buffer[offset + i] = b;
                            _historyBuffer[_historyPos] = b;
                            _historyPos = (_historyPos + 1) % _historyBuffer.Length;
                        }

                        _pendingMatch -= toCopy;
                        _decompressedPosition += toCopy;
                        totalOut += toCopy;
                        offset += toCopy;
                        count -= toCopy;
                        if (_pendingMatch == 0)
                        {
                            _phase = Phase.NeedToken;
                        }
                    }
                }
                    break;

                case Phase.Finished:
                    return totalOut;
            }
        }

        return totalOut;
    }

    /// <inheritdoc/>
    public override async Task<Int32> ReadAsync(Byte[] buffer, Int32 offset, Int32 count, CancellationToken cancellationToken)
    {
        if (buffer == null)
            throw new ArgumentNullException(nameof(buffer));
        if (offset < 0 || offset > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(offset));
        if (count < 0 || (offset + count) > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(count));

        Int32 totalOut = 0;
        while (count > 0 && _decompressedPosition < _decompressedLength)
        {
            switch (_phase)
            {
                case Phase.NeedToken:
                {
                    Int32 token = await ReadByteInternalAsync(cancellationToken).ConfigureAwait(false);
                    Int32 literal = token >> 4;
                    if (literal == 15)
                    {
                        Int32 extra;
                        while ((extra = await ReadByteInternalAsync(cancellationToken).ConfigureAwait(false)) == 255)
                            literal += 255;
                        literal += extra;
                    }

                    _pendingLiteral = literal;
                    _pendingMatchNibble = token & 0x0F;
                    _phase = Phase.CopyLiteral;
                }
                    break;

                case Phase.CopyLiteral:
                {
                    if (_pendingLiteral > 0)
                    {
                        Int32 toCopy = Math.Min(_pendingLiteral, count);
                        Int32 r = 0;
                        while (r < toCopy)
                        {
                            Int32 n = await _baseStream.ReadAsync(buffer, offset + r, toCopy - r, cancellationToken).ConfigureAwait(false);
                            if (n == 0)
                                throw new EndOfStreamException("Unexpected end while reading literal bytes.");
                            r += n;
                        }

                        for (Int32 i = 0; i < toCopy; i++)
                        {
                            _historyBuffer[_historyPos] = buffer[offset + i];
                            _historyPos = (_historyPos + 1) % _historyBuffer.Length;
                        }

                        _pendingLiteral -= toCopy;
                        _decompressedPosition += toCopy;
                        totalOut += toCopy;
                        offset += toCopy;
                        count -= toCopy;
                    }

                    if (_pendingLiteral == 0)
                    {
                        if (_decompressedPosition == _decompressedLength)
                        {
                            _phase = Phase.Finished;
                            break;
                        }

                        Int32 offLsb = await ReadByteInternalAsync(cancellationToken).ConfigureAwait(false);
                        Int32 offMsb = await ReadByteInternalAsync(cancellationToken).ConfigureAwait(false);
                        _currentMatchOffset = offLsb | (offMsb << 8);
                        Int32 matchLength = _pendingMatchNibble + 4;
                        if (_pendingMatchNibble == 15)
                        {
                            Int32 extra;
                            while ((extra = await ReadByteInternalAsync(cancellationToken).ConfigureAwait(false)) == 255)
                                matchLength += 255;
                            matchLength += extra;
                        }

                        _pendingMatch = matchLength;
                        _phase = Phase.CopyMatch;
                    }
                }
                    break;

                case Phase.CopyMatch:
                {
                    if (_pendingMatch > 0)
                    {
                        Int32 toCopy = Math.Min(_pendingMatch, count);
                        for (Int32 i = 0; i < toCopy; i++)
                        {
                            Int32 srcIndex = (_historyPos - _currentMatchOffset + _historyBuffer.Length) % _historyBuffer.Length;
                            Byte b = _historyBuffer[srcIndex];
                            buffer[offset + i] = b;
                            _historyBuffer[_historyPos] = b;
                            _historyPos = (_historyPos + 1) % _historyBuffer.Length;
                        }

                        _pendingMatch -= toCopy;
                        _decompressedPosition += toCopy;
                        totalOut += toCopy;
                        offset += toCopy;
                        count -= toCopy;
                        if (_pendingMatch == 0)
                        {
                            _phase = Phase.NeedToken;
                        }
                    }
                }
                    break;

                case Phase.Finished:
                    return totalOut;
            }
        }

        return totalOut;
    }

    /// <summary>
    /// Reads a single byte from the underlying stream.
    /// </summary>
    /// <returns>The byte read as an int.</returns>
    private Int32 ReadByteInternal()
    {
        Int32 b = _baseStream.ReadByte();
        if (b == -1)
            throw new EndOfStreamException("Unexpected end of compressed stream.");
        return b;
    }

    /// <summary>
    /// Asynchronously reads a single byte from the underlying stream.
    /// </summary>
    private async Task<Int32> ReadByteInternalAsync(CancellationToken cancellationToken)
    {
        Byte[] buf = new Byte[1];
        Int32 r = await _baseStream.ReadAsync(buf, 0, 1, cancellationToken).ConfigureAwait(false);
        if (r == 0)
            throw new EndOfStreamException("Unexpected end of compressed stream.");
        return buf[0];
    }

    /// <inheritdoc/>
    public override Int64 Seek(Int64 offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override void SetLength(Int64 value)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override void Write(Byte[] buffer, Int32 offset, Int32 count)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    protected override void Dispose(Boolean disposing)
    {
        if (disposing && !_leaveOpen)
            _baseStream.Dispose();
        base.Dispose(disposing);
    }
}