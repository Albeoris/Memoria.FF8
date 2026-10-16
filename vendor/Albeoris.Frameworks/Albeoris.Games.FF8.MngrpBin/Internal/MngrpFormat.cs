namespace Albeoris.Games.FF8.MngrpBin.Internal;

/// <summary>Sizes and helpers shared by every <c>mngrp.bin</c> layout.</summary>
internal static class MngrpFormat
{
    /// <summary>Every section starts and ends on a boundary of this many bytes.</summary>
    public const Int32 SectorSize = 0x800;

    /// <summary>Rounds <paramref name="length"/> up to the next sector boundary.</summary>
    public static Int32 AlignToSector(Int32 length) => checked(length + SectorSize - 1) / SectorSize * SectorSize;

    /// <summary>Rounds <paramref name="position"/> up to the next four-byte boundary.</summary>
    public static Int32 AlignToFour(Int32 position) => checked(position + 3) & ~3;

    /// <summary>Narrows a computed value to the 16 bits the file format stores it in.</summary>
    public static UInt16 ToUInt16(Int32 value, String description)
    {
        if (value is < 0 or > UInt16.MaxValue)
        {
            throw new InvalidOperationException($"The {description} ({value}) does not fit in 16 bits.");
        }

        return (UInt16)value;
    }

    /// <summary>Returns <paramref name="span"/> without its trailing zero bytes.</summary>
    public static ReadOnlySpan<Byte> TrimTrailingZeros(ReadOnlySpan<Byte> span)
    {
        return span[..(span.LastIndexOfAnyExcept((Byte)0) + 1)];
    }
}
