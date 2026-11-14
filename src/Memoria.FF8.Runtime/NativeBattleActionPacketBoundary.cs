using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleActionPacketBoundary
{
    internal static short ReadActiveCameraFlag50A6E0(NativeBattleActionPacketsState packets)
    {
        BattleContactCursor cursor = packets.ActiveContact ?? throw new NotSupportedException("Native50A6E0 requires its active contact cursor.");
        BattleContactOccurrence occurrence = new(cursor.Ordinal);
        if (packets.FindStoredContact(occurrence) is { } live)
        {
            return (short)(live.Primary.HitFlags & 2);
        }
        if (cursor.Ordinal >= NativeBattleRetainedActionPackets.ContactCapacity ||
            packets.RetainedStorage.Contacts[cursor.Ordinal] is not { } retained ||
            (retained.UnknownBitsAt(3) & 2) != 0)
        {
            throw new NotSupportedException("Native50A6E0 requires an observed primary contact flag bit.");
        }
        return (short)(retained.Primary.HitFlags3 & 2);
    }

    internal static NativeBattleRetainedActionPackets PrepareHeader(NativeBattleRetainedActionPackets retained, BattleActionHeaderReference reference, NativeBattleActionHeaderSlot header, NativeBattleModelAliases aliases)
    {
        int index = reference.Ordinal;
        if (index >= NativeBattleRetainedActionPackets.HeaderCapacity)
        {
            return retained;
        }
        NativeBattleRetainedActionHeaderSlot previous = retained.Headers[index];
        byte[] bytes = previous.Payload.ToArray();
        uint unknown = previous.UnknownByteMask & ~0xFEu;
        if (aliases.TryEncodeExact(header.Source, out byte source))
        {
            bytes[0] = source;
            unknown &= ~1u;
        }
        else
        {
            unknown |= 1;
        }
        bytes[1] = header.Command;
        bytes[2] = header.Sequence;
        bytes[3] = unchecked((byte)header.CameraChoice);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), header.Action);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), header.Effect);

        int? first = EncodeContactCursor(header.FirstContact);
        if (first is int firstIndex)
        {
            uint pointer = NativeBattleRetainedActionPackets.ContactBase + (uint)(firstIndex * NativeBattleRetainedActionPackets.ContactSize);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), pointer);
            unknown &= ~0xF00u;
        }
        else
        {
            unknown |= 0xF00u;
        }

        if (header.Data is null)
        {
            bytes.AsSpan(12, 4).Clear();
            unknown &= ~0xF000u;
        }
        else
        {
            unknown |= 0xF000u;
        }

        NativeBattleRetainedActionHeaderSlot written = new(ImmutableArray.CreateRange(bytes), unknown, first, header.Data);
        return retained with { Headers = retained.Headers.SetItem(index, written) };
    }

    internal static NativeBattleRetainedActionPackets PublishHeader(NativeBattleRetainedActionPackets retained, BattleActionHeaderReference reference, int targetCount, int actionCount)
    {
        retained = WriteHeaderScalar(retained, reference, 16, targetCount <= byte.MaxValue ? (byte)targetCount : null);
        return actionCount <= byte.MaxValue
            ? retained with { ActionCount = (byte)actionCount, UnknownCountMask = (byte)(retained.UnknownCountMask & ~1) }
            : retained with { UnknownCountMask = (byte)(retained.UnknownCountMask | 1) };
    }

    internal static NativeBattleRetainedActionPackets WriteRepeatMode(NativeBattleRetainedActionPackets retained, BattleActionHeaderReference reference, byte repeatModeMinusOne)
    {
        return WriteHeaderScalar(retained, reference, 17, repeatModeMinusOne);
    }

    private static NativeBattleRetainedActionPackets WriteHeaderScalar(NativeBattleRetainedActionPackets retained, BattleActionHeaderReference reference, int offset, byte? value)
    {
        if (reference.Ordinal >= NativeBattleRetainedActionPackets.HeaderCapacity)
        {
            return retained;
        }
        NativeBattleRetainedActionHeaderSlot slot = retained.Headers[reference.Ordinal];
        uint bit = 1u << offset;
        NativeBattleRetainedActionHeaderSlot written = value is byte known
            ? slot with { Payload = slot.Payload.SetItem(offset, known), UnknownByteMask = slot.UnknownByteMask & ~bit }
            : slot with { UnknownByteMask = slot.UnknownByteMask | bit };
        return retained with { Headers = retained.Headers.SetItem(reference.Ordinal, written) };
    }

    internal static NativeBattleRetainedActionPackets ActivateHeader(NativeBattleRetainedActionPackets retained, BattleActionHeaderReference reference, BattleContactCursor cursor)
    {
        return retained with
        {
            ActiveHeaderIndex = reference.Ordinal < NativeBattleRetainedActionPackets.HeaderCapacity ? reference.Ordinal : null,
            ActiveContactIndex = EncodeContactCursor(cursor)
        };
    }

    internal static NativeBattleRetainedActionPackets AdvanceContact(NativeBattleRetainedActionPackets retained, BattleContactCursor cursor)
    {
        return retained with { ActiveContactIndex = EncodeContactCursor(cursor) };
    }

    private static int? EncodeContactCursor(BattleContactCursor cursor)
    {
        return cursor.Ordinal <= NativeBattleRetainedActionPackets.ContactCapacity ? cursor.Ordinal : null;
    }

    internal static NativeBattleRetainedActionPackets CompleteContactWrites(NativeBattleRetainedActionPackets retained, ImmutableArray<NativeBattleRawContactPacket?> contacts, int contactCount, int? lastEncodableCount)
    {
        return contactCount <= byte.MaxValue
            ? retained with { Contacts = contacts, ContactCount = contactCount, UnknownCountMask = (byte)(retained.UnknownCountMask & ~2) }
            : retained with { Contacts = contacts, ContactCount = lastEncodableCount ?? retained.ContactCount, UnknownCountMask = (byte)(retained.UnknownCountMask | 2) };
    }

    internal static NativeBattleRawContactPacket WriteContact(NativeBattleContactPacket contact, NativeBattleRawContactPacket? retained, NativeBattleModelAliases aliases)
    {
        byte[]? unknown = null;
        NativeBattleRawContactHalf primary = WriteHalf(contact.Primary, retained?.Primary, 0);
        NativeBattleRawContactHalf secondary = contact.Secondary is { } present ? WriteHalf(present, retained?.Secondary, 12) : new(byte.MaxValue, 0, 0, 0, 0, 0, 0);
        return new(primary, secondary) { UnknownBits = unknown is null ? default : ImmutableArray.CreateRange(unknown) };

        NativeBattleRawContactHalf WriteHalf(NativeBattleContactHalf half, NativeBattleRawContactHalf? previous, int offset)
        {
            if (!aliases.TryEncodeExact(half.Recipient, out byte recipient))
            {
                recipient = previous?.ActorIndex ?? 0;
                (unknown ??= new byte[NativeBattleRetainedActionPackets.ContactSize])[offset] = byte.MaxValue;
            }
            byte flags = half.HitFlags;
            if (half.CoverSource is { } cover)
            {
                if (aliases.TryEncodeExact(cover, out byte coverSlot) && coverSlot < 4)
                {
                    flags |= (byte)(coverSlot << 6);
                }
                else
                {
                    flags |= (byte)((previous?.HitFlags3 ?? 0) & 0xC0);
                    (unknown ??= new byte[NativeBattleRetainedActionPackets.ContactSize])[offset + 3] = 0xC0;
                }
            }
            return new(recipient, half.Sequence, half.Flags2, flags, half.SavedStatuses4, half.Amount6, half.BattleStatuses8) { ExtendedDamageAmount = half.ExtendedDamageAmount };
        }
    }
}
