using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>One physical20-byte slot at1D280C4; unwritten bytes survive48D1F0.</summary>
public sealed record NativeBattleActionHeaderSlot([property: JsonRequired] ImmutableArray<byte> Payload, [property: JsonRequired] uint UnknownByteMask, [property: JsonRequired] int? FirstContactIndex, [property: JsonRequired] NativeBattleActionDataReference? Data)
{
    public byte ReadByte(int offset)
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
public sealed record NativeBattleActionPacketsState([property: JsonRequired] byte ActionCount, [property: JsonRequired] byte ContactCount, [property: JsonRequired] ImmutableArray<NativeBattleActionHeaderSlot> Headers, [property: JsonRequired] ImmutableArray<NativeBattleContactPacket?> Contacts, [property: JsonRequired] int? ActiveHeaderIndex)
{
    public int? ActiveContactIndex { get; init; }

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

    public void Validate()
    {
        if (ActionCount > HeaderCapacity || ContactCount > ContactCapacity || Headers.IsDefault || Headers.Length != HeaderCapacity || Contacts.IsDefault || Contacts.Length != ContactCapacity || ActiveHeaderIndex is < 0 or >= HeaderCapacity || ActiveContactIndex is < 0 or > ContactCapacity)
        {
            throw new InvalidDataException("Native action packets require their original counts and32-header/49-contact physical arenas.");
        }
        foreach (NativeBattleActionHeaderSlot header in Headers)
        {
            if (header is null || header.Payload.IsDefault || header.Payload.Length != 20 || (header.UnknownByteMask & ~0xFFFFFu) != 0 || header.FirstContactIndex is < 0 or > ContactCapacity)
            {
                throw new InvalidDataException("Native action header has invalid retained bytes or contact binding.");
            }
            if (header.FirstContactIndex is int first)
            {
                if ((header.UnknownByteMask & 0xFFF) != 0 || BinaryPrimitives.ReadUInt32LittleEndian(header.Payload.AsSpan()[8..]) != ContactBase + 24u * (uint)first)
                {
                    throw new InvalidDataException("Native action header contradicts its prepared physical contact pointer.");
                }
                uint pointerMask = header.UnknownByteMask & 0xF000u;
                if (header.Data is null ? pointerMask != 0 || BinaryPrimitives.ReadUInt32LittleEndian(header.Payload.AsSpan()[12..]) != 0 : pointerMask != 0xF000)
                {
                    throw new InvalidDataException("Native action data pointer lacks its known NULL or logical producer binding.");
                }
            }
            else if (header.Data is not null)
            {
                throw new InvalidDataException("Native prepared action header lacks its physical contact binding.");
            }
        }
        foreach (NativeBattleContactPacket? contact in Contacts)
        {
            contact?.Validate();
        }
        for (int index = 0; index < ActionCount; index++)
        {
            RequirePrepared(index);
            _ = Headers[index].ReadByte(16);
        }
        for (int index = 0; index < ContactCount; index++)
        {
            _ = ReadContact(index);
        }
        if (ActiveHeaderIndex is int active)
        {
            RequirePrepared(active);
            _ = Headers[active].ReadByte(16);
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
        if (preparation.Actor >= 7)
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
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(8), ContactBase + 24u * ContactCount);
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
        packet.Validate();
        if (ContactCount == ContactCapacity)
        {
            throw new InvalidOperationException("Native contact write would overlap the adjacent scene owner.");
        }
        return (this with { Contacts = Contacts.SetItem(ContactCount, packet), ContactCount = checked((byte)(ContactCount + 1)) }, ContactCount);
    }

    public (NativeBattleActionPacketsState State, int HeaderIndex) Publish(byte targetCount)
    {
        Validate();
        RequirePrepared(ActionCount);
        NativeBattleActionHeaderSlot header = Headers[ActionCount];
        header = header with { Payload = header.Payload.SetItem(16, targetCount), UnknownByteMask = header.UnknownByteMask & ~(1u << 16) };
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
        _ = Headers[index].ReadByte(16);
        return this with { ActiveHeaderIndex = index, ActiveContactIndex = Headers[index].FirstContactIndex };
    }

    public NativeBattleContactPacket ReadActiveContact()
    {
        return ReadContact(ActiveContactIndex ?? throw new NotSupportedException("Native current contact pointer1D99A60 requires its retained physical cursor; migrate the standalone snapshot explicitly."));
    }

    public NativeBattleActionPacketsState AdvanceContact50A690()
    {
        int index = ActiveContactIndex ?? throw new NotSupportedException("Native50A690 requires its retained physical contact cursor.");
        if (index >= ContactCapacity)
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
        int count = header.ReadByte(16);
        if (count == 0)
        {
            return [];
        }
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Native506BA0 requires its actual header's physical contact pointer.");
        if (first < 0 || first > ContactCapacity - count)
        {
            throw new InvalidDataException("Native506BA0 contact range exceeds the retained physical contact arena.");
        }
        return Enumerable.Range(first, count).Select(ReadContact).ToImmutableArray();
    }

    public NativeBattleContactPacket ReadContact(int index)
    {
        if (index is < 0 or >= ContactCapacity)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        return Contacts[index] ?? throw new NotSupportedException("Native contact packet has no observed physical writer.");
    }

    private void RequirePrepared(int index)
    {
        if (index is < 0 or >= HeaderCapacity || Headers[index].FirstContactIndex is null)
        {
            throw new InvalidDataException("Native action header has not been prepared in its physical slot.");
        }
    }
}
