using static Memoria.FF8.Tests.NativeBattleTypedFixture;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectContactBridgeTests
{
    [Fact]
    public void RejectsAliasedRowsAndContactsThatDoNotBelongToTheAction()
    {
        NativeBattleContactHalf primary = new(OriginalOwner(3), 4, 0, 0, 0, 83, 0);
        Assert.Throws<InvalidDataException>(() => new NativeBattleEffectContactBridge<string>([("enemy-five", primary), ("enemy-six", primary)], OriginalRoster));
        Assert.Throws<InvalidDataException>(() => new NativeBattleEffectContactBridge<string>([("enemy-five", primary), ("enemy-five", primary with { Recipient = OriginalOwner(4) })], OriginalRoster));
        NativeBattleEffectContactBridge<string> bridge = new([("enemy-six", primary)], OriginalRoster);
        Assert.Equal("enemy-six", bridge.Resolve(new(primary, null)));
        Assert.Throws<NotSupportedException>(() => bridge.Resolve(new(primary with { Amount6 = 82 }, null)));
        Assert.Throws<NotSupportedException>(() => bridge.Resolve(new(primary with { Recipient = OriginalOwner(4) }, null)));
        Assert.Throws<NotSupportedException>(() => bridge.Resolve(new(primary, primary)));
    }

    [Fact]
    public void RetainsTheSecondaryContactOnTheEffectSourceRow()
    {
        NativeBattleContactHalf primary = new(OriginalOwner(3), 4, 0, 0, 0, 83, 0);
        NativeBattleContactHalf secondary = new(OriginalOwner(0), 0, 0, 1, 0, 41, 0);
        NativeBattleContactPacket expected = new(primary, secondary);
        NativeBattleEffectContactBridge<string> bridge = new([("enemy-ten", expected)], OriginalRoster);

        var (packets, targets) = bridge.Prepare(OriginalPackets(), 2, 1, 31, 39, OriginalOwner(0));

        Assert.Equal(new[] { OriginalOwner(3) }, targets.Enumerate());
        Assert.Equal(expected, packets.ReadContact(new BattleContactOccurrence(0)));
        Assert.Equal("enemy-ten", bridge.Resolve(expected));
        Assert.Throws<NotSupportedException>(() => bridge.Resolve(expected with { Secondary = secondary with { Amount6 = 40 } }));
        Assert.Throws<InvalidDataException>(() => bridge.Prepare(OriginalPackets(), 2, 1, 31, 39, OriginalOwner(1)));
    }

    [Fact]
    public void IdenticalPacketsUseThePublishedOccurrenceReferences()
    {
        NativeBattleContactPacket repeated = new(new(OriginalOwner(3), 4, 0, 0, 0, 83, 0), null);
        NativeBattleContactPacket other = repeated with { Primary = repeated.Primary with { Recipient = OriginalOwner(4) } };
        NativeBattleEffectContactBridge<string> bridge = new([("first", repeated), ("second", other), ("first", repeated)], OriginalRoster);

        var (packets, targets) = bridge.Prepare(OriginalPackets(), 2, 1, 31, 39, OriginalOwner(0));

        Assert.Equal(new[] { OriginalOwner(3), OriginalOwner(4) }, targets.Enumerate());
        Assert.Equal(1, packets.ActionCount);
        Assert.Equal(3, packets.ContactCount);
        Assert.Equal(3, packets.ExportRetained().Headers[0].ReadOriginalByte(16));
        Assert.Equal((2, "first"), bridge.ResolveOccurrence(packets.ReadContact(new BattleContactOccurrence(2))));
        Assert.Equal((0, "first"), bridge.ResolveOccurrence(packets.ReadContact(new BattleContactOccurrence(0))));
        Assert.Equal((1, "second"), bridge.ResolveOccurrence(other));
        Assert.Throws<NotSupportedException>(() => bridge.ResolveOccurrence(repeated));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BattleContactOccurrence(-1));
        Assert.Throws<NotSupportedException>(() => bridge.ResolveOccurrence(other with { Occurrence = new BattleContactOccurrence(3) }));
        Assert.Throws<NotSupportedException>(() => bridge.ResolveOccurrence(other with { Occurrence = new BattleContactOccurrence(0) }));
        Assert.Throws<NotSupportedException>(() => bridge.ResolveOccurrence(packets.ReadContact(new BattleContactOccurrence(2)) with { Secondary = new(OriginalOwner(0), 0, 0, 0, 0, 1, 0) }));
    }

    [Fact]
    public void ContactStreamReachesBeyondTheNativeWindowAndValidatesBothHalves()
    {
        NativeBattleContactPacket packet = new(new(OriginalOwner(6), 4, 0, 0, 0, 83, 0), new(OriginalOwner(0), 0, 0, 1, 0, 41, 0));
        const int capacity = 270;
        NativeBattleEffectContactBridge<string> bridge = new(Enumerable.Repeat(("enemy", packet), capacity), OriginalRoster);

        var (packets, targets) = bridge.Prepare(OriginalPackets(), 2, 1, 31, 39, OriginalOwner(0));

        Assert.Equal(new[] { OriginalOwner(6) }, targets.Enumerate());
        Assert.Equal(capacity, bridge.Count);
        Assert.Equal(capacity, packets.ContactCount);
        Assert.Equal((int?)capacity, packets.ReadHeader(new BattleActionHeaderReference(0)).ContactCount);
        Assert.Throws<NotSupportedException>(() => packets.ExportRetained().Headers[0].ReadOriginalByte(16));
        Assert.Equal((capacity - 1, "enemy"), bridge.ResolveOccurrence(packets.ReadContact(new BattleContactOccurrence(capacity - 1))));
        Assert.Throws<InvalidDataException>(() => new NativeBattleEffectContactBridge<string>(Array.Empty<(string, NativeBattleContactPacket)>(), OriginalRoster));
        Assert.Throws<InvalidDataException>(() => new NativeBattleEffectContactBridge<string>([("enemy", packet with { Primary = packet.Primary with { ExtendedDamageAmount = 80000 } })], OriginalRoster));
        Assert.Throws<InvalidDataException>(() => new NativeBattleEffectContactBridge<string>([("enemy", packet with { Secondary = packet.Secondary! with { ExtendedDamageAmount = 80000 } })], OriginalRoster));
    }

    [Fact]
    public void DistinctPacketsKeepTheirSelectedOwnersAndOriginalContactBytes()
    {
        NativeBattleContactPacket first = new(new(OriginalOwner(0), 4, 0, 0, 0, 83, 0), null);
        NativeBattleContactPacket second = first with { Primary = first.Primary with { Recipient = OriginalOwner(6), Amount6 = 193 } };
        NativeBattleEffectContactBridge<string> bridge = new([("source", first), ("enemy", second)], OriginalRoster);

        var (packets, targets) = bridge.Prepare(OriginalPackets(), 2, 1, 31, 39, OriginalOwner(0));

        Assert.Equal(new[] { OriginalOwner(0), OriginalOwner(6) }, targets.Enumerate());
        Assert.Equal(1, packets.ActionCount);
        Assert.Equal(2, packets.ContactCount);
        Assert.Equal(2, packets.ExportRetained().Headers[0].ReadOriginalByte(16));
        Assert.Equal(new byte[] { 0, 4, 0, 0, 0, 0, 83, 0, 0, 0, 0, 0, 255, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, NativeBattleContactPacketCodec.Encode(packets.ExportRetained().Contacts[0]!));
        Assert.Equal(new byte[] { 6, 4, 0, 0, 0, 0, 193, 0, 0, 0, 0, 0, 255, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, NativeBattleContactPacketCodec.Encode(packets.ExportRetained().Contacts[1]!));
        Assert.Equal("source", bridge.Resolve(first));
        Assert.Equal("enemy", bridge.Resolve(second));
    }
}
