using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>One retained original action header without a live actor binding.</summary>
public sealed record NativeBattleRetainedActionHeaderSlot([property: JsonRequired] ImmutableArray<byte> Payload, [property: JsonRequired] uint UnknownByteMask, [property: JsonRequired] int? FirstContactIndex, [property: JsonRequired] NativeBattleActionDataReference? Data)
{
    internal const int Size = 20;
    internal const uint AllUnknown = (1u << Size) - 1;

    private const uint ContactPointerMask = 0xF00;
    private const uint DataPointerMask = 0xF000;

    internal void Validate()
    {
        if (Payload.IsDefault || Payload.Length != Size || (UnknownByteMask & ~AllUnknown) != 0 || FirstContactIndex is < 0 or > NativeBattleRetainedActionPackets.ContactCapacity)
        {
            throw new InvalidDataException("A retained action header has invalid physical bytes, knownness or contact extent.");
        }
        if (FirstContactIndex is int first)
        {
            uint address = NativeBattleRetainedActionPackets.ContactBase + (uint)(NativeBattleRetainedActionPackets.ContactSize * first);
            if ((UnknownByteMask & ContactPointerMask) != 0 || BinaryPrimitives.ReadUInt32LittleEndian(Payload.AsSpan()[8..]) != address)
            {
                throw new InvalidDataException("A retained action header contradicts its original contact pointer.");
            }
        }
        uint pointerKnownness = UnknownByteMask & DataPointerMask;
        if (Data is not null ? pointerKnownness != DataPointerMask :
            FirstContactIndex is not null && (pointerKnownness != 0 || BinaryPrimitives.ReadUInt32LittleEndian(Payload.AsSpan()[12..]) != 0))
        {
            throw new InvalidDataException("A retained action data pointer requires either known NULL or its unknown bytes and producer binding.");
        }
    }

    public byte ReadOriginalByte(int offset)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(offset, Size);
        return ReadKnownByte(offset) ?? throw new NotSupportedException("The retained original header byte has no known writer.");
    }

    internal byte? ReadKnownByte(int offset)
    {
        return (UnknownByteMask & (1u << offset)) == 0 ? Payload[offset] : null;
    }
}

/// <summary>Original action header and contact storage retained independently of any current battle roster.</summary>
public sealed record NativeBattleRetainedActionPackets([property: JsonRequired] byte ActionCount, [property: JsonRequired] int ContactCount, [property: JsonRequired] ImmutableArray<NativeBattleRetainedActionHeaderSlot> Headers, [property: JsonRequired] ImmutableArray<NativeBattleRawContactPacket?> Contacts, [property: JsonRequired] int? ActiveHeaderIndex)
{
    public int? ActiveContactIndex { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public byte UnknownCountMask { get; init; }

    public const int HeaderCapacity = 32;
    public const int ContactCapacity = 49;
    internal const int ContactSize = 24;
    internal const uint ContactBase = 0x1D28344;

    public static NativeBattleRetainedActionPackets Fresh()
    {
        NativeBattleRetainedActionHeaderSlot unknown = new(ImmutableArray.CreateRange(new byte[NativeBattleRetainedActionHeaderSlot.Size]), NativeBattleRetainedActionHeaderSlot.AllUnknown, null, null);
        return new(0, 0, Enumerable.Repeat(unknown, HeaderCapacity).ToImmutableArray(), Enumerable.Repeat<NativeBattleRawContactPacket?>(null, ContactCapacity).ToImmutableArray(), null);
    }

    public static NativeBattleRetainedActionPackets FromColdLoaderStorage(ReadOnlySpan<byte> headers, ReadOnlySpan<byte> contacts)
    {
        const int headerSize = NativeBattleRetainedActionHeaderSlot.Size;
        if (headers.Length != HeaderCapacity * headerSize || contacts.Length != ContactCapacity * ContactSize ||
            headers.ContainsAnyExcept((byte)0) || contacts.ContainsAnyExcept((byte)0))
        {
            throw new InvalidDataException("The original action packet arenas require the observed zero-filled loader storage.");
        }
        var headerSlots = ImmutableArray.CreateBuilder<NativeBattleRetainedActionHeaderSlot>(HeaderCapacity);
        var contactSlots = ImmutableArray.CreateBuilder<NativeBattleRawContactPacket?>(ContactCapacity);
        for (int index = 0; index < HeaderCapacity; index++)
        {
            headerSlots.Add(new(ImmutableArray.CreateRange(headers.Slice(index * headerSize, headerSize).ToArray()), 0, null, null));
        }
        for (int index = 0; index < ContactCapacity; index++)
        {
            contactSlots.Add(NativeBattleContactPacketCodec.Decode(contacts.Slice(index * ContactSize, ContactSize)));
        }
        return new(0, 0, headerSlots.MoveToImmutable(), contactSlots.MoveToImmutable(), null);
    }

    public NativeBattleRetainedActionPackets ResetCounts()
    {
        return this with { ActionCount = 0, ContactCount = 0, UnknownCountMask = 0 };
    }

    internal NativeBattleRetainedActionPackets DetachDiagnosticCopy()
    {
        return this with
        {
            Headers = Headers.Select(header => header with
            {
                Payload = ImmutableArray.Create(header.Payload.AsSpan()),
                Data = header.Data is null ? null : header.Data with { }
            }).ToImmutableArray(),
            Contacts = Contacts.Select(contact => contact is null ? null : contact with
            {
                Primary = contact.Primary with { },
                Secondary = contact.Secondary with { },
                UnknownBits = contact.UnknownBits.IsDefault ? default : ImmutableArray.Create(contact.UnknownBits.AsSpan())
            }).ToImmutableArray()
        };
    }

    public void Validate()
    {
        if (ContactCount is < 0 or > byte.MaxValue || (UnknownCountMask & ~3) != 0 ||
            Headers.IsDefault || Headers.Length != HeaderCapacity || Contacts.IsDefault || Contacts.Length != ContactCapacity ||
            ActiveHeaderIndex is < 0 or >= HeaderCapacity || ActiveContactIndex is < 0 or > ContactCapacity)
        {
            throw new InvalidDataException("Retained action packets require the original storage windows, byte counts and valid cursors.");
        }
        for (int index = 0; index < Headers.Length; index++)
        {
            NativeBattleRetainedActionHeaderSlot header = Headers[index] ?? throw new InvalidDataException("The retained action header arena contains a missing slot.");
            header.Validate();
        }
        for (int index = 0; index < Contacts.Length; index++)
        {
            if (Contacts[index] is { } contact)
            {
                NativeBattleContactPacketCodec.Validate(contact);
            }
            else if ((UnknownCountMask & 2) == 0 && index < ContactCount)
            {
                throw new InvalidDataException("The retained action contacts include a published slot without physical data.");
            }
        }
    }
}
