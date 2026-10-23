namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBlaster186RectangleTests
{
    [Theory]
    [InlineData(255, 63, 63, 0x623F3FFFu)]
    [InlineData(-1, 258, 511, 0x62FF02FFu)]
    public void Rectangle_uses_each_low_color_byte_and_fully_written_native_COMMON_packet(int red, int green, int blue, uint color)
    {
        NativeBattleModulePacketBatch batch = NativeBattleFlashPackets.Emit571250(red, green, blue);
        NativeBattleAllocatedPrimitive packet = Assert.Single(batch.Primitives);
        Assert.Equal(16, batch.AllocationBytes);
        Assert.Equal(new uint[] { 0x03000000, color, 0, 0x00D80140 }, packet.Primitive.Words);
        Assert.Equal(17, packet.Primitive.Bucket);
        Assert.Equal(0, packet.ByteOffset);
        Assert.Equal((ushort)1, packet.Flags20);
        Assert.Equal(new int[] { 0, 0, 0, 0 }, packet.Primitive.VertexPayload4);
        Assert.Equal(16, packet.Primitive.PhysicalWriteMask!.Value.Length);
        Assert.All(packet.Primitive.PhysicalWriteMask.Value, value => Assert.Equal(NativeBattleGfPacketByteState.Known, value));
    }
}
