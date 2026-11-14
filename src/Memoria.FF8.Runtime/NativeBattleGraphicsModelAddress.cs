namespace Memoria.FF8.Runtime;

/// <summary>Addresses original model rows and engine-owned handles for hosted extension rows.</summary>
public static class NativeBattleGraphicsModelAddress
{
    public const int NativeRowCount = NativeBattleSlots.Count;
    public const int MaximumRowCount = ExpandedBattleFormation.MaximumAllies + ExpandedBattleFormation.MaximumEnemies;
    public const int RowSize = 156;

    private const uint NativeBase = 0x1D972C0;
    private const uint NativeEnd = NativeBase + NativeRowCount * RowSize;
    private const uint HostedBase = 0xF1000000;
    private const uint HostedEnd = HostedBase + (MaximumRowCount - NativeRowCount) * RowSize;

    public static uint Encode(int row)
    {
        if ((uint)row >= MaximumRowCount)
        {
            throw new ArgumentOutOfRangeException(nameof(row));
        }
        return row < NativeRowCount ? NativeBase + (uint)(row * RowSize) : HostedBase + (uint)((row - NativeRowCount) * RowSize);
    }

    public static int Decode(uint address, int actorCount = NativeRowCount)
    {
        if (!TryDecode(address, out int row, actorCount))
        {
            throw new ArgumentOutOfRangeException(nameof(address), address, "The address is not a model row base owned by this actor bank.");
        }
        return row;
    }

    public static bool TryDecode(uint address, out int row, int actorCount = NativeRowCount)
    {
        if (TryResolveField(address, out int candidate, out int offset, actorCount) && offset == 0)
        {
            row = candidate;
            return true;
        }
        row = -1;
        return false;
    }

    /// <summary>Resolves a field within one owned row. A zero-length field still starts at an owned byte.</summary>
    public static (int ActorIndex, int Offset) ResolveField(uint address, int byteLength, int actorCount = NativeRowCount)
    {
        if (!TryResolveField(address, out int row, out int offset, actorCount))
        {
            throw new ArgumentOutOfRangeException(nameof(address), address, "The address is not a byte owned by this actor bank.");
        }
        ValidateFieldLength(offset, byteLength);
        return (row, offset);
    }

    /// <summary>Identifies the row owning a field's first byte.</summary>
    public static bool TryResolveField(uint address, out int actorIndex, out int offset, int actorCount = NativeRowCount)
    {
        ValidateActorCount(actorCount);
        if (TryResolveAddress(address, out actorIndex, out offset) && actorIndex < actorCount)
        {
            return true;
        }
        actorIndex = -1;
        offset = -1;
        return false;
    }

    /// <summary>Encodes a field within one row. Offsets from zero through 155 identify owned bytes.</summary>
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

    private static void ValidateActorCount(int actorCount)
    {
        if (actorCount is < 1 or > MaximumRowCount)
        {
            throw new ArgumentOutOfRangeException(nameof(actorCount));
        }
    }

    private static void ValidateFieldLength(int offset, int byteLength)
    {
        if (byteLength < 0 || byteLength > RowSize - offset)
        {
            throw new ArgumentOutOfRangeException(nameof(byteLength), byteLength, "The field must stay within one model row.");
        }
    }
}
