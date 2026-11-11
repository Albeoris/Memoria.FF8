using System.Buffers.Binary;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

/// <summary>A classic PC .ff7 container. Unknown header and slot bytes survive editing.</summary>
public sealed class Ff7NativeSaveBank
{
    public const int SlotCount = 15;
    public const int HeaderSize = 9;
    public const int FileSize = HeaderSize + SlotCount * Ff7NativeSaveSlot.Size;
    private readonly byte[] data;

    private Ff7NativeSaveBank(byte[] data)
    {
        this.data = data;
    }

    public static Ff7NativeSaveBank Create()
    {
        byte[] data = new byte[FileSize];
        new byte[] { 0x71, 0x73, 0x27, 0x06 }.CopyTo(data, 0);
        return new(data);
    }

    public static Ff7NativeSaveBank Decode(ReadOnlySpan<byte> source)
    {
        if (source.Length != FileSize || !source[..4].SequenceEqual(new byte[] { 0x71, 0x73, 0x27, 0x06 }))
        {
            throw new InvalidDataException("Expected a classic PC FFVII save bank with fifteen slots.");
        }
        return new(source.ToArray());
    }

    public byte[] Encode() => data.ToArray();

    public Ff7NativeSaveSlot GetSlot(int index)
    {
        CheckIndex(index);
        return Ff7NativeSaveSlot.Decode(data.AsSpan(HeaderSize + index * Ff7NativeSaveSlot.Size, Ff7NativeSaveSlot.Size));
    }

    public Ff7NativeSaveBank WithSlot(int index, Ff7NativeSaveSlot? slot)
    {
        CheckIndex(index);
        byte[] result = data.ToArray();
        Span<byte> destination = result.AsSpan(HeaderSize + index * Ff7NativeSaveSlot.Size, Ff7NativeSaveSlot.Size);
        if (slot is null)
        {
            destination.Clear();
        }
        else
        {
            slot.Encode().CopyTo(destination);
        }
        return new(result);
    }

    private static void CheckIndex(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, SlotCount);
    }
}

public sealed class Ff7NativeSaveSlot
{
    public const int Size = 0x10F4;
    private readonly byte[] data;

    private Ff7NativeSaveSlot(byte[] data)
    {
        this.data = data;
    }

    public bool IsEmpty => data.All(value => value == 0) || BinaryPrimitives.ReadUInt16LittleEndian(data) == 0x4D1D;
    public bool ChecksumValid => BinaryPrimitives.ReadUInt16LittleEndian(data) == ComputeChecksum(data);
    public int Level => data[4];
    public string Name => ReadText(data.AsSpan(8, 16));
    public string Location => ReadText(data.AsSpan(0x28, 32));
    public uint Gil => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(0x20));
    public uint ElapsedSeconds => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(0x24));
    public ushort Module => BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(0xB94));
    public ushort MapId => BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(0xB96));

    public byte[] Encode() => data.ToArray();

    public static Ff7NativeSaveSlot Decode(ReadOnlySpan<byte> source)
    {
        if (source.Length != Size)
        {
            throw new InvalidDataException("A native FFVII save slot must contain 4340 bytes.");
        }
        return new(source.ToArray());
    }

    public static ushort ComputeChecksum(ReadOnlySpan<byte> source)
    {
        if (source.Length != Size)
        {
            throw new InvalidDataException("A native FFVII save slot must contain 4340 bytes.");
        }
        ushort checksum = 0xFFFF;
        foreach (byte value in source[4..])
        {
            checksum ^= (ushort)(value << 8);
            for (int bit = 0; bit < 8; bit++)
            {
                checksum = unchecked((ushort)((checksum << 1) ^ ((checksum & 0x8000) != 0 ? 0x1021 : 0)));
            }
        }
        return (ushort)(checksum ^ 0xFFFF);
    }

    internal static string ReadText(ReadOnlySpan<byte> source)
    {
        return string.Concat(FieldText.Read(source).Select(token => token.Text));
    }
}
