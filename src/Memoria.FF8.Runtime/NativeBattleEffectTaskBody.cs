using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

/// <summary>A bounded task-body view of the operative physical authority, including a public arena transaction.</summary>
internal sealed class NativeBattleTaskBody(NativeBattleTaskBankOwner owner)
{
    private byte[] rawScratch = [];
    private bool[] knownScratch = [];

    private NativeBattleWorkingMemory Kernel => owner.RequireActive().Storage;

    private void RequireRange(int offset, int length)
    {
        owner.RequireActive();
        if (offset < owner.StorageOffset || length < 0 || offset - owner.StorageOffset > owner.BankLength - length)
        {
            throw new InvalidDataException("Native task access leaves its authoritative physical bank window.");
        }
    }

    private bool TryRoute(int offset, int length, out NativeBattleEffectArenaTransaction? transaction, out int relative)
    {
        RequireRange(offset, length);
        return NativeBattleEffectArenaTransaction.TryRoute(Kernel, offset, length, out transaction, out relative);
    }

    public byte ReadByte(int offset)
    {
        Span<byte> bytes = stackalloc byte[1];
        ReadInto(offset, bytes);
        return bytes[0];
    }
    public ushort ReadUInt16(int offset)
    {
        Span<byte> bytes = stackalloc byte[2];
        ReadInto(offset, bytes);
        return BinaryPrimitives.ReadUInt16LittleEndian(bytes);
    }
    public uint ReadUInt32(int offset)
    {
        Span<byte> bytes = stackalloc byte[4];
        ReadInto(offset, bytes);
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes);
    }
    public void ReadInto(int offset, Span<byte> destination)
    {
        if (TryRoute(offset, destination.Length, out var transaction, out int relative))
        {
            transaction!.ReadInto(relative, destination);
        }
        else
        {
            Kernel.ReadInto(offset, destination);
        }
    }
    public void ReadWithKnownness(int offset, Span<byte> destination, Span<bool> known)
    {
        if (known.Length != destination.Length)
        {
            throw new ArgumentException("Native task byte/knownness destinations must have equal lengths.", nameof(known));
        }
        if (TryRoute(offset, destination.Length, out var transaction, out int relative))
        {
            transaction!.ReadWithKnownness(relative, destination, known);
        }
        else
        {
            Kernel.ReadWithKnownness(offset, destination, known);
        }
    }
    public void WriteByte(int offset, byte value)
    {
        Span<byte> bytes = stackalloc byte[1];
        bytes[0] = value;
        Write(offset, bytes);
    }
    public void WriteUInt16(int offset, ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        Write(offset, bytes);
    }
    public void WriteUInt32(int offset, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        Write(offset, bytes);
    }
    public void Write(int offset, ReadOnlySpan<byte> bytes)
    {
        if (TryRoute(offset, bytes.Length, out var transaction, out int relative))
        {
            transaction!.Write(relative, bytes);
        }
        else
        {
            Kernel.Write(offset, bytes);
        }
    }
    public void WriteWithKnownness(int offset, ReadOnlySpan<byte> bytes, ReadOnlySpan<bool> known)
    {
        if (known.Length != bytes.Length)
        {
            throw new ArgumentException("Native task byte/knownness sources must have equal lengths.", nameof(known));
        }
        if (TryRoute(offset, bytes.Length, out var transaction, out int relative))
        {
            transaction!.WriteWithKnownness(relative, bytes, known);
        }
        else
        {
            Kernel.WriteWithKnownness(offset, bytes, known);
        }
    }
    public void WriteRetainedBytes(int offset, ReadOnlySpan<byte> bytes)
    {
        if (TryRoute(offset, bytes.Length, out var transaction, out int relative))
        {
            transaction!.WriteRetainedBytes(relative, bytes);
        }
        else
        {
            Kernel.WriteRetainedBytes(offset, bytes);
        }
    }
    public void MarkUnknown(int offset, int length)
    {
        if (TryRoute(offset, length, out var transaction, out int relative))
        {
            transaction!.MarkUnknown(relative, length);
        }
        else
        {
            Kernel.MarkUnknown(offset, length);
        }
    }
    public NativeBattleResourcePointer? ReadResourcePointer(int offset)
    {
        if (TryRoute(offset, 4, out var transaction, out int relative))
        {
            return transaction!.TryGetResourcePointer(relative, out NativeBattleResourcePointer? pointer) ? pointer : new(null, ReadUInt32(offset));
        }
        return Kernel.ReadResourcePointer(offset);
    }
    public void SetResourcePointer(int offset, NativeBattleResourcePointer? pointer)
    {
        if (TryRoute(offset, 4, out var transaction, out int relative))
        {
            transaction!.WriteResourcePointer(relative, pointer);
        }
        else
        {
            Kernel.SetResourcePointer(offset, pointer);
        }
    }
    public ReadOnlySpan<byte> BorrowRawReadOnly(int offset, int length)
    {
        if (!TryRoute(offset, length, out var transaction, out int relative))
        {
            return Kernel.BorrowRawReadOnly(offset, length);
        }
        //Only the requested immediate borrow is copied; this scratch never becomes a second writable owner.
        if (rawScratch.Length < length)
        {
            rawScratch = new byte[length];
            knownScratch = new bool[length];
        }
        transaction!.ReadWithKnownness(relative, rawScratch.AsSpan(0, length), knownScratch.AsSpan(0, length));
        return rawScratch.AsSpan(0, length);
    }
    public IEnumerator<KeyValuePair<int, NativeBattleResourcePointer?>> EnumerateResourcePointers() => Enumerate().GetEnumerator();
    private IEnumerable<KeyValuePair<int, NativeBattleResourcePointer?>> Enumerate()
    {
        if (TryRoute(owner.StorageOffset, owner.BankLength, out var transaction, out _))
        {
            foreach ((int offset, NativeBattleResourcePointer? pointer) in transaction!.EnumerateResourcePointers())
            {
                int absolute = checked(transaction.AbsoluteOffset + offset);
                if (absolute >= owner.StorageOffset && absolute + 4L <= (long)owner.StorageOffset + owner.BankLength)
                {
                    yield return new(absolute, pointer);
                }
            }
            yield break;
        }
        using var pointers = Kernel.EnumerateResourcePointers();
        while (pointers.MoveNext())
        {
            var pointer = pointers.Current;
            if (pointer.Key >= owner.StorageOffset && pointer.Key + 4L <= (long)owner.StorageOffset + owner.BankLength)
            {
                yield return pointer;
            }
        }
    }
}
