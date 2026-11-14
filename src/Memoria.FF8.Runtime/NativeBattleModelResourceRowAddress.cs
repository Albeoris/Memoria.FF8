namespace Memoria.FF8.Runtime;

/// <summary>Identifies original resource aliases and hosted row handles, independently of the resources they select.</summary>
public static class NativeBattleModelResourceRowAddress
{
    public const int NativeRowCount = 11;
    public const int RowSize = 52;
    public const uint NativeBase = 0x1D99774;
    public const uint HostedBase = 0xF3000000;
    // This codec owns the address range from HostedBase through uint.MaxValue.
    public const int MaximumEncodedRowCount = NativeRowCount + (int)(((ulong)uint.MaxValue + 1 - HostedBase) / RowSize);

    private const uint NativeEnd = checked(NativeBase + NativeRowCount * RowSize);

    public static int CapacityForRoster(BattleModelRoster roster)
    {
        ArgumentNullException.ThrowIfNull(roster);
        return Math.Max(NativeRowCount, checked(2 * roster.Party.Count() + roster.Enemies.Count() + 1));
    }

    public static uint Encode(int row, int capacity = NativeRowCount)
    {
        ValidateCapacity(capacity);
        if ((uint)row >= capacity)
        {
            throw new ArgumentOutOfRangeException(nameof(row));
        }
        return row < NativeRowCount ? checked(NativeBase + (uint)row * RowSize) : checked(HostedBase + (uint)(row - NativeRowCount) * RowSize);
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

    /// <summary>Resolves a complete span within one owned row. A zero-length span still starts at an owned byte.</summary>
    public static bool TryResolveField(uint address, int length, out int row, out int offset, int capacity = NativeRowCount)
    {
        ValidateCapacity(capacity);
        row = -1;
        offset = -1;
        if (length < 0)
        {
            return false;
        }

        int firstRow;
        uint relative;
        if (address is >= NativeBase and < NativeEnd)
        {
            firstRow = 0;
            relative = address - NativeBase;
        }
        else if (address >= HostedBase)
        {
            firstRow = NativeRowCount;
            relative = address - HostedBase;
        }
        else
        {
            return false;
        }

        if (relative >= checked((uint)(capacity - firstRow) * RowSize))
        {
            return false;
        }
        int fieldOffset = (int)(relative % RowSize);
        if (length > RowSize - fieldOffset)
        {
            return false;
        }
        row = checked(firstRow + (int)(relative / RowSize));
        offset = fieldOffset;
        return true;
    }

    internal static void ValidateCapacity(int capacity)
    {
        if (capacity is < NativeRowCount or > MaximumEncodedRowCount)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }
    }
}
