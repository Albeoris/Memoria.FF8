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
    private readonly NativeBattleModelAliases aliases;

    private NativeBattleActionPacketsState(BattleModelRoster roster, NativeBattleModelAliases aliases, NativeBattleRetainedActionPackets retainedStorage, ImmutableArray<NativeBattleActionHeaderSlot?> headers, ImmutableArray<NativeBattleContactPacket?> contacts, int actionCount = 0, int contactCount = 0, BattleActionHeaderReference? activeHeader = null, BattleContactCursor? activeContact = null)
    {
        Roster = roster;
        this.aliases = aliases;
        RetainedStorage = retainedStorage;
        this.headers = headers;
        this.contacts = contacts;
        ActionCount = actionCount;
        ContactCount = contactCount;
        ActiveHeader = activeHeader;
        ActiveContact = activeContact;
    }

    public BattleModelRoster Roster { get; }
    internal NativeBattleModelAliases ModelAliases => aliases;
    internal NativeBattleRetainedActionPackets RetainedStorage { get; }
    public int ActionCount { get; }
    public int ContactCount { get; }
    public BattleActionHeaderReference? ActiveHeader { get; }
    public BattleContactCursor? ActiveContact { get; }

    public static NativeBattleActionPacketsState Create(BattleModelRoster roster, NativeBattleRetainedActionPackets retained, NativeBattleModelAliases aliases)
    {
        ValidateAliases(roster, aliases);
        ArgumentNullException.ThrowIfNull(retained);
        retained.Validate();
        return new(roster, aliases, retained.ResetCounts(), ImmutableArray.CreateRange(new NativeBattleActionHeaderSlot?[NativeBattleRetainedActionPackets.HeaderCapacity]), ImmutableArray.CreateRange(new NativeBattleContactPacket?[NativeBattleRetainedActionPackets.ContactCapacity]));
    }

    /// <summary>Replaces membership and aliases for new writes after callers retire outstanding work for removed owners.</summary>
    public NativeBattleActionPacketsState WithRoster(BattleModelRoster roster, NativeBattleModelAliases aliases)
    {
        ValidateAliases(roster, aliases);
        return ReferenceEquals(Roster, roster) && ReferenceEquals(this.aliases, aliases) ? this : new(roster, aliases, RetainedStorage, headers, contacts, ActionCount, ContactCount, ActiveHeader, ActiveContact);
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
        BattleActionHeaderReference reference = new(index);
        NativeBattleRetainedActionPackets raw = NativeBattleActionPacketBoundary.PrepareHeader(RetainedStorage, reference, slot, aliases);
        return (new(Roster, aliases, raw, grown.SetItem(index, slot), contacts, ActionCount, ContactCount, ActiveHeader, ActiveContact), reference);
    }

    public (NativeBattleActionPacketsState State, BattleContactOccurrence Occurrence) AppendContact(NativeBattleContactPacket packet)
    {
        return (AppendContacts([packet]), new(ContactCount));
    }

    public NativeBattleActionPacketsState AppendContacts(IEnumerable<NativeBattleContactPacket> packets)
    {
        ArgumentNullException.ThrowIfNull(packets);
        var builder = contacts.ToBuilder();
        ImmutableArray<NativeBattleRawContactPacket?>.Builder? rawContacts = null;
        int? lastEncodableCount = null;
        int count = ContactCount;
        foreach (NativeBattleContactPacket packet in packets)
        {
            ValidateContact(packet);
            if (count < NativeBattleRetainedActionPackets.ContactCapacity)
            {
                rawContacts ??= RetainedStorage.Contacts.ToBuilder();
                rawContacts[count] = NativeBattleActionPacketBoundary.WriteContact(packet, rawContacts[count], aliases);
            }
            if (count == builder.Count)
            {
                builder.Add(packet with { Occurrence = null });
            }
            else
            {
                builder[count] = packet with { Occurrence = null };
            }
            count = checked(count + 1);
            if (count <= byte.MaxValue)
            {
                lastEncodableCount = count;
            }
        }
        if (count == ContactCount)
        {
            return this;
        }
        NativeBattleRetainedActionPackets raw = NativeBattleActionPacketBoundary.CompleteContactWrites(RetainedStorage, rawContacts?.ToImmutable() ?? RetainedStorage.Contacts, count, lastEncodableCount);
        return new(Roster, aliases, raw, headers, builder.ToImmutable(), ActionCount, count, ActiveHeader, ActiveContact);
    }

    public (NativeBattleActionPacketsState State, BattleActionHeaderReference Header) Publish(int targetCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(targetCount);
        BattleActionHeaderReference reference = new(ActionCount);
        NativeBattleActionHeaderSlot header = ReadHeader(reference) with { ContactCount = targetCount };
        int count = checked(ActionCount + 1);
        NativeBattleRetainedActionPackets raw = NativeBattleActionPacketBoundary.PublishHeader(RetainedStorage, reference, targetCount, count);
        return (new(Roster, aliases, raw, headers.SetItem(reference.Ordinal, header), contacts, count, ContactCount, ActiveHeader, ActiveContact), reference);
    }

    public NativeBattleActionPacketsState WritePlayerRepeatMode(BattleActionHeaderReference reference, byte repeatMode)
    {
        byte repeatModeMinusOne = unchecked((byte)(repeatMode - 1));
        NativeBattleActionHeaderSlot header = ReadHeader(reference) with { RepeatModeMinusOne = repeatModeMinusOne };
        NativeBattleRetainedActionPackets raw = NativeBattleActionPacketBoundary.WriteRepeatMode(RetainedStorage, reference, repeatModeMinusOne);
        return new(Roster, aliases, raw, headers.SetItem(reference.Ordinal, header), contacts, ActionCount, ContactCount, ActiveHeader, ActiveContact);
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

    /// <summary>Reads the written live cell at a cursor without selecting an occurrence relative to a header.</summary>
    public NativeBattleContactPacket ReadContact(BattleContactCursor cursor)
    {
        return ReadContact(new BattleContactOccurrence(cursor.Ordinal));
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
            result.Add(ReadContact(new BattleContactOccurrence(checked(header.FirstContact.Ordinal + relative))));
        }
        return result.MoveToImmutable();
    }

    public NativeBattleActionPacketsState ReplaceHeaderContacts(BattleActionHeaderReference reference, ImmutableArray<NativeBattleContactPacket> replacements)
    {
        NativeBattleActionHeaderSlot header = ReadHeader(reference);
        if (replacements.IsDefault || replacements.Length != header.ReadContactCount())
        {
            throw new InvalidDataException("Contact replacement requires the existing header's exact contact extent.");
        }
        ImmutableArray<NativeBattleContactPacket> current = ReadHeaderContacts(reference);
        if (current.IsEmpty)
        {
            return this;
        }
        var updated = contacts.ToBuilder();
        ImmutableArray<NativeBattleRawContactPacket?>.Builder? rawContacts = null;
        for (int relative = 0; relative < current.Length; relative++)
        {
            NativeBattleContactPacket replacement = replacements[relative];
            BattleContactOccurrence occurrence = current[relative].Occurrence!.Value;
            ValidateContact(replacement);
            if (replacement.Primary.Recipient != current[relative].Primary.Recipient ||
                replacement.Secondary is { } secondary && secondary.Recipient != header.Source ||
                replacement.Occurrence is { } supplied && supplied != occurrence)
            {
                throw new InvalidDataException("A replacement must retain the primary recipient and occurrence, and any secondary must name the header source.");
            }
            updated[occurrence.Ordinal] = replacement with { Occurrence = null };
            if (occurrence.Ordinal < NativeBattleRetainedActionPackets.ContactCapacity)
            {
                rawContacts ??= RetainedStorage.Contacts.ToBuilder();
                rawContacts[occurrence.Ordinal] = NativeBattleActionPacketBoundary.WriteContact(replacement, rawContacts[occurrence.Ordinal], aliases);
            }
        }
        NativeBattleRetainedActionPackets retained = rawContacts is null ? RetainedStorage : RetainedStorage with { Contacts = rawContacts.ToImmutable() };
        return new(Roster, aliases, retained, headers, updated.ToImmutable(), ActionCount, ContactCount, ActiveHeader, ActiveContact);
    }

    internal NativeBattleActionPacketsState WriteSequence(BattleActionHeaderReference reference, byte sequence)
    {
        NativeBattleActionHeaderSlot header = ReadHeader(reference) with { Sequence = sequence };
        NativeBattleRetainedActionPackets retained = NativeBattleActionPacketBoundary.WriteSequence(RetainedStorage, reference, sequence);
        return new(Roster, aliases, retained, headers.SetItem(reference.Ordinal, header), contacts, ActionCount, ContactCount, ActiveHeader, ActiveContact);
    }

    public NativeBattleActionPacketsState CompleteHeader(BattleActionHeaderReference reference)
    {
        NativeBattleActionHeaderSlot header = ReadHeader(reference) with { Command = byte.MaxValue };
        NativeBattleRetainedActionPackets retained = NativeBattleActionPacketBoundary.CompleteHeader(RetainedStorage, reference);
        return new(Roster, aliases, retained, headers.SetItem(reference.Ordinal, header), contacts, ActionCount, ContactCount, ActiveHeader, ActiveContact);
    }

    public NativeBattleActionPacketsState ActivateHeader(BattleActionHeaderReference reference)
    {
        NativeBattleActionHeaderSlot header = ReadHeader(reference);
        _ = header.ReadContactCount();
        NativeBattleRetainedActionPackets raw = NativeBattleActionPacketBoundary.ActivateHeader(RetainedStorage, reference, header.FirstContact);
        return new(Roster, aliases, raw, headers, contacts, ActionCount, ContactCount, reference, header.FirstContact);
    }

    public NativeBattleActionPacketsState AdvanceContact()
    {
        BattleContactCursor cursor = ActiveContact ?? throw new NotSupportedException("Native50A690 requires its current contact cursor.");
        if (cursor.Ordinal >= contacts.Length)
        {
            throw new NotSupportedException("Native50A690 cannot advance past the live arena's one-past cursor.");
        }
        BattleContactCursor next = cursor.Advance(1);
        NativeBattleRetainedActionPackets raw = NativeBattleActionPacketBoundary.AdvanceContact(RetainedStorage, next);
        return new(Roster, aliases, raw, headers, contacts, ActionCount, ContactCount, ActiveHeader, next);
    }

    public NativeBattleContactPacket ReadActiveContact()
    {
        BattleContactCursor cursor = ActiveContact ?? throw new NotSupportedException("The action has no active contact cursor.");
        return ReadContact(cursor);
    }

    public NativeBattleActionPacketsState ResetCounts()
    {
        return new(Roster, aliases, RetainedStorage.ResetCounts(), headers, contacts, 0, 0, ActiveHeader, ActiveContact);
    }

    public NativeBattleRetainedActionPackets ExportRetained()
    {
        RetainedStorage.Validate();
        return RetainedStorage;
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

    internal NativeBattleContactPacket? FindStoredContact(BattleContactOccurrence occurrence) => occurrence.Ordinal < contacts.Length ? contacts[occurrence.Ordinal] : null;

    private static void ValidateAliases(BattleModelRoster roster, NativeBattleModelAliases aliases)
    {
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(aliases);
        if (!ReferenceEquals(aliases.Roster, roster) && (aliases.Roster.Count != roster.Count || !roster.Owners.All(aliases.Roster.Contains)))
        {
            throw new ArgumentException("Original model aliases must describe the action packet roster.", nameof(aliases));
        }
    }

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
