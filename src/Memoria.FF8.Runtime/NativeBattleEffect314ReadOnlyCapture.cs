using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace Memoria.FF8.Runtime;

/// <summary>Detached call-local face or vertex bytes with their observed knownness; contains no mutable memory owner.</summary>
internal readonly struct NativeBattleEffect314ReadOnlyCapture
{
    [InlineArray(20)]
    private struct Bytes
    {
        private byte first;
    }

    private readonly Bytes bytes;
    private readonly uint knownMask;
    private readonly int length;

    private NativeBattleEffect314ReadOnlyCapture(ReadOnlySpan<byte> data, ReadOnlySpan<bool> known)
    {
        if (data.Length > 20 || known.Length != data.Length)
        {
            throw new InvalidDataException("Native captured face or vertex requires bounded bytes and matching knownness.");
        }
        Bytes captured = default;
        uint mask = 0;
        for (int index = 0; index < data.Length; index++)
        {
            captured[index] = data[index];
            if (known[index])
            {
                mask |= 1u << index;
            }
        }
        bytes = captured;
        knownMask = mask;
        length = data.Length;
    }

    internal static NativeBattleEffect314ReadOnlyCapture Capture(NativeBattleEffectArena source, int offset, int length)
    {
        if ((uint)length > 20)
        {
            throw new InvalidDataException("Native captured face or vertex exceeds its bounded bytes.");
        }
        Span<byte> data = stackalloc byte[length];
        Span<bool> known = stackalloc bool[length];
        source.ReadWithKnownness(offset, data, known);
        return new(data, known);
    }

    internal static NativeBattleEffect314ReadOnlyCapture Known(ReadOnlySpan<byte> data)
    {
        if (data.Length > 20)
        {
            throw new InvalidDataException("Native captured face or vertex exceeds its bounded bytes.");
        }
        Span<bool> known = stackalloc bool[data.Length];
        known.Fill(true);
        return new(data, known);
    }

    internal byte ReadByte(int offset)
    {
        Span<byte> data = stackalloc byte[1];
        ReadInto(offset, data);
        return data[0];
    }

    internal ushort ReadUInt16(int offset)
    {
        Span<byte> data = stackalloc byte[2];
        ReadInto(offset, data);
        return BinaryPrimitives.ReadUInt16LittleEndian(data);
    }

    internal short ReadInt16(int offset) => unchecked((short)ReadUInt16(offset));

    internal NativeBattleEffectArena CopyTo(int sourceOffset, NativeBattleEffectArena destination, int destinationOffset, int count)
    {
        RequireRange(sourceOffset, count);
        Span<byte> data = stackalloc byte[count];
        Span<bool> known = stackalloc bool[count];
        for (int index = 0; index < count; index++)
        {
            data[index] = bytes[sourceOffset + index];
            known[index] = (knownMask & (1u << (sourceOffset + index))) != 0;
        }
        return destination.WriteWithKnownness(destinationOffset, data, known);
    }

    private void ReadInto(int offset, Span<byte> destination)
    {
        RequireRange(offset, destination.Length);
        for (int index = 0; index < destination.Length; index++)
        {
            if ((knownMask & (1u << (offset + index))) == 0)
            {
                throw new NotSupportedException("Native retained memory read reaches unobserved bytes.");
            }
        }
        for (int index = 0; index < destination.Length; index++)
        {
            destination[index] = bytes[offset + index];
        }
    }

    private void RequireRange(int offset, int count)
    {
        if (offset < 0 || count < 0 || offset > length - count)
        {
            throw new InvalidDataException("Native captured read leaves its bounded face or vertex bytes.");
        }
    }
}
