namespace Memoria.FF8.Runtime;

/// <summary>Addresses the seven original graphics model cells in native memory.</summary>
public static class NativeBattleGraphicsModelAddress
{
    public const int NativeRowCount = NativeBattleSlots.Count;
    public const int RowSize = 156;

    private const uint NativeBase = 0x1D972C0;
    private const uint NativeEnd = NativeBase + NativeRowCount * RowSize;

    public static uint EncodeOriginalCell(NativeBattleModelSlot slot)
    {
        return NativeBase + (uint)(slot.Offset * RowSize);
    }

    public static NativeBattleModelSlot DecodeOriginalCell(uint address)
    {
        if (!TryDecodeOriginalCell(address, out NativeBattleModelSlot slot))
        {
            throw new ArgumentOutOfRangeException(nameof(address), address, "The address is not an original model cell base.");
        }
        return slot;
    }

    public static bool TryDecodeOriginalCell(uint address, out NativeBattleModelSlot slot)
    {
        if (TryResolveOriginalField(address, out NativeBattleModelSlot candidate, out int offset) && offset == 0)
        {
            slot = candidate;
            return true;
        }
        slot = default;
        return false;
    }

    /// <summary>Resolves a field within one original cell. A zero-length field still starts at an owned byte.</summary>
    public static (NativeBattleModelSlot Slot, int Offset) ResolveOriginalField(uint address, int byteLength)
    {
        if (!TryResolveOriginalField(address, out NativeBattleModelSlot slot, out int offset))
        {
            throw new ArgumentOutOfRangeException(nameof(address), address, "The address is outside the original model cells.");
        }
        ValidateFieldLength(offset, byteLength);
        return (slot, offset);
    }

    /// <summary>Identifies the original cell owning a field's first byte.</summary>
    public static bool TryResolveOriginalField(uint address, out NativeBattleModelSlot slot, out int offset)
    {
        if (address is >= NativeBase and < NativeEnd)
        {
            int relative = (int)(address - NativeBase);
            slot = NativeBattleModelSlot.Decode((byte)(relative / RowSize));
            offset = relative % RowSize;
            return true;
        }
        slot = default;
        offset = -1;
        return false;
    }

    public static uint EncodeOriginalField(NativeBattleModelSlot slot, int offset, int length)
    {
        if ((uint)offset >= RowSize)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }
        ValidateFieldLength(offset, length);
        return EncodeOriginalCell(slot) + (uint)offset;
    }

    private static void ValidateFieldLength(int offset, int byteLength)
    {
        if (byteLength < 0 || byteLength > RowSize - offset)
        {
            throw new ArgumentOutOfRangeException(nameof(byteLength), byteLength, "The field must stay within one original model cell.");
        }
    }
}
