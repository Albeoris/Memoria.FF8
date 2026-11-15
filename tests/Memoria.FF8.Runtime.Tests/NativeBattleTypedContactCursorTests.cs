namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTypedContactCursorTests
{
    private static readonly BattlePartyActorModel Source = new(new("source"));
    private static readonly BattleEnemyActorModel Target = new(new("target"));
    private static readonly NativeBattleActionHeaderPreparation Preparation = new(Source, 1, 7, -1, 101, 0, null);
    private static readonly NativeBattleContactPacket Contact = new(new(Target, 4, 0, 0, 0, 37, 0), null);

    [Fact]
    public void CursorCanReadAWrittenCellAtAnEmptyHeadersStart()
    {
        NativeBattleActionPacketsState packets = Create();
        (packets, BattleActionHeaderReference empty) = packets.Prepare(Preparation);
        (packets, _) = packets.Publish(0);
        BattleContactCursor cursor = packets.ReadHeader(empty).FirstContact;
        Assert.Throws<ArgumentOutOfRangeException>(() => packets.ResolveContact(empty, 0));
        Assert.Throws<NotSupportedException>(() => packets.ReadContact(cursor));

        (packets, BattleActionHeaderReference writer) = packets.Prepare(Preparation);
        (packets, BattleContactOccurrence occurrence) = packets.AppendContact(Contact);
        (packets, _) = packets.Publish(1);

        Assert.Equal(cursor, packets.ReadHeader(writer).FirstContact);
        Assert.Throws<ArgumentOutOfRangeException>(() => packets.ResolveContact(empty, 0));
        NativeBattleContactPacket reached = packets.ReadContact(packets.ReadHeader(empty).FirstContact);
        Assert.Equal(Contact, reached);
        Assert.Equal(occurrence, reached.Occurrence);
    }

    [Fact]
    public void CursorDoesNotReadUnwrittenOrOnePastCells()
    {
        NativeBattleActionPacketsState packets = Create();
        Assert.Throws<NotSupportedException>(() => packets.ReadContact(new BattleContactCursor(0)));
        Assert.Throws<NotSupportedException>(() => packets.ReadContact(new BattleContactCursor(48)));
        Assert.Throws<ArgumentOutOfRangeException>(() => packets.ReadContact(new BattleContactCursor(49)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BattleContactCursor(0).Advance(-1));
        Assert.Throws<OverflowException>(() => new BattleContactCursor(int.MaxValue).Advance(1));
    }

    [Fact]
    public void ResetKeepsWrittenCellIdentityAndReadRejectsItsRetiredOwner()
    {
        NativeBattleActionPacketsState packets = Create();
        (packets, BattleActionHeaderReference header) = packets.Prepare(Preparation);
        (packets, BattleContactOccurrence occurrence) = packets.AppendContact(Contact);
        (packets, _) = packets.Publish(1);
        BattleContactCursor cursor = packets.ReadHeader(header).FirstContact;
        NativeBattleActionPacketsState reset = packets.ResetCounts();

        Assert.Equal(0, reset.ActionCount);
        Assert.Equal(0, reset.ContactCount);
        Assert.Equal(occurrence, reset.ReadContact(cursor).Occurrence);
        Assert.Equal(Contact, reset.ReadContact(cursor));

        BattleModelRoster roster = new([Source]);
        NativeBattleModelAliases aliases = new(roster, [(NativeBattleModelSlot.Decode(0), Source)]);
        NativeBattleActionPacketsState retired = reset.WithRoster(roster, aliases);
        Assert.Throws<InvalidDataException>(() => retired.ReadContact(cursor));
        Assert.Equal(occurrence, reset.ReadContact(cursor).Occurrence);
    }

    [Fact]
    public void CursorAndHeaderPreserveOccurrenceAndCountPastOriginalWidths()
    {
        NativeBattleActionPacketsState packets = Create();
        (packets, BattleActionHeaderReference header) = packets.Prepare(Preparation);
        NativeBattleContactPacket final = Contact with { Primary = Contact.Primary with { Amount6 = 123 } };
        packets = packets.AppendContacts(Enumerable.Repeat(Contact, 299).Append(final));
        (packets, _) = packets.Publish(300);

        Assert.Equal(300, packets.ContactCount);
        NativeBattleActionHeaderSlot slot = packets.ReadHeader(header);
        Assert.Equal(300, slot.ReadContactCount());
        Assert.Equal(new BattleContactOccurrence(49), packets.ReadContact(slot.FirstContact.Advance(49)).Occurrence);
        BattleContactOccurrence occurrence = packets.ResolveContact(header, 299);
        NativeBattleContactPacket reached = packets.ReadContact(slot.FirstContact.Advance(299));
        Assert.Equal(new BattleContactOccurrence(299), occurrence);
        Assert.Equal(occurrence, reached.Occurrence);
        Assert.Equal(final, reached);
        Assert.Throws<ArgumentOutOfRangeException>(() => packets.ReadContact(slot.FirstContact.Advance(300)));
    }

    [Fact]
    public void ReplacementKeepsContactExtentAndCompletionWritesOnlyItsCommandByte()
    {
        NativeBattleActionPacketsState packets = Create();
        (packets, BattleActionHeaderReference header) = packets.Prepare(Preparation);
        (packets, BattleContactOccurrence first) = packets.AppendContact(Contact);
        (packets, _) = packets.Publish(1);
        packets = packets.ActivateHeader(header);
        NativeBattleContactPacket replacement = Contact with { Primary = Contact.Primary with { Amount6 = 81 }, Secondary = new(Source, 0, 0, 0, 0, 81, 0), Occurrence = first };
        NativeBattleActionPacketsState updated = packets.ReplaceHeaderContacts(header, [replacement]);

        Assert.Equal(replacement, updated.ReadContact(first));
        Assert.Equal(first, updated.ReadContact(first).Occurrence);
        Assert.Equal(Contact, packets.ReadContact(first));
        Assert.Equal(packets.ReadHeader(header), updated.ReadHeader(header));
        Assert.Equal(packets.ActiveContact, updated.ActiveContact);
        Assert.Equal(packets.ContactCount, updated.ContactCount);
        Assert.Equal(packets.RetainedStorage.ContactCount, updated.RetainedStorage.ContactCount);
        Assert.Equal((ushort)81, updated.RetainedStorage.Contacts[0]!.Primary.Amount6);

        NativeBattleRetainedActionHeaderSlot raw = updated.RetainedStorage.Headers[0];
        NativeBattleActionPacketsState completed = updated.CompleteHeader(header);
        Assert.Equal(updated.ReadHeader(header) with { Command = byte.MaxValue }, completed.ReadHeader(header));
        Assert.Equal(raw.Payload.SetItem(1, byte.MaxValue), completed.RetainedStorage.Headers[0].Payload);
        Assert.Equal(raw.UnknownByteMask & ~2u, completed.RetainedStorage.Headers[0].UnknownByteMask);
        Assert.Equal(updated.RetainedStorage.Contacts, completed.RetainedStorage.Contacts);
    }

    [Fact]
    public void ReplacementRejectsWrongOccurrenceRecipientOrSecondaryWithoutPublishingEarlierWrites()
    {
        NativeBattleActionPacketsState packets = Create();
        (packets, BattleActionHeaderReference header) = packets.Prepare(Preparation);
        (packets, BattleContactOccurrence first) = packets.AppendContact(Contact);
        (packets, _) = packets.AppendContact(Contact);
        (packets, _) = packets.Publish(2);
        NativeBattleContactPacket changed = Contact with { Primary = Contact.Primary with { Amount6 = 81 } };
        Assert.Throws<InvalidDataException>(() => packets.ReplaceHeaderContacts(header, [changed]));
        Assert.Throws<InvalidDataException>(() => packets.ReplaceHeaderContacts(header, [changed, Contact with { Occurrence = first }]));
        Assert.Throws<InvalidDataException>(() => packets.ReplaceHeaderContacts(header, [changed, Contact with { Primary = Contact.Primary with { Recipient = Source } }]));
        Assert.Throws<InvalidDataException>(() => packets.ReplaceHeaderContacts(header, [changed, Contact with { Secondary = Contact.Primary }]));
        Assert.Equal(Contact, packets.ReadContact(first));
        Assert.Equal(Contact.Primary.Amount6, packets.RetainedStorage.Contacts[0]!.Primary.Amount6);
    }

    private static NativeBattleActionPacketsState Create()
    {
        BattleModelRoster roster = new([Source, Target]);
        NativeBattleModelAliases aliases = new(roster, [(NativeBattleModelSlot.Decode(0), Source), (NativeBattleModelSlot.Decode(3), Target)]);
        NativeBattleRetainedActionPackets retained = NativeBattleRetainedActionPackets.FromColdLoaderStorage(new byte[640], new byte[1176]);
        return NativeBattleActionPacketsState.Create(roster, retained, aliases);
    }
}
