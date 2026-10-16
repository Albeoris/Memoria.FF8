namespace Albeoris.Games.Core.NSCompression.LZS;

using System;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Provides a stream for compressing data using the LZS algorithm.
/// Data written to this stream is compressed on the fly and written to the underlying stream.
/// </summary>
public class LZSCompressionStream : Stream
{
    private readonly Stream _baseStream;
    private readonly Boolean _leaveOpen;
    private Boolean _disposed;

    // Buffer for uncompressed input data that hasn't been processed yet.
    private readonly List<Byte> _inputBuffer = new();

    // Token group variables: for each group, we accumulate a flag byte and a variable-length token payload.
    // Each flag bit corresponds to a token: 1 for literal (1 byte), 0 for back-reference (2 bytes).
    private Byte _currentFlag;
    private Int32 _tokenCount;
    private readonly List<Byte> _tokenData = new();

    // Circular dictionary (window) of 4096 bytes.
    private readonly Byte[] _dictionary = new Byte[4096];
    private Int32 _dictPos = 0; // next write position in dictionary

    // Maximum match length and minimum match length as defined by the algorithm.
    private const Int32 MaxMatch = 18;
    private const Int32 MinMatch = 3;
    private const Int32 WindowSize = 4096;

    public LZSCompressionStream(Stream baseStream, Boolean leaveOpen)
    {
        if (baseStream == null)
            throw new ArgumentNullException(nameof(baseStream));
        if (!baseStream.CanWrite)
            throw new ArgumentException("Base stream must be writable.", nameof(baseStream));

        _baseStream = baseStream;
        _leaveOpen = leaveOpen;

        // Initialize dictionary with zeros.
        Array.Clear(_dictionary, 0, _dictionary.Length);
    }

    public override Boolean CanWrite => !_disposed;
    public override Boolean CanRead => false;
    public override Boolean CanSeek => false;
    public override Int64 Length => throw new NotSupportedException();
    public override Int64 Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    /// <summary>
    /// Writes uncompressed data to the compression stream.
    /// </summary>
    public override void Write(Byte[] buffer, Int32 offset, Int32 count)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(LZSCompressionStream));
        if (buffer == null)
            throw new ArgumentNullException(nameof(buffer));
        if (offset < 0 || count < 0 || offset + count > buffer.Length)
            throw new ArgumentOutOfRangeException();

        // Append new data to our input buffer.
        for (Int32 i = offset; i < offset + count; i++)
            _inputBuffer.Add(buffer[i]);

        // Process as many tokens as possible.
        ProcessBuffer(false);
    }

    /// <summary>
    /// Processes buffered input data and emits tokens.
    /// If flushAll is false, leaves up to 2 bytes unprocessed (to allow for possible matches).
    /// </summary>
    private void ProcessBuffer(Boolean flushAll)
    {
        // We leave up to 2 bytes unprocessed unless flushing everything
        Int32 limit = _inputBuffer.Count;
        if (!flushAll && limit > 2)
            limit = _inputBuffer.Count - 2;

        Int32 pos = 0;
        while (pos < limit)
        {
            // Try to find the longest match in the dictionary for the upcoming data.
            Int32 bestLength = 0;
            Int32 bestOffset = 0; // the token offset (so that (offset + 18) mod 4096 equals the start index in the dictionary)
            // Naively search entire window.
            for (Int32 j = 0; j < WindowSize; j++)
            {
                if (_dictionary[j] != _inputBuffer[pos])
                    continue;
                Int32 length = 1;
                while (length < MaxMatch && pos + length < _inputBuffer.Count)
                {
                    // Compare next byte with dictionary (using cyclic wrap-around)
                    if (_dictionary[(j + length) & 0xFFF] != _inputBuffer[pos + length])
                        break;
                    length++;
                }
                if (length > bestLength && length >= MinMatch)
                {
                    bestLength = length;
                    // Compute token offset: we need (offset + 18) mod 4096 == j.
                    Int32 tokenOffset = j - 18;
                    if (tokenOffset < 0)
                        tokenOffset += WindowSize;
                    bestOffset = tokenOffset;
                    if (bestLength == MaxMatch)
                        break; // reached maximum match length
                }
            }

            if (bestLength >= MinMatch)
            {
                // Emit back-reference token (flag bit = 0).
                // Token is 2 bytes:
                // first byte = lower 8 bits of offset.
                // second byte = (upper 4 bits of offset in high nibble) | ((matchLength - 3) in low nibble).
                Byte token1 = (Byte)(bestOffset & 0xFF);
                Byte token2 = (Byte)(((bestOffset >> 8) & 0x0F) << 4 | ((bestLength - 3) & 0x0F));
                AddToken(false, new Byte[] { token1, token2 });

                // Write matched bytes to dictionary.
                for (Int32 k = 0; k < bestLength; k++)
                    WriteToDictionary(_inputBuffer[pos + k]);

                pos += bestLength;
            }
            else
            {
                // No sufficient match; output literal token (flag bit = 1).
                AddToken(true, new Byte[] { _inputBuffer[pos] });
                WriteToDictionary(_inputBuffer[pos]);
                pos++;
            }
        }
        // Remove processed bytes from the input buffer.
        if (pos > 0)
            _inputBuffer.RemoveRange(0, pos);
    }

    /// <summary>
    /// Adds a token to the current token group.
    /// For literal tokens, isLiteral should be true (flag bit 1); for back-references, false (flag bit 0).
    /// </summary>
    private void AddToken(Boolean isLiteral, Byte[] tokenBytes)
    {
        if (isLiteral)
            _currentFlag |= (Byte)(1 << _tokenCount);
        _tokenData.AddRange(tokenBytes);
        _tokenCount++;

        if (_tokenCount == 8)
            FlushTokenGroup();
    }

    /// <summary>
    /// Flushes the current token group (flag byte plus token data) to the base stream.
    /// </summary>
    private void FlushTokenGroup()
    {
        _baseStream.WriteByte(_currentFlag);
        if (_tokenData.Count > 0)
            _baseStream.Write(_tokenData.ToArray(), 0, _tokenData.Count);
        _currentFlag = 0;
        _tokenCount = 0;
        _tokenData.Clear();
    }

    /// <summary>
    /// Writes a byte to the circular dictionary.
    /// </summary>
    private void WriteToDictionary(Byte b)
    {
        _dictionary[_dictPos] = b;
        _dictPos = (_dictPos + 1) & 0xFFF;
    }

    public override void Flush()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(LZSCompressionStream));
        // Process any remaining data in the input buffer.
        ProcessBuffer(true);
        // Emit any leftover tokens.
        if (_tokenCount > 0)
            FlushTokenGroup();
        _baseStream.Flush();
    }

    public override void WriteByte(Byte value)
    {
        Write(new Byte[] { value }, 0, 1);
    }

    public override Int32 Read(Byte[] buffer, Int32 offset, Int32 count)
        => throw new NotSupportedException("Read is not supported on LZSCompressionStream.");

    public override Int64 Seek(Int64 offset, SeekOrigin origin)
        => throw new NotSupportedException("Seek is not supported on LZSCompressionStream.");

    public override void SetLength(Int64 value)
        => throw new NotSupportedException("SetLength is not supported on LZSCompressionStream.");

    protected override void Dispose(Boolean disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                try { Flush(); } catch { }
                if (!_leaveOpen)
                    _baseStream.Dispose();
            }
            _disposed = true;
        }
        base.Dispose(disposing);
    }
}