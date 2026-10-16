using System.Text;
using Albeoris.Games.FF8.MngrpBin.Abstractions;

namespace Albeoris.Games.FF8.MngrpBin.Internal;

/// <summary>Parses a <c>mngrp.bin</c>/<c>mngrphd.bin</c> pair into a <see cref="MngrpArchive"/>.</summary>
internal static class MngrpArchiveReader
{
    public static MngrpArchive Read(ReadOnlySpan<Byte> content, ReadOnlySpan<Byte> header, Encoding encoding)
    {
        MngrpSlotDirectory directory = MngrpSlotDirectory.Read(header);
        List<MngrpSlotLocation> occupied = [.. directory.EnumerateOccupied()];
        Dictionary<Int32, MngrpSlotLocation> occupiedBySlot = occupied.ToDictionary(location => location.SlotIndex);

        List<IMngrpSection> sections = new(occupied.Count);
        List<List<Int32>> blockStartsBySectionNumber = [];
        List<(MngrpTextBlockMapSection Placeholder, MngrpSlotLocation Location)> pendingMaps = [];

        foreach (MngrpSlotLocation location in occupied)
        {
            if (IsClaimedTextSlot(location.SlotIndex, occupiedBySlot))
            {
                continue;
            }

            try
            {
                IMngrpSection section = ReadSection(location, content, encoding, occupiedBySlot, blockStartsBySectionNumber, pendingMaps);
                section.ReservedSize = location.Size;
                if (section is MngrpTextRecordSection records && occupiedBySlot.TryGetValue(records.TextSlotIndex, out MngrpSlotLocation textLocation))
                {
                    records.TextReservedSize = textLocation.Size;
                }

                sections.Add(section);
            }
            catch (InvalidDataException exception)
            {
                throw new InvalidDataException($"Slot {location.SlotIndex}: {exception.Message}", exception);
            }
        }

        // Text block maps reference blocks in sections that occupy later slots, so they are
        // parsed only after every text-block section reported its block offsets.
        foreach ((MngrpTextBlockMapSection placeholder, MngrpSlotLocation location) in pendingMaps)
        {
            try
            {
                MngrpTextBlockMapSection map = MngrpTextBlockMapCodec.Read(location.SlotIndex, GetBody(location, content), blockStartsBySectionNumber);
                map.ReservedSize = location.Size;
                sections[sections.IndexOf(placeholder)] = map;
            }
            catch (InvalidDataException exception)
            {
                throw new InvalidDataException($"Slot {location.SlotIndex}: {exception.Message}", exception);
            }
        }

        return new MngrpArchive(sections, directory, encoding);
    }

    private static IMngrpSection ReadSection(
        MngrpSlotLocation location,
        ReadOnlySpan<Byte> content,
        Encoding encoding,
        Dictionary<Int32, MngrpSlotLocation> occupiedBySlot,
        List<List<Int32>> blockStartsBySectionNumber,
        List<(MngrpTextBlockMapSection Placeholder, MngrpSlotLocation Location)> pendingMaps)
    {
        ReadOnlySpan<Byte> body = GetBody(location, content);
        MngrpSectionDescriptor descriptor = MngrpSectionCatalog.Get(location.SlotIndex);
        switch (descriptor.Layout)
        {
            case MngrpSectionLayout.StringTable:
            {
                MngrpStringTableSection section = new(location.SlotIndex);
                MngrpStringTable table = MngrpStringTableCodec.Read(body, encoding, trimLastEntry: true);
                section.Table.Entries.AddRange(table.Entries);
                section.Table.LeadingBytes = table.LeadingBytes;
                return section;
            }

            case MngrpSectionLayout.StringTableGroup:
                return MngrpStringTableGroupCodec.Read(location.SlotIndex, body, encoding);

            case MngrpSectionLayout.TextBlockList:
            {
                MngrpTextBlockSection section = MngrpTextBlockCodec.Read(location.SlotIndex, body, encoding, out List<Int32> blockStarts);
                blockStartsBySectionNumber.Add(blockStarts);
                return section;
            }

            case MngrpSectionLayout.TextBlockMap:
            {
                MngrpTextBlockMapSection placeholder = new(location.SlotIndex);
                pendingMaps.Add((placeholder, location));
                return placeholder;
            }

            case MngrpSectionLayout.TextRecordList when descriptor.TextSlot is Int32 textSlot && occupiedBySlot.TryGetValue(textSlot, out MngrpSlotLocation textLocation):
                return MngrpTextRecordCodec.Read(location.SlotIndex, textSlot, body, GetBody(textLocation, content), encoding);

            default:
                // Also the fallback for a record section whose companion text slot is vacant:
                // without the texts the records cannot be represented, only preserved.
                return new MngrpOpaqueSection(location.SlotIndex, body.ToArray());
        }
    }

    /// <summary>Whether the slot stores the texts of an occupied text-record section, which consumes it.</summary>
    private static Boolean IsClaimedTextSlot(Int32 slotIndex, Dictionary<Int32, MngrpSlotLocation> occupiedBySlot)
    {
        Int32 owningRecordSlot = MngrpSectionCatalog.GetOwningRecordSlot(slotIndex);
        return owningRecordSlot >= 0 && occupiedBySlot.ContainsKey(owningRecordSlot);
    }

    private static ReadOnlySpan<Byte> GetBody(MngrpSlotLocation location, ReadOnlySpan<Byte> content)
    {
        if (location.Offset < 0 || location.Size < 0 || location.Offset + (Int64)location.Size > content.Length)
        {
            throw new InvalidDataException($"Slot {location.SlotIndex} points at bytes {location.Offset}..{location.Offset + (Int64)location.Size}, outside the {content.Length}-byte content file.");
        }

        return content.Slice(location.Offset, location.Size);
    }
}
