namespace Memoria.FF8.Runtime;

/// <summary>Codec for the original physical contact rows.</summary>
public static class NativeBattleContactAddress
{
    public const int NativeRowCount = 49;
    public const int RowSize = 24;

    private const uint NativeBase = 0x1D28344;
    private const uint NativeEnd = NativeBase + NativeRowCount * RowSize;

    public static uint EncodeOriginal(BattleContactOccurrence occurrence)
    {
        if (occurrence.Ordinal >= NativeRowCount)
        {
            throw new ArgumentOutOfRangeException(nameof(occurrence));
        }
        return EncodeOriginalCursor(occurrence.AsCursor());
    }

    public static BattleContactOccurrence DecodeOriginal(uint address)
    {
        if (!TryDecodeOriginal(address, out BattleContactOccurrence occurrence))
        {
            throw new ArgumentOutOfRangeException(nameof(address), address, "The address is not an original contact row base.");
        }
        return occurrence;
    }

    public static bool TryDecodeOriginal(uint address, out BattleContactOccurrence occurrence)
    {
        if (TryResolveOriginalField(address, 0, out occurrence, out int offset) && offset == 0)
        {
            return true;
        }
        occurrence = default;
        return false;
    }

    /// <summary>Encodes an aligned original contact position, including its one-past cursor.</summary>
    public static uint EncodeOriginalCursor(BattleContactCursor cursor)
    {
        if (cursor.Ordinal > NativeRowCount)
        {
            throw new ArgumentOutOfRangeException(nameof(cursor));
        }
        return NativeBase + (uint)(cursor.Ordinal * RowSize);
    }

    public static BattleContactCursor DecodeOriginalCursor(uint address)
    {
        return address == NativeEnd ? new(NativeRowCount) : DecodeOriginal(address).AsCursor();
    }

    public static uint OffsetOriginalCursor(uint address, int delta)
    {
        return EncodeOriginalCursor(DecodeOriginalCursor(address).Advance(delta));
    }

    /// <summary>Resolves a complete original contact field. Even a zero-length field must start in a row.</summary>
    public static (BattleContactOccurrence Occurrence, int Offset) ResolveOriginalField(uint address, int byteLength)
    {
        if (!TryResolveOriginalField(address, 0, out BattleContactOccurrence occurrence, out int offset))
        {
            throw new ArgumentOutOfRangeException(nameof(address), address, "The address is outside the original contact rows.");
        }
        ValidateFieldLength(offset, byteLength);
        return (occurrence, offset);
    }

    public static bool TryResolveOriginalField(uint address, int byteLength, out BattleContactOccurrence occurrence, out int offset)
    {
        if (address is >= NativeBase and < NativeEnd)
        {
            int relative = (int)(address - NativeBase);
            int fieldOffset = relative % RowSize;
            if (IsFieldLengthValid(fieldOffset, byteLength))
            {
                occurrence = new(relative / RowSize);
                offset = fieldOffset;
                return true;
            }
        }
        occurrence = default;
        offset = -1;
        return false;
    }

    public static uint EncodeOriginalField(BattleContactOccurrence occurrence, int offset, int byteLength)
    {
        uint address = EncodeOriginal(occurrence);
        if ((uint)offset >= RowSize)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }
        ValidateFieldLength(offset, byteLength);
        return address + (uint)offset;
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
