using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleActionHeaderSlot(BattleModelOwner Source, byte Command, byte Sequence, sbyte CameraChoice, ushort Action, ushort Effect, BattleContactCursor FirstContact, NativeBattleActionDataReference? Data, int? ContactCount, byte? RepeatModeMinusOne)
{
    public int ReadContactCount()
    {
        return ContactCount ?? throw new NotSupportedException("The action header's contact count has no observed writer.");
    }

    public byte ReadRepeatModeMinusOne()
    {
        return RepeatModeMinusOne ?? throw new NotSupportedException("The action header's repeat mode has no observed writer.");
    }
}

public sealed class NativeBattleActionPacketsState
{
    private readonly ImmutableArray<NativeBattleActionHeaderSlot?> headers;
    private readonly ImmutableArray<NativeBattleContactPacket?> contacts;

    private NativeBattleActionPacketsState(BattleModelRoster roster, NativeBattleRetainedActionPackets retainedStorage, ImmutableArray<NativeBattleActionHeaderSlot?> headers, ImmutableArray<NativeBattleContactPacket?> contacts, int actionCount = 0, int contactCount = 0, BattleActionHeaderReference? activeHeader = null, BattleContactCursor? activeContact = null)
    {
        Roster = roster;
        RetainedStorage = retainedStorage;
        this.headers = headers;
        this.contacts = contacts;
        ActionCount = actionCount;
        ContactCount = contactCount;
        ActiveHeader = activeHeader;
        ActiveContact = activeContact;
    }

    public BattleModelRoster Roster { get; }
    internal NativeBattleRetainedActionPackets RetainedStorage { get; }
    public int ActionCount { get; }
    public int ContactCount { get; }
    public BattleActionHeaderReference? ActiveHeader { get; }
    public BattleContactCursor? ActiveContact { get; }

    public static NativeBattleActionPacketsState Create(BattleModelRoster roster, NativeBattleRetainedActionPackets retained)
    {
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(retained);
        retained.Validate();
        return new(roster, retained, ImmutableArray.CreateRange(new NativeBattleActionHeaderSlot?[NativeBattleRetainedActionPackets.HeaderCapacity]), ImmutableArray.CreateRange(new NativeBattleContactPacket?[NativeBattleRetainedActionPackets.ContactCapacity]));
    }

    /// <summary>Replaces live membership after callers retire outstanding work for removed owners.</summary>
    public NativeBattleActionPacketsState WithRoster(BattleModelRoster roster)
    {
        ArgumentNullException.ThrowIfNull(roster);
        return ReferenceEquals(Roster, roster) ? this : new(roster, RetainedStorage, headers, contacts, ActionCount, ContactCount, ActiveHeader, ActiveContact);
    }

    public (NativeBattleActionPacketsState State, BattleActionHeaderReference Header) Prepare(NativeBattleActionHeaderPreparation preparation)
    {
        ArgumentNullException.ThrowIfNull(preparation);
        RequireOwner(preparation.Source);
        int index = ActionCount;
        ImmutableArray<NativeBattleActionHeaderSlot?> grown = EnsureCapacity(headers, checked(index + 1));
        NativeBattleActionHeaderSlot? previous = grown[index];
        NativeBattleRetainedActionHeaderSlot? retained = index < RetainedStorage.Headers.Length ? RetainedStorage.Headers[index] : null;
        int? count = previous is null ? retained?.ReadKnownByte(16) : previous.ContactCount;
        byte? repeat = previous is null ? retained?.ReadKnownByte(17) : previous.RepeatModeMinusOne;
        NativeBattleActionHeaderSlot slot = new(preparation.Source, preparation.Command, preparation.Sequence, preparation.CameraChoice, preparation.Action, preparation.Effect, new(ContactCount), preparation.Data, count, repeat);
        return (new(Roster, RetainedStorage, grown.SetItem(index, slot), contacts, ActionCount, ContactCount, ActiveHeader, ActiveContact), new(index));
    }

    public (NativeBattleActionPacketsState State, BattleContactOccurrence Occurrence) AppendContact(NativeBattleContactPacket packet)
    {
        return (AppendContacts([packet]), new(ContactCount));
    }

    public NativeBattleActionPacketsState AppendContacts(IEnumerable<NativeBattleContactPacket> packets)
    {
        ArgumentNullException.ThrowIfNull(packets);
        var builder = contacts.ToBuilder();
        int count = ContactCount;
        foreach (NativeBattleContactPacket packet in packets)
        {
            ValidateContact(packet);
            if (count == builder.Count)
            {
                builder.Add(packet with { Occurrence = null });
            }
            else
            {
                builder[count] = packet with { Occurrence = null };
            }
            count = checked(count + 1);
        }
        return count == ContactCount ? this : new(Roster, RetainedStorage, headers, builder.ToImmutable(), ActionCount, count, ActiveHeader, ActiveContact);
    }

    public (NativeBattleActionPacketsState State, BattleActionHeaderReference Header) Publish(int targetCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(targetCount);
        BattleActionHeaderReference reference = new(ActionCount);
        NativeBattleActionHeaderSlot header = ReadHeader(reference) with { ContactCount = targetCount };
        return (new(Roster, RetainedStorage, headers.SetItem(reference.Ordinal, header), contacts, checked(ActionCount + 1), ContactCount, ActiveHeader, ActiveContact), reference);
    }

    public NativeBattleActionPacketsState WritePlayerRepeatMode(BattleActionHeaderReference reference, byte repeatMode)
    {
        NativeBattleActionHeaderSlot header = ReadHeader(reference) with { RepeatModeMinusOne = unchecked((byte)(repeatMode - 1)) };
        return new(Roster, RetainedStorage, headers.SetItem(reference.Ordinal, header), contacts, ActionCount, ContactCount, ActiveHeader, ActiveContact);
    }

    public NativeBattleActionHeaderSlot ReadHeader(BattleActionHeaderReference reference)
    {
        if ((uint)reference.Ordinal >= (uint)headers.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(reference));
        }
        NativeBattleActionHeaderSlot header = headers[reference.Ordinal] ?? throw new InvalidDataException("The live action header has not been prepared.");
        RequireOwner(header.Source);
        return header;
    }

    public NativeBattleContactPacket ReadContact(BattleContactOccurrence occurrence)
    {
        if ((uint)occurrence.Ordinal >= (uint)contacts.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(occurrence));
        }
        NativeBattleContactPacket packet = contacts[occurrence.Ordinal] ?? throw new NotSupportedException("The live contact has no observed writer.");
        ValidateContact(packet);
        return packet with { Occurrence = occurrence };
    }

    public BattleContactOccurrence ResolveContact(BattleActionHeaderReference header, int relativeOrdinal)
    {
        NativeBattleActionHeaderSlot slot = ReadHeader(header);
        int count = slot.ReadContactCount();
        if ((uint)relativeOrdinal >= (uint)count)
        {
            throw new ArgumentOutOfRangeException(nameof(relativeOrdinal));
        }
        if (slot.FirstContact.Ordinal > contacts.Length - count)
        {
            throw new InvalidDataException("The action's contact range exceeds the live arena.");
        }
        return new(checked(slot.FirstContact.Ordinal + relativeOrdinal));
    }

    public ImmutableArray<NativeBattleContactPacket> ReadHeaderContacts(BattleActionHeaderReference reference)
    {
        NativeBattleActionHeaderSlot header = ReadHeader(reference);
        int count = header.ReadContactCount();
        if (header.FirstContact.Ordinal > contacts.Length - count)
        {
            throw new InvalidDataException("The action's contact range exceeds the live arena.");
        }
        var result = ImmutableArray.CreateBuilder<NativeBattleContactPacket>(count);
        for (int relative = 0; relative < count; relative++)
        {
            result.Add(ReadContact(new(checked(header.FirstContact.Ordinal + relative))));
        }
        return result.MoveToImmutable();
    }

    public NativeBattleActionPacketsState ActivateHeader(BattleActionHeaderReference reference)
    {
        NativeBattleActionHeaderSlot header = ReadHeader(reference);
        _ = header.ReadContactCount();
        return new(Roster, RetainedStorage, headers, contacts, ActionCount, ContactCount, reference, header.FirstContact);
    }

    public NativeBattleActionPacketsState AdvanceContact()
    {
        BattleContactCursor cursor = ActiveContact ?? throw new NotSupportedException("Native50A690 requires its current contact cursor.");
        if (cursor.Ordinal >= contacts.Length)
        {
            throw new NotSupportedException("Native50A690 cannot advance past the live arena's one-past cursor.");
        }
        return new(Roster, RetainedStorage, headers, contacts, ActionCount, ContactCount, ActiveHeader, cursor.Advance(1));
    }

    public NativeBattleContactPacket ReadActiveContact()
    {
        BattleContactCursor cursor = ActiveContact ?? throw new NotSupportedException("The action has no active contact cursor.");
        return ReadContact(new(cursor.Ordinal));
    }

    public NativeBattleActionPacketsState ResetCounts()
    {
        return new(Roster, RetainedStorage, headers, contacts, 0, 0, ActiveHeader, ActiveContact);
    }

    public NativeBattleRetainedActionPackets ExportRetained(NativeBattleModelBoundary bindings, BattleActorModels models)
    {
        return NativeBattleActionPacketBoundary.Export(this, bindings, models);
    }

    internal NativeBattleRetainedActionPackets ExportUnmodifiedRetained()
    {
        if (ActionCount != 0 || ContactCount != 0 || ActiveHeader is not null || ActiveContact is not null ||
            headers.Any(header => header is not null) || contacts.Any(contact => contact is not null))
        {
            throw new NotSupportedException("Projecting written live action packets requires original model bindings.");
        }
        return RetainedStorage.ResetCounts();
    }

    internal NativeBattleActionPacketsState DetachDiagnosticCopy() => this;

    public void Validate()
    {
        if (headers.IsDefault || contacts.IsDefault || ActionCount < 0 || ActionCount > headers.Length || ContactCount < 0 || ContactCount > contacts.Length ||
            ActiveHeader is { } active && ((uint)active.Ordinal >= (uint)headers.Length || headers[active.Ordinal]?.ContactCount is null) ||
            ActiveContact is { } cursor && cursor.Ordinal > contacts.Length)
        {
            throw new InvalidDataException("The action arena lacks its live storage or has invalid counts or cursors.");
        }
        // Rebinding may retire an owner recorded by an earlier invocation. Membership is checked when that fact is reached.
        if (headers.Take(ActionCount).Any(header => header?.ContactCount is null) || contacts.Take(ContactCount).Any(contact => contact is null) ||
            headers.Any(header => header is not null && (header.Source is null || header.ContactCount < 0 || header.FirstContact.Ordinal > contacts.Length)))
        {
            throw new InvalidDataException("The action arena contains invalid live header or contact storage.");
        }
    }

    internal NativeBattleActionHeaderSlot? FindRetainedHeader(BattleActionHeaderReference reference) => reference.Ordinal < headers.Length ? headers[reference.Ordinal] : null;

    internal NativeBattleContactPacket? FindRetainedContact(BattleContactOccurrence occurrence) => occurrence.Ordinal < contacts.Length ? contacts[occurrence.Ordinal] : null;

    private void RequireOwner(BattleModelOwner owner)
    {
        if (owner is null || !Roster.Contains(owner))
        {
            throw new InvalidDataException("The action packet refers to a model outside its current roster.");
        }
    }

    private void ValidateContact(NativeBattleContactPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        packet.Validate();
        RequireHalfOwner(packet.Primary);
        if (packet.Secondary is { } secondary)
        {
            RequireHalfOwner(secondary);
        }
    }

    private void RequireHalfOwner(NativeBattleContactHalf half)
    {
        RequireOwner(half.Recipient);
        if (half.CoverSource is { } cover)
        {
            RequireOwner(cover);
        }
    }

    private static ImmutableArray<T> EnsureCapacity<T>(ImmutableArray<T> storage, int required)
    {
        if (required <= storage.Length)
        {
            return storage;
        }
        int capacity = checked((int)Math.Max(required, Math.Min((long)Array.MaxLength, (long)storage.Length * 2)));
        return storage.AddRange(new T[capacity - storage.Length]);
    }
}
