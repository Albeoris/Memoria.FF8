using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleActionPacketBoundary
{
    internal static short ReadActiveCameraFlag50A6E0(NativeBattleActionPacketsState packets)
    {
        BattleContactCursor cursor = packets.ActiveContact ?? throw new NotSupportedException("Native50A6E0 requires its active contact cursor.");
        BattleContactOccurrence occurrence = new(cursor.Ordinal);
        if (packets.FindRetainedContact(occurrence) is { } live)
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

    internal static NativeBattleRetainedActionPackets Export(NativeBattleActionPacketsState arena, NativeBattleModelBoundary bindings, BattleActorModels models)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(models);
        arena.Validate();
        NativeBattleRetainedActionPackets retained = arena.RetainedStorage;
        var headers = retained.Headers.ToBuilder();
        var contacts = retained.Contacts.ToBuilder();
        for (int index = 0; index < headers.Count; index++)
        {
            if (arena.FindRetainedHeader(new(index)) is { } header)
            {
                headers[index] = ProjectHeader(header, headers[index], bindings, models);
            }
        }
        for (int index = 0; index < contacts.Count; index++)
        {
            if (arena.FindRetainedContact(new(index)) is { } contact)
            {
                contacts[index] = ProjectContact(contact, contacts[index], bindings, models);
            }
        }
        bool actionCountFits = arena.ActionCount <= NativeBattleRetainedActionPackets.HeaderCapacity;
        bool contactCountFits = arena.ContactCount <= NativeBattleRetainedActionPackets.ContactCapacity;
        NativeBattleRetainedActionPackets result = new(
            actionCountFits ? checked((byte)arena.ActionCount) : retained.ActionCount,
            contactCountFits ? arena.ContactCount : retained.ContactCount,
            headers.MoveToImmutable(), contacts.MoveToImmutable(),
            arena.ActiveHeader is { } active && active.Ordinal < NativeBattleRetainedActionPackets.HeaderCapacity ? active.Ordinal : null)
        {
            ActiveContactIndex = arena.ActiveContact is { } cursor && cursor.Ordinal <= NativeBattleRetainedActionPackets.ContactCapacity ? cursor.Ordinal : null,
            UnknownCountMask = (byte)((actionCountFits ? 0 : 1) | (contactCountFits ? 0 : 2))
        };
        result.Validate();
        return result;
    }

    private static NativeBattleRetainedActionHeaderSlot ProjectHeader(NativeBattleActionHeaderSlot header, NativeBattleRetainedActionHeaderSlot retained, NativeBattleModelBoundary bindings, BattleActorModels models)
    {
        byte[] bytes = retained.Payload.ToArray();
        uint unknown = retained.UnknownByteMask & ~0xFEu;
        if (bindings.TryEncodeExactActor(header.Source, models, out byte source))
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

        int? first = null;
        if (header.FirstContact.Ordinal <= NativeBattleRetainedActionPackets.ContactCapacity)
        {
            first = header.FirstContact.Ordinal;
            uint pointer = NativeBattleRetainedActionPackets.ContactBase + (uint)(first.Value * NativeBattleRetainedActionPackets.ContactSize);
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

        WriteOptional(16, header.ContactCount is >= 0 and <= byte.MaxValue ? (byte)header.ContactCount.Value : null);
        WriteOptional(17, header.RepeatModeMinusOne);
        return new(ImmutableArray.CreateRange(bytes), unknown, first, header.Data);

        void WriteOptional(int offset, byte? value)
        {
            uint bit = 1u << offset;
            if (value is byte known)
            {
                bytes[offset] = known;
                unknown &= ~bit;
            }
            else
            {
                unknown |= bit;
            }
        }
    }

    private static NativeBattleRawContactPacket ProjectContact(NativeBattleContactPacket contact, NativeBattleRawContactPacket? retained, NativeBattleModelBoundary bindings, BattleActorModels models)
    {
        byte[]? unknown = null;
        NativeBattleRawContactHalf primary = ProjectHalf(contact.Primary, retained?.Primary, 0);
        NativeBattleRawContactHalf secondary = contact.Secondary is { } present ? ProjectHalf(present, retained?.Secondary, 12) : new(byte.MaxValue, 0, 0, 0, 0, 0, 0);
        return new(primary, secondary) { UnknownBits = unknown is null ? default : ImmutableArray.CreateRange(unknown) };

        NativeBattleRawContactHalf ProjectHalf(NativeBattleContactHalf half, NativeBattleRawContactHalf? previous, int offset)
        {
            if (!bindings.TryEncodeExactActor(half.Recipient, models, out byte recipient))
            {
                recipient = previous?.ActorIndex ?? 0;
                (unknown ??= new byte[NativeBattleRetainedActionPackets.ContactSize])[offset] = byte.MaxValue;
            }
            byte flags = half.HitFlags;
            if (half.CoverSource is { } cover)
            {
                if (bindings.TryEncodeExactActor(cover, models, out byte coverSlot) && coverSlot < 4)
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
