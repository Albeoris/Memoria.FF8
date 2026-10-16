using System.Buffers.Binary;

namespace Albeoris.Games.FF8.MngrpBin.Internal;

/// <summary>
/// The <c>mngrphd.bin</c> file: a fixed table of 256 slots, each an eight-byte
/// (offset, size) pair. An occupied slot stores the section's byte offset within
/// <c>mngrp.bin</c> plus one (so a section at offset zero can be told apart from a vacant slot)
/// and its sector-aligned size. Vacant slots appear with two encodings in shipped files —
/// (0xFFFFFFFF, 0) between occupied slots and (0, 0) after the last one — so their raw values
/// are preserved and written back verbatim.
/// </summary>
internal sealed class MngrpSlotDirectory
{
    public const Int32 SlotCount = 256;
    private const Int32 SlotSize = 8;
    public const Int32 FileLength = SlotCount * SlotSize;

    private readonly (UInt32 RawOffset, UInt32 Size)[] _slots;

    private MngrpSlotDirectory((UInt32 RawOffset, UInt32 Size)[] slots)
    {
        _slots = slots;
    }

    public static MngrpSlotDirectory Read(ReadOnlySpan<Byte> header)
    {
        if (header.Length != FileLength)
        {
            throw new InvalidDataException($"Unexpected header file size: expected {FileLength} bytes, found {header.Length}.");
        }

        (UInt32 RawOffset, UInt32 Size)[] slots = new (UInt32, UInt32)[SlotCount];
        for (Int32 slotIndex = 0; slotIndex < SlotCount; slotIndex++)
        {
            ReadOnlySpan<Byte> slot = header.Slice(slotIndex * SlotSize, SlotSize);
            slots[slotIndex] = (BinaryPrimitives.ReadUInt32LittleEndian(slot), BinaryPrimitives.ReadUInt32LittleEndian(slot[4..]));
        }

        return new MngrpSlotDirectory(slots);
    }

    /// <summary>Lists every occupied slot with its real byte offset and size, in ascending slot order.</summary>
    public IEnumerable<MngrpSlotLocation> EnumerateOccupied()
    {
        for (Int32 slotIndex = 0; slotIndex < SlotCount; slotIndex++)
        {
            (UInt32 rawOffset, UInt32 size) = _slots[slotIndex];
            Boolean isVacantOffset = rawOffset is UInt32.MaxValue or 0;
            if (isVacantOffset && size == 0)
            {
                continue;
            }

            if (isVacantOffset || size == 0)
            {
                throw new InvalidDataException($"Slot {slotIndex} is half-vacant: it stores offset {rawOffset} with size {size}.");
            }

            yield return new MngrpSlotLocation(slotIndex, (Int32)(rawOffset - 1), (Int32)size);
        }
    }

    /// <summary>
    /// Serializes a header holding <paramref name="occupied"/>, keeping this directory's raw
    /// values for every slot not present in the list.
    /// </summary>
    public Byte[] Write(IReadOnlyList<MngrpSlotLocation> occupied)
    {
        (UInt32 RawOffset, UInt32 Size)[] slots = [.. _slots];
        foreach (MngrpSlotLocation location in occupied)
        {
            slots[location.SlotIndex] = (checked((UInt32)location.Offset + 1), (UInt32)location.Size);
        }

        Byte[] header = new Byte[FileLength];
        for (Int32 slotIndex = 0; slotIndex < SlotCount; slotIndex++)
        {
            Span<Byte> slot = header.AsSpan(slotIndex * SlotSize, SlotSize);
            BinaryPrimitives.WriteUInt32LittleEndian(slot, slots[slotIndex].RawOffset);
            BinaryPrimitives.WriteUInt32LittleEndian(slot[4..], slots[slotIndex].Size);
        }

        return header;
    }
}

/// <summary>An occupied header slot: the section's slot number, byte offset and size.</summary>
internal readonly record struct MngrpSlotLocation(Int32 SlotIndex, Int32 Offset, Int32 Size);
