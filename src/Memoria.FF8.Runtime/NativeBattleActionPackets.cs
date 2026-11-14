using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>One physical20-byte slot at1D280C4; unwritten bytes survive48D1F0.</summary>
public sealed record NativeBattleActionHeaderSlot([property: JsonRequired] ImmutableArray<byte> Payload, [property: JsonRequired] uint UnknownByteMask, [property: JsonRequired] int? FirstContactIndex, [property: JsonRequired] NativeBattleActionDataReference? Data)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? PresentationContactCount { get; init; }

    public byte ReadByte(int offset)
    {
        byte value = ReadRetainedByte(offset);
        if (offset == 16 && PresentationContactCount > byte.MaxValue)
        {
            throw new NotSupportedException("Hosted contact counts above 255 require the semantic ReadContactCount reader.");
        }
        return value;
    }

    public int ReadContactCount()
    {
        byte value = ReadRetainedByte(16);
        return PresentationContactCount ?? value;
    }

    private byte ReadRetainedByte(int offset)
    {
        if (offset is < 0 or >= 20)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }
        if ((UnknownByteMask & (1u << offset)) != 0)
        {
            throw new NotSupportedException($"Native action header byte{offset} has no observed physical writer.");
        }
        return Payload[offset];
    }
}

/// <summary>Canonical C0/C1 counts and adjacent physical header/contact arenas. Resetting counts does not erase storage.</summary>
public sealed record NativeBattleActionPacketsState([property: JsonRequired] byte ActionCount, [property: JsonRequired] int ContactCount, [property: JsonRequired] ImmutableArray<NativeBattleActionHeaderSlot> Headers, [property: JsonRequired] ImmutableArray<NativeBattleContactPacket?> Contacts, [property: JsonRequired] int? ActiveHeaderIndex)
{
    public int? ActiveContactIndex { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleGraphicsRoster? PresentationRoster { get; init; }

    [JsonIgnore]
    public int StorageCapacity => Contacts.Length;

    public const int HeaderCapacity = 32;
    public const int ContactCapacity = 49;
    public const uint ContactBase = 0x1D28344;

    public static NativeBattleActionPacketsState Fresh()
    {
        //48D1F0 clears only the two counts; the retained slot payload has no known cold writer here.
        return new(0, 0, Enumerable.Range(0, HeaderCapacity).Select(_ => new NativeBattleActionHeaderSlot(ImmutableArray.CreateRange(new byte[20]), 0xFFFFF, null, null)).ToImmutableArray(), Enumerable.Repeat<NativeBattleContactPacket?>(null, ContactCapacity).ToImmutableArray(), null);
    }

    public static NativeBattleActionPacketsState FromColdLoaderStorage(ReadOnlySpan<byte> headers, ReadOnlySpan<byte> contacts)
    {
        if (headers.Length != HeaderCapacity * 20 || contacts.Length != ContactCapacity * 24 || headers.ContainsAnyExcept((byte)0) || contacts.ContainsAnyExcept((byte)0))
        {
            throw new InvalidDataException("Native cold-process action storage requires the observed zero-filled loader arenas.");
        }
        NativeBattleActionHeaderSlot[] headerSlots = new NativeBattleActionHeaderSlot[HeaderCapacity];
        NativeBattleContactPacket?[] contactSlots = new NativeBattleContactPacket?[ContactCapacity];
        for (int index = 0; index < headerSlots.Length; index++)
        {
            headerSlots[index] = new(ImmutableArray.CreateRange(headers.Slice(index * 20, 20).ToArray()), 0, null, null);
        }
        for (int index = 0; index < contactSlots.Length; index++)
        {
            contactSlots[index] = NativeBattleContactPacket.Decode(contacts.Slice(index * 24, 24));
        }
        return new(0, 0, headerSlots.ToImmutableArray(), contactSlots.ToImmutableArray(), null);
    }

    public static NativeBattleActionPacketsState FromPresentationStorage(NativeBattleGraphicsRoster roster, int capacity = ContactCapacity)
    {
        ArgumentNullException.ThrowIfNull(roster);
        roster.Validate();
        if (capacity is < ContactCapacity or > NativeBattleContactAddress.MaximumRowCount)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }
        NativeBattleActionHeaderSlot empty = new(ImmutableArray.CreateRange(new byte[20]), 0, null, null);
        return new(0, 0, Enumerable.Repeat(empty, HeaderCapacity).ToImmutableArray(), Enumerable.Repeat<NativeBattleContactPacket?>(null, capacity).ToImmutableArray(), null) { PresentationRoster = roster };
    }

    internal NativeBattleActionPacketsState DetachDiagnosticCopy()
    {
        Validate();
        return this with
        {
            Headers = Headers.Select(header => header with { Payload = ImmutableArray.CreateRange(header.Payload.AsSpan().ToArray()), Data = header.Data is null ? null : header.Data with { } }).ToImmutableArray(),
            Contacts = Contacts.Select(contact => contact is null ? null : contact with { Primary = contact.Primary with { }, Secondary = contact.Secondary with { } }).ToImmutableArray()
        };
    }

    public void Validate()
    {
        PresentationRoster?.Validate();
        if (ActionCount > HeaderCapacity || Headers.IsDefault || Headers.Length != HeaderCapacity || Contacts.IsDefault ||
            (PresentationRoster is null ? StorageCapacity != ContactCapacity : StorageCapacity is < ContactCapacity or > NativeBattleContactAddress.MaximumRowCount) ||
            ContactCount < 0 || ContactCount > StorageCapacity || ActiveHeaderIndex is < 0 or >= HeaderCapacity || ActiveContactIndex < 0 || ActiveContactIndex > StorageCapacity)
        {
            throw new InvalidDataException(PresentationRoster is null ? "Native action packets require their original counts and32-header/49-contact physical arenas." : "Hosted action packets require valid counts in their owned header and contact storage.");
        }
        foreach (NativeBattleActionHeaderSlot header in Headers)
        {
            if (header is null || header.Payload.IsDefault || header.Payload.Length != 20 || (header.UnknownByteMask & ~0xFFFFFu) != 0 || header.FirstContactIndex < 0 || header.FirstContactIndex > StorageCapacity)
            {
                throw new InvalidDataException("Native action header has invalid retained bytes or contact binding.");
            }
            if (header.FirstContactIndex is int first)
            {
                if ((header.UnknownByteMask & 0xFFF) != 0 || BinaryPrimitives.ReadUInt32LittleEndian(header.Payload.AsSpan()[8..]) != NativeBattleContactAddress.EncodeCursor(first, StorageCapacity))
                {
                    throw new InvalidDataException("Native action header contradicts its prepared physical contact pointer.");
                }
                uint pointerMask = header.UnknownByteMask & 0xF000u;
                if (header.Data is null ? pointerMask != 0 || BinaryPrimitives.ReadUInt32LittleEndian(header.Payload.AsSpan()[12..]) != 0 : pointerMask != 0xF000)
                {
                    throw new InvalidDataException("Native action data pointer lacks its known NULL or logical producer binding.");
                }
                if (PresentationRoster is { } roster)
                {
                    RequireBoundActor(roster, header.Payload[0]);
                }
            }
            else if (header.Data is not null)
            {
                throw new InvalidDataException("Native prepared action header lacks its physical contact binding.");
            }
            if (header.PresentationContactCount is int count)
            {
                if (PresentationRoster is null || header.FirstContactIndex is not int start || count < 0 || count > StorageCapacity - start ||
                    (header.UnknownByteMask & (1u << 16)) != 0 || header.Payload[16] != unchecked((byte)count))
                {
                    throw new InvalidDataException("Hosted action header contradicts its retained contact count or packet owner.");
                }
                for (int row = start; row < start + count; row++)
                {
                    if (Contacts[row] is null)
                    {
                        throw new InvalidDataException("Hosted action header references an unwritten contact row.");
                    }
                }
            }
        }
        foreach (NativeBattleContactPacket? contact in Contacts)
        {
            if (contact is not null)
            {
                ValidateContact(contact);
            }
        }
        for (int index = 0; index < ActionCount; index++)
        {
            RequirePrepared(index);
            NativeBattleActionHeaderSlot header = Headers[index];
            _ = header.ReadContactCount();
            if (PresentationRoster is not null && !header.PresentationContactCount.HasValue)
            {
                throw new InvalidDataException("Published hosted action header lacks its semantic contact count.");
            }
        }
        for (int index = 0; index < ContactCount; index++)
        {
            _ = ReadContact(index);
        }
        if (ActiveHeaderIndex is int active)
        {
            RequirePrepared(active);
            _ = Headers[active].ReadContactCount();
        }
    }

    public (NativeBattleActionPacketsState State, int HeaderIndex) Prepare(NativeBattleActionHeaderPreparation preparation)
    {
        Validate();
        ArgumentNullException.ThrowIfNull(preparation);
        if (ActionCount == HeaderCapacity)
        {
            throw new InvalidOperationException("Native action header write would overlap the adjacent contact arena.");
        }
        if (PresentationRoster is { } roster)
        {
            RequireBoundActor(roster, preparation.Actor);
        }
        else if (preparation.Actor >= NativeBattleGraphicsModelAddress.NativeRowCount)
        {
            throw new InvalidDataException("Native action header requires a physical actor index0..6.");
        }
        NativeBattleActionHeaderSlot previous = Headers[ActionCount];
        byte[] payload = previous.Payload.ToArray();
        payload[0] = preparation.Actor;
        payload[1] = preparation.Command;
        payload[2] = preparation.Sequence;
        payload[3] = unchecked((byte)preparation.CameraChoice);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(4), preparation.Action);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(6), preparation.Effect);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(8), NativeBattleContactAddress.EncodeCursor(ContactCount, StorageCapacity));
        uint unknown = previous.UnknownByteMask & ~0xFFFFu;
        if (preparation.Data is null)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(12), 0);
        }
        else
        {
            unknown |= 0xF000;
        }
        NativeBattleActionHeaderSlot header = new(ImmutableArray.CreateRange(payload), unknown, ContactCount, preparation.Data);
        return (this with { Headers = Headers.SetItem(ActionCount, header) }, ActionCount);
    }

    public (NativeBattleActionPacketsState State, int ContactIndex) AppendContact(NativeBattleContactPacket packet)
    {
        Validate();
        ArgumentNullException.ThrowIfNull(packet);
        ValidateContact(packet);
        if (ContactCount == StorageCapacity)
        {
            throw new InvalidOperationException("Native contact write would overlap the adjacent scene owner.");
        }
        return (this with { Contacts = Contacts.SetItem(ContactCount, packet), ContactCount = ContactCount + 1 }, ContactCount);
    }

    public NativeBattleActionPacketsState AppendContacts(IEnumerable<NativeBattleContactPacket> packets)
    {
        Validate();
        ArgumentNullException.ThrowIfNull(packets);
        ImmutableArray<NativeBattleContactPacket?>.Builder contacts = Contacts.ToBuilder();
        int count = ContactCount;
        foreach (NativeBattleContactPacket packet in packets)
        {
            if (count == StorageCapacity)
            {
                throw new InvalidOperationException("Native contact write would exceed its owned storage.");
            }
            ArgumentNullException.ThrowIfNull(packet);
            ValidateContact(packet);
            contacts[count] = packet;
            count++;
        }
        return this with { Contacts = contacts.MoveToImmutable(), ContactCount = count };
    }

    public (NativeBattleActionPacketsState State, int HeaderIndex) Publish(int targetCount)
    {
        Validate();
        RequirePrepared(ActionCount);
        NativeBattleActionHeaderSlot header = Headers[ActionCount];
        if (targetCount < 0 || (PresentationRoster is null ? targetCount > byte.MaxValue : header.FirstContactIndex!.Value > ContactCount - targetCount))
        {
            throw new ArgumentOutOfRangeException(nameof(targetCount));
        }
        header = header with
        {
            Payload = header.Payload.SetItem(16, unchecked((byte)targetCount)),
            UnknownByteMask = header.UnknownByteMask & ~(1u << 16),
            PresentationContactCount = PresentationRoster is null ? null : targetCount
        };
        return (this with { Headers = Headers.SetItem(ActionCount, header), ActionCount = checked((byte)(ActionCount + 1)) }, ActionCount);
    }

    public NativeBattleActionPacketsState WritePlayerRepeatMode(int firstHeaderIndex, byte repeatMode)
    {
        Validate();
        RequirePrepared(firstHeaderIndex);
        NativeBattleActionHeaderSlot header = Headers[firstHeaderIndex];
        header = header with { Payload = header.Payload.SetItem(17, unchecked((byte)(repeatMode - 1))), UnknownByteMask = header.UnknownByteMask & ~(1u << 17) };
        return this with { Headers = Headers.SetItem(firstHeaderIndex, header) };
    }

    public NativeBattleActionPacketsState ActivateHeader(int index)
    {
        Validate();
        RequirePrepared(index);
        _ = Headers[index].ReadContactCount();
        return this with { ActiveHeaderIndex = index, ActiveContactIndex = Headers[index].FirstContactIndex };
    }

    public NativeBattleContactPacket ReadActiveContact()
    {
        return ReadContact(ActiveContactIndex ?? throw new NotSupportedException("Native current contact pointer1D99A60 requires its retained physical cursor; migrate the standalone snapshot explicitly."));
    }

    public NativeBattleActionPacketsState AdvanceContact50A690()
    {
        int index = ActiveContactIndex ?? throw new NotSupportedException("Native50A690 requires its retained physical contact cursor.");
        if (index >= StorageCapacity)
        {
            throw new NotSupportedException("Native50A690 cursor leaves its retained physical contact arena.");
        }
        return this with { ActiveContactIndex = index + 1 };
    }

    public NativeBattleActionPacketsState ResetCounts()
    {
        Validate();
        return this with { ActionCount = 0, ContactCount = 0 };
    }

    public ImmutableArray<NativeBattleContactPacket> ReadHeaderContacts506BA0(int headerIndex)
    {
        if (headerIndex is < 0 or >= HeaderCapacity)
        {
            throw new ArgumentOutOfRangeException(nameof(headerIndex));
        }
        NativeBattleActionHeaderSlot header = Headers[headerIndex];
        int count = header.ReadContactCount();
        if (count == 0)
        {
            return [];
        }
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Native506BA0 requires its actual header's physical contact pointer.");
        if (first < 0 || count < 0 || first > StorageCapacity - count)
        {
            throw new InvalidDataException("Native506BA0 contact range exceeds the retained physical contact arena.");
        }
        return Enumerable.Range(first, count).Select(ReadContact).ToImmutableArray();
    }

    public NativeBattleContactPacket ReadContact(int index)
    {
        if (index < 0 || index >= StorageCapacity)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        NativeBattleContactPacket packet = Contacts[index] ?? throw new NotSupportedException("Native contact packet has no observed physical writer.");
        return packet with { PhysicalContactIndex = index };
    }

    public byte[] ReadContactBytes(int index) => ReadContact(index).Encode(PresentationRoster?.ActorCount ?? NativeBattleGraphicsModelAddress.NativeRowCount);

    private void ValidateContact(NativeBattleContactPacket packet)
    {
        packet.Validate(PresentationRoster?.ActorCount ?? NativeBattleGraphicsModelAddress.NativeRowCount);
        if (PresentationRoster is { } roster)
        {
            RequireBoundActor(roster, packet.Primary.ActorIndex);
            if (packet.Secondary.ActorIndex != byte.MaxValue)
            {
                RequireBoundActor(roster, packet.Secondary.ActorIndex);
            }
        }
    }

    private static void RequireBoundActor(NativeBattleGraphicsRoster roster, byte row)
    {
        if (row >= roster.ActorCount || roster.Rows[row] is null)
        {
            throw new InvalidDataException($"Hosted action packets require a bound actor at row {row}.");
        }
    }

    private void RequirePrepared(int index)
    {
        if (index is < 0 or >= HeaderCapacity || Headers[index].FirstContactIndex is null)
        {
            throw new InvalidDataException("Native action header has not been prepared in its physical slot.");
        }
    }
}
