namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedActionGraphicsTests
{
    [Theory]
    [InlineData(256)]
    [InlineData(300)]
    public void FirstHeaderSemanticCountDrivesServiceSelection(int count)
    {
        var source = new BattlePartyActorModel(new("source"));
        var target = new BattleEnemyActorModel(new("target"));
        BattleModelRoster roster = new([source, target]);
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Create(roster, NativeBattleRetainedActionPackets.Fresh(), new(roster, []));
        BattleActionHeaderReference header;
        (packets, header) = packets.Prepare(new(source, 0, 0, -1, 0, 0, null));
        packets = packets.AppendContacts(Enumerable.Repeat(new NativeBattleContactPacket(new(target, 0, 0, 0, 0, 0, 0), null), count));
        (packets, _) = packets.Publish(count);
        packets = packets.WritePlayerRepeatMode(header, 1);

        Assert.Equal(0x50BD80u, NativeBattleActionGraphics.SelectService(packets.ReadHeader(header)).Callback);
        NativeBattleActionGraphicsState action = NativeBattleActionGraphics.Normalize(packets, header);
        Assert.Equal(count, NativeBattleActionGraphics.ReadFirstHeaderTargetCount5044B0(action));
        Assert.Equal(target, action.FirstTarget);
        Assert.True(action.TargetActors.IsSingle(target));
        Assert.Equal(0x50BD80u, action.Callback);
    }

    [Fact]
    public void NormalizationCarriesCoverFromTheEarlierHeaderButUsesTheNewFirstTarget()
    {
        var source = new BattlePartyActorModel(new("source"));
        var cover = new BattlePartyActorModel(new("cover"));
        var first = new BattleEnemyActorModel(new("first"));
        var second = new BattleEnemyActorModel(new("second"));
        BattleModelRoster roster = new([source, cover, first, second]);
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Create(roster, NativeBattleRetainedActionPackets.Fresh(), new(roster, []));
        BattleActionHeaderReference firstHeader;
        (packets, firstHeader) = packets.Prepare(new(source, 0, 0, -1, 0, 0, null));
        (packets, _) = packets.AppendContact(new(new(first, 22, 0, 8, 0, 0, 0) { CoverSource = cover }, null));
        (packets, _) = packets.Publish(1);
        packets = packets.WritePlayerRepeatMode(firstHeader, 1);
        NativeBattleActionGraphicsState previous = NativeBattleActionGraphics.Normalize(packets, firstHeader);

        BattleActionHeaderReference secondHeader;
        (packets, secondHeader) = packets.Prepare(new(source, 0, 0, -1, 0, 0, null));
        (packets, _) = packets.AppendContact(new(new(second, 0, 0, 0, 0, 0, 0), null));
        (packets, _) = packets.Publish(1);
        packets = packets.WritePlayerRepeatMode(secondHeader, 1);

        NativeBattleActionGraphicsState action = NativeBattleActionGraphics.Normalize(packets, secondHeader, previous);
        Assert.Equal(cover, action.CoverSource);
        Assert.Equal(second, action.FirstTarget);
        Assert.True(action.TargetActors.IsSingle(second));
        Assert.True(action.CoverActors.IsEmpty);
        Assert.Equal(new BattleContactCursor(1), action.FirstContact);
    }
}
