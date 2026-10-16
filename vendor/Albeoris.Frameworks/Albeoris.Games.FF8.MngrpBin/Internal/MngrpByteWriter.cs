using System.Buffers;
using System.Buffers.Binary;

namespace Albeoris.Games.FF8.MngrpBin.Internal;

/// <summary>A little-endian append-only buffer the section codecs serialize into.</summary>
internal sealed class MngrpByteWriter
{
    private readonly ArrayBufferWriter<Byte> _buffer = new();

    /// <summary>The number of bytes written so far.</summary>
    public Int32 Length => _buffer.WrittenCount;

    public void WriteUInt16(UInt16 value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(_buffer.GetSpan(sizeof(UInt16)), value);
        _buffer.Advance(sizeof(UInt16));
    }

    public void WriteUInt32(UInt32 value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(_buffer.GetSpan(sizeof(UInt32)), value);
        _buffer.Advance(sizeof(UInt32));
    }

    public void WriteBytes(ReadOnlySpan<Byte> bytes)
    {
        _buffer.Write(bytes);
    }

    public void WriteZeros(Int32 count)
    {
        _buffer.GetSpan(count)[..count].Clear();
        _buffer.Advance(count);
    }

    /// <summary>Appends zeros until <see cref="Length"/> reaches <paramref name="length"/>.</summary>
    public void PadTo(Int32 length)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(length, Length);
        WriteZeros(length - Length);
    }

    /// <summary>Appends zeros until <see cref="Length"/> reaches the next four-byte boundary.</summary>
    public void PadToFour()
    {
        WriteZeros(MngrpFormat.AlignToFour(Length) - Length);
    }

    public Byte[] ToArray() => _buffer.WrittenSpan.ToArray();
}
