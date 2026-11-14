namespace Memoria.FF8.Runtime;

/// <summary>Addresses original contact rows and engine-owned handles for hosted contact rows.</summary>
public static class NativeBattleContactAddress
{
    public const int NativeRowCount = NativeBattleActionPacketsState.ContactCapacity;
    public const int MaximumRowCount = checked(NativeBattleActionPacketsState.HeaderCapacity * NativeBattleGraphicsModelAddress.MaximumRowCount * byte.MaxValue);
    public const int RowSize = 24;

    private const uint NativeBase = 0x1D28344;
    private const uint NativeEnd = NativeBase + NativeRowCount * RowSize;
    private const uint HostedBase = 0xF2000000;
    private const uint HostedEnd = HostedBase + (MaximumRowCount - NativeRowCount) * RowSize;

    public static uint Encode(int row)
    {
        if ((uint)row >= MaximumRowCount)
        {
            throw new ArgumentOutOfRangeException(nameof(row));
        }
        return RowAddress(row);
    }

    public static int Decode(uint address, int capacity = NativeRowCount)
    {
        if (!TryDecode(address, out int row, capacity))
        {
            throw new ArgumentOutOfRangeException(nameof(address), address, "The address is not a contact row base owned by this arena.");
        }
        return row;
    }

    public static bool TryDecode(uint address, out int row, int capacity = NativeRowCount)
    {
        if (TryResolveField(address, 0, out int candidate, out int offset, capacity) && offset == 0)
        {
            row = candidate;
            return true;
        }
        row = -1;
        return false;
    }

    /// <summary>Encodes an aligned position in the arena, including its one-past cursor.</summary>
    public static uint EncodeCursor(int row, int capacity = NativeRowCount)
    {
        ValidateCapacity(capacity);
        if ((uint)row > capacity)
        {
            throw new ArgumentOutOfRangeException(nameof(row));
        }
        return row == NativeRowCount && capacity == NativeRowCount ? NativeEnd : RowAddress(row);
    }

    public static int DecodeCursor(uint address, int capacity = NativeRowCount)
    {
        if (address == EncodeCursor(capacity, capacity))
        {
            return capacity;
        }
        return Decode(address, capacity);
    }

    public static uint OffsetCursor(uint address, int delta, int capacity = NativeRowCount)
    {
        int row = DecodeCursor(address, capacity);
        return EncodeCursor(checked(row + delta), capacity);
    }

    /// <summary>Resolves a complete field within one owned row. A zero-length field still starts at an owned byte.</summary>
    public static (int ContactIndex, int Offset) ResolveField(uint address, int byteLength, int capacity = NativeRowCount)
    {
        if (!TryResolveField(address, 0, out int row, out int offset, capacity))
        {
            throw new ArgumentOutOfRangeException(nameof(address), address, "The address is not a byte owned by this contact arena.");
        }
        ValidateFieldLength(offset, byteLength);
        return (row, offset);
    }

    public static bool TryResolveField(uint address, int byteLength, out int contactIndex, out int offset, int capacity = NativeRowCount)
    {
        ValidateCapacity(capacity);
        if (TryResolveAddress(address, out contactIndex, out offset) && contactIndex < capacity && IsFieldLengthValid(offset, byteLength))
        {
            return true;
        }
        contactIndex = -1;
        offset = -1;
        return false;
    }

    public static uint EncodeField(int row, int offset, int byteLength)
    {
        uint address = Encode(row);
        if ((uint)offset >= RowSize)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }
        ValidateFieldLength(offset, byteLength);
        return address + (uint)offset;
    }

    private static uint RowAddress(int row)
    {
        return row < NativeRowCount ? NativeBase + (uint)(row * RowSize) : HostedBase + (uint)((row - NativeRowCount) * RowSize);
    }

    private static bool TryResolveAddress(uint address, out int row, out int offset)
    {
        if (address is >= NativeBase and < NativeEnd)
        {
            int relative = (int)(address - NativeBase);
            row = relative / RowSize;
            offset = relative % RowSize;
            return true;
        }
        if (address is >= HostedBase and < HostedEnd)
        {
            int relative = (int)(address - HostedBase);
            row = NativeRowCount + relative / RowSize;
            offset = relative % RowSize;
            return true;
        }
        row = -1;
        offset = -1;
        return false;
    }

    private static void ValidateCapacity(int capacity)
    {
        if (capacity is < 1 or > MaximumRowCount)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }
    }

    private static bool IsFieldLengthValid(int offset, int byteLength) => byteLength >= 0 && byteLength <= RowSize - offset;

    private static void ValidateFieldLength(int offset, int byteLength)
    {
        if (!IsFieldLengthValid(offset, byteLength))
        {
            throw new ArgumentOutOfRangeException(nameof(byteLength), byteLength, "The field must stay within one contact row.");
        }
    }
}
