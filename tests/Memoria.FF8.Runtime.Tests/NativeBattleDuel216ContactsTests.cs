using static Memoria.FF8.Tests.NativeBattleTypedFixture;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel216ContactsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExplicitFirstContactIgnoresCountAndOtherHeaderAfterResetAndPrivateContinuation(bool unknownCount)
    {
        NativeBattleActionPacketsState packets = OriginalPackets();
        (packets, _) = packets.Prepare(new(OriginalOwner(0), 239, 8, -1, 216, 0, null));
        (packets, _) = packets.AppendContact(Contact(17));
        (packets, _) = packets.Publish(1);
        (packets, BattleActionHeaderReference header) = packets.Prepare(new(OriginalOwner(0), 239, 8, -1, 216, 0, null));
        (packets, _) = packets.AppendContact(Contact(29));
        (packets, _) = packets.AppendContact(Contact(31));
        if (!unknownCount)
        {
            (packets, _) = packets.Publish(2);
        }
        packets = packets.ActivateHeader(new BattleActionHeaderReference(0)).ResetCounts();
        packets = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(packets));
        Assert.Equal((ushort)29, NativeBattleDuel216Contacts.Read506690(packets, header).Primary.Amount6);
        Assert.Equal((ushort)17, NativeBattleDuel216Contacts.Read506690(packets, new BattleActionHeaderReference(0)).Primary.Amount6);
    }

    [Fact]
    public void ExplicitFirstContactRejectsTheUnwrittenCell()
    {
        NativeBattleActionPacketsState packets = OriginalPackets();
        (packets, BattleActionHeaderReference header) = packets.Prepare(new(OriginalOwner(0), 239, 8, -1, 216, 0, null));
        Assert.Throws<InvalidDataException>(() => NativeBattleDuel216Contacts.Read506690(packets, header));
    }

    private static NativeBattleContactPacket Contact(ushort damage) => new(new(OriginalOwner(3), 4, 0, 0, 0, damage, 0), null);
}
