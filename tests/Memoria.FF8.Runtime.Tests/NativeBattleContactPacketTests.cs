namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleContactPacketTests
{
    [Fact]
    public void OriginalTwoHalfLayoutRoundTripsWithoutDroppingHighStatusOrFlagBits()
    {
        byte[] bytes = Convert.FromHexString("0305020434127856EFCDAB800116AC80EFBEFFFF76543210");
        NativeBattleContactPacket packet = NativeBattleContactPacket.Decode(bytes);
        Assert.Equal(new NativeBattleContactHalf(3, 5, 2, 4, 0x1234, 0x5678, 0x80ABCDEF), packet.Primary);
        Assert.Equal(new NativeBattleContactHalf(1, 22, 0xAC, 0x80, 0xBEEF, 0xFFFF, 0x10325476), packet.Secondary);
        Assert.Equal((ushort)0x0402, packet.Primary.ContactFlags);
        Assert.Equal(bytes, packet.Encode());
    }

    [Theory]
    [InlineData(-1, 65535)]
    [InlineData(65536, 0)]
    [InlineData(60000, 60000)]
    public void ScratchDamagePublishesOnlyItsLowWord(int dword, int expected)
    {
        NativeBattleContactHalf half = NativeBattleContactHalf.FromScratch(3, 5, 0, 0, 0, dword, 0);
        Assert.Equal((ushort)expected, half.Amount6);
    }

    [Fact]
    public void SecondarySentinelIsRetainedAndMalformedPhysicalIndicesAreRejected()
    {
        NativeBattleContactPacket packet = new(new(0, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0));
        Assert.Equal((byte)255, NativeBattleContactPacket.Decode(packet.Encode()).Secondary.ActorIndex);
        Assert.Throws<InvalidDataException>(() => (packet with { Primary = packet.Primary with { ActorIndex = 255 } }).Encode());
        Assert.Throws<InvalidDataException>(() => NativeBattleContactPacket.Decode(new byte[23]));
    }
}
