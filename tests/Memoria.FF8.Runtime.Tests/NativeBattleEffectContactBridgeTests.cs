namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectContactBridgeTests
{
    [Fact]
    public void RejectsAliasedRowsAndContactsThatDoNotBelongToTheAction()
    {
        NativeBattleContactHalf primary = new(3, 4, 0, 0, 0, 83, 0);
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
        NativeBattleContactHalf primary = new(3, 4, 0, 0, 0, 83, 0);
        NativeBattleContactHalf secondary = new(0, 0, 0, 1, 0, 41, 0);
        NativeBattleContactPacket expected = new(primary, secondary);
        NativeBattleEffectContactBridge<string> bridge = new([("enemy-ten", expected)]);

        var (packets, mask) = bridge.Prepare(2, 1, 31, 39);

        Assert.Equal(8, mask);
        Assert.Equal(expected, packets.ReadContact(0));
        Assert.Equal("enemy-ten", bridge.Resolve(expected));
        Assert.Throws<NotSupportedException>(() => bridge.Resolve(expected with { Secondary = secondary with { Amount6 = 40 } }));
    }
}
