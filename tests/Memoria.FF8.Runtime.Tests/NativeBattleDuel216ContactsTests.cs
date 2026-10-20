using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel216ContactsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExplicitFirstContactIgnoresCountAndOtherHeaderAfterResetAndSnapshot(bool unknownCount)
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(0, 239, 8, -1, 216, 0, null));
        (packets, _) = packets.AppendContact(Contact(17));
        (packets, _) = packets.Publish(1);
        (packets, int header) = packets.Prepare(new(0, 239, 8, -1, 216, 0, null));
        (packets, _) = packets.AppendContact(Contact(29));
        (packets, _) = packets.AppendContact(Contact(31));
        (packets, _) = packets.Publish(2);
        packets = packets.ActivateHeader(0).ResetCounts();
        if (unknownCount)
        {
            NativeBattleActionHeaderSlot source = packets.Headers[header];
            packets = packets with { Headers = packets.Headers.SetItem(header, source with { UnknownByteMask = source.UnknownByteMask | (1u << 16) }) };
        }
        packets = JsonSerializer.Deserialize<NativeBattleActionPacketsState>(JsonSerializer.Serialize(packets))!;
        Assert.Equal((ushort)29, NativeBattleDuel216Contacts.Read506690(packets, header).Primary.Amount6);
        Assert.Equal((ushort)17, NativeBattleDuel216Contacts.Read506690(packets, 0).Primary.Amount6);
        NativeBattleActionHeaderSlot missing = packets.Headers[header] with { FirstContactIndex = null };
        packets = packets with { Headers = packets.Headers.SetItem(header, missing) };
        Assert.Throws<NotSupportedException>(() => NativeBattleDuel216Contacts.Read506690(packets, header));
    }

    private static NativeBattleContactPacket Contact(ushort damage) => new(new(3, 4, 0, 0, 0, damage, 0), new(255, 0, 0, 0, 0, 0, 0));
}
