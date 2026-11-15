using static Memoria.FF8.Tests.NativeBattleTypedFixture;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect312ContactsTests
{
    [Fact]
    public void Finds_first_matching_primary_at_its_relative_position_without_reading_unwritten_suffix()
    {
        var (packets, header) = Prepare(3, OriginalOwner(2), OriginalOwner(0));
        Assert.Equal(1, NativeBattleEffect312Contacts.Find(packets, header, OriginalOwner(0)));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect312Contacts.Find(packets, header, OriginalOwner(1)));
    }

    [Fact]
    public void Zero_count_does_not_read_an_unwritten_contact()
    {
        var (packets, header) = Prepare(0);
        Assert.Null(NativeBattleEffect312Contacts.Find(packets, header, OriginalOwner(0)));
    }

    [Fact]
    public void Missing_actor_returns_no_position_after_the_declared_count()
    {
        var (packets, header) = Prepare(2, OriginalOwner(2), OriginalOwner(2), OriginalOwner(1));
        Assert.Null(NativeBattleEffect312Contacts.Find(packets, header, OriginalOwner(1)));
    }

    [Fact]
    public void Required_header_count_still_requires_its_actual_writer()
    {
        var (packets, header) = Prepare(null);
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect312Contacts.Find(packets, header, OriginalOwner(0)));
    }

    private static (NativeBattleActionPacketsState Packets, BattleActionHeaderReference Header) Prepare(int? count, params BattleModelOwner[] targets)
    {
        NativeBattleActionPacketsState packets = OriginalPackets();
        (packets, _) = packets.AppendContact(new(new(OriginalOwner(0), 0, 0, 0, 0, 0, 0), null));
        (packets, BattleActionHeaderReference header) = packets.Prepare(new(OriginalOwner(3), 0, 0, 0, 0, 312, null));
        packets = packets.AppendContacts(targets.Select(owner => new NativeBattleContactPacket(new(owner, 0, 0, 0, 0, 0, 0), null)));
        if (count is int declared)
        {
            (packets, _) = packets.Publish(declared);
        }
        return (packets, header);
    }
}
