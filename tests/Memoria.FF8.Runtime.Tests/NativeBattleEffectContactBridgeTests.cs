using static Memoria.FF8.Tests.NativeBattleTypedFixture;
namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectContactBridgeTests
{
    [Fact]
    public void RejectsAliasedRowsAndContactsThatDoNotBelongToTheAction()
    {
        NativeBattleContactHalf primary = new(OriginalOwner(3), 4, 0, 0, 0, 83, 0);
        Assert.Throws<InvalidDataException>(() => new NativeBattleEffectContactBridge<string>([("enemy-five", primary), ("enemy-six", primary)]));
        Assert.Throws<InvalidDataException>(() => new NativeBattleEffectContactBridge<string>([("enemy-five", primary), ("enemy-five", primary with { ActorIndex = 4 })]));
        NativeBattleEffectContactBridge<string> bridge = new([("enemy-six", primary)]);
        NativeBattleContactHalf absent = new(255, 0, 0, 0, 0, 0, 0);
        Assert.Equal("enemy-six", bridge.Resolve(new(primary, absent)));
        Assert.Throws<NotSupportedException>(() => bridge.Resolve(new(primary with { Amount6 = 82 }, absent)));
        Assert.Throws<NotSupportedException>(() => bridge.Resolve(new(primary with { ActorIndex = 4 }, absent)));
        Assert.Throws<NotSupportedException>(() => bridge.Resolve(new(primary, primary)));
    }

    [Fact]
    public void RetainsTheSecondaryContactOnTheEffectSourceRow()
    {
        NativeBattleContactHalf primary = new(OriginalOwner(3), 4, 0, 0, 0, 83, 0);
        NativeBattleContactHalf secondary = new(OriginalOwner(0), 0, 0, 1, 0, 41, 0);
        NativeBattleContactPacket expected = new(primary, secondary);
        NativeBattleEffectContactBridge<string> bridge = new([("enemy-ten", expected)]);

        var (packets, mask) = bridge.Prepare(2, 1, 31, 39);

        Assert.Equal(8, mask);
        Assert.Equal(expected, packets.ReadContact(new BattleContactOccurrence(0)));
        Assert.Equal("enemy-ten", bridge.Resolve(expected));
        Assert.Throws<NotSupportedException>(() => bridge.Resolve(expected with { Secondary = secondary with { Amount6 = 40 } }));
        Assert.Throws<InvalidDataException>(() => bridge.Prepare(2, 1, 31, 39, source: 1));
    }

    [Fact]
    public void IdenticalPacketsUseTheirPhysicalIndicesToDistinguishOccurrences()
    {
        NativeBattleContactPacket repeated = new(new(OriginalOwner(3), 4, 0, 0, 0, 83, 0), new(255, 0, 0, 0, 0, 0, 0));
        NativeBattleContactPacket other = repeated with { Primary = repeated.Primary with { ActorIndex = 4 } };
        NativeBattleEffectContactBridge<string> bridge = new([("first", repeated), ("second", other), ("first", repeated)]);

        var (packets, mask) = bridge.Prepare(2, 1, 31, 39);

        Assert.Equal(24, mask);
        Assert.Equal(1, packets.ActionCount);
        Assert.Equal(3, packets.ContactCount);
        Assert.Equal(3, packets.ExportRetained().Headers[0].ReadOriginalByte(16));
        Assert.Equal((2, "first"), bridge.ResolveOccurrence(packets.ReadContact(new BattleContactOccurrence(2))));
        Assert.Equal((0, "first"), bridge.ResolveOccurrence(packets.ReadContact(new BattleContactOccurrence(0))));
        Assert.Equal((1, "second"), bridge.ResolveOccurrence(other));
        Assert.Throws<NotSupportedException>(() => bridge.ResolveOccurrence(repeated));
        Assert.Throws<NotSupportedException>(() => bridge.ResolveOccurrence(other with { PhysicalContactIndex = -1 }));
        Assert.Throws<NotSupportedException>(() => bridge.ResolveOccurrence(other with { PhysicalContactIndex = 3 }));
        Assert.Throws<NotSupportedException>(() => bridge.ResolveOccurrence(other with { PhysicalContactIndex = 0 }));
        Assert.Throws<NotSupportedException>(() => bridge.ResolveOccurrence(packets.ReadContact(new BattleContactOccurrence(2)) with { Secondary = repeated.Secondary with { Amount6 = 1 } }));
    }

    [Fact]
    public void ContactStreamUsesTheNativePacketCapacityAndValidatesBothHalves()
    {
        NativeBattleContactPacket packet = new(new(OriginalOwner(6), 4, 0, 0, 0, 83, 0), new(OriginalOwner(0), 0, 0, 1, 0, 41, 0));
        int capacity = NativeBattleActionPacketsState.ContactCapacity;
        NativeBattleEffectContactBridge<string> bridge = new(Enumerable.Repeat(("enemy", packet), capacity));

        var (packets, mask) = bridge.Prepare(2, 1, 31, 39);

        Assert.Equal(64, mask);
        Assert.Equal(capacity, bridge.Count);
        Assert.Equal(capacity, packets.ContactCount);
        Assert.Equal(capacity, packets.ExportRetained().Headers[0].ReadOriginalByte(16));
        Assert.Equal((capacity - 1, "enemy"), bridge.ResolveOccurrence(packets.ReadContact(capacity - 1)));
        Assert.Throws<InvalidDataException>(() => new NativeBattleEffectContactBridge<string>(Array.Empty<(string, NativeBattleContactPacket)>()));
        Assert.Throws<InvalidDataException>(() => new NativeBattleEffectContactBridge<string>(Enumerable.Repeat(("enemy", packet), capacity + 1)));
        Assert.Throws<InvalidDataException>(() => new NativeBattleEffectContactBridge<string>([("enemy", packet with { Primary = packet.Primary with { ExtendedDamageAmount = 80000 } })]));
        Assert.Throws<InvalidDataException>(() => new NativeBattleEffectContactBridge<string>([("enemy", packet with { Secondary = packet.Secondary with { ExtendedDamageAmount = 80000 } })]));
    }

    [Fact]
    public void DistinctPacketsKeepTheirNativeHeaderMaskAndBytes()
    {
        NativeBattleContactPacket first = new(new(OriginalOwner(0), 4, 0, 0, 0, 83, 0), new(255, 0, 0, 0, 0, 0, 0));
        NativeBattleContactPacket second = first with { Primary = first.Primary with { ActorIndex = 6, Amount6 = 193 } };
        NativeBattleEffectContactBridge<string> bridge = new([("source", first), ("enemy", second)]);

        var (packets, mask) = bridge.Prepare(2, 1, 31, 39);

        Assert.Equal(65, mask);
        Assert.Equal(1, packets.ActionCount);
        Assert.Equal(2, packets.ContactCount);
        Assert.Equal(2, packets.ExportRetained().Headers[0].ReadOriginalByte(16));
        Assert.Equal(first.Encode(), packets.ReadContact(new BattleContactOccurrence(0)).Encode());
        Assert.Equal(second.Encode(), packets.ReadContact(new BattleContactOccurrence(1)).Encode());
        Assert.Equal("source", bridge.Resolve(first));
        Assert.Equal("enemy", bridge.Resolve(second));
    }
}
