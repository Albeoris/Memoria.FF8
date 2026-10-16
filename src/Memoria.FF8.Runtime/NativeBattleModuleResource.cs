using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>One original off_C7E554 binding, including its native sprite frame directory and callback pair.</summary>
public sealed record NativeBattleModuleResource(byte Id, uint Address, uint WrapperCallback, uint Callback, ImmutableArray<byte> Bytes)
{
    public byte Kind => Bytes.Length >= 4 ? Bytes[3] : throw new InvalidDataException("Native module resource lacks its type DWORD.");

    public void Validate()
    {
        if (Bytes.IsDefault || Bytes.Length < 4 || Kind > 4 || Kind != 0 && Bytes.Length != 4)
        {
            throw new InvalidDataException("Native module resource lacks its original type and payload.");
        }
        if (Kind != 0)
        {
            return;
        }
        if (Bytes.Length < 12)
        {
            throw new InvalidDataException("Native module sprite lacks its frame directory.");
        }
        int count = BinaryPrimitives.ReadUInt16LittleEndian(Bytes.AsSpan().Slice(8));
        if (count == 0 || 10L + 2L * (count + 1) > Bytes.Length)
        {
            throw new InvalidDataException("Native module sprite frame directory exceeds its resource.");
        }
        for (int phase = 0; phase < count; phase++)
        {
            _ = SpriteFrame(phase);
        }
    }

    public NativeBattleModuleSpriteFrame SpriteFrame(int phase)
    {
        if (Kind != 0 || Bytes.Length < 12 || (uint)phase >= BinaryPrimitives.ReadUInt16LittleEndian(Bytes.AsSpan().Slice(8)))
        {
            throw new InvalidDataException("Native sprite phase does not identify an original resource frame.");
        }
        int offset = BinaryPrimitives.ReadUInt16LittleEndian(Bytes.AsSpan().Slice(10 + phase * 2));
        short next = BinaryPrimitives.ReadInt16LittleEndian(Bytes.AsSpan().Slice(12 + phase * 2));
        if (offset + 4L > Bytes.Length)
        {
            throw new InvalidDataException("Native sprite frame pointer exceeds its original resource.");
        }
        uint descriptor = BinaryPrimitives.ReadUInt32LittleEndian(Bytes.AsSpan().Slice(offset));
        uint count = descriptor & 0x7FFFFFFF;
        if (offset + 4L + 20L * count > Bytes.Length)
        {
            throw new InvalidDataException("Native sprite quad data exceeds its original resource.");
        }
        return new(next, (descriptor & 0x80000000) != 0, Bytes.AsSpan().Slice(offset + 4, checked((int)count * 20)).ToArray().ToImmutableArray());
    }
}

public sealed record NativeBattleModuleSpriteFrame(short NextOffset, bool ExtendedPacket, ImmutableArray<byte> Quads);
