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

    private static NativeBattleActionPacketsState Create()
    {
        BattleModelRoster roster = new([Source, Target]);
        NativeBattleModelAliases aliases = new(roster, [(NativeBattleModelSlot.Decode(0), Source), (NativeBattleModelSlot.Decode(3), Target)]);
        NativeBattleRetainedActionPackets retained = NativeBattleRetainedActionPackets.FromColdLoaderStorage(new byte[640], new byte[1176]);
        return NativeBattleActionPacketsState.Create(roster, retained, aliases);
    }
}
