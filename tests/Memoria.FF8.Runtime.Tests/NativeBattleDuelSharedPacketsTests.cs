namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuelSharedPacketsTests
{
    [Theory]
    [InlineData(2048, 2048, 0xAA020202u)]
    [InlineData(0x10000, 4096, 0xAA010101u)]
    [InlineData(-4096, 0, 0xAAFDFDFDu)]
    public void ColorInterpolationRoundsBothProductsUsesSignedWordWeightsAndStoresOnlyRgb(int firstWeight, int secondWeight, uint expected)
    {
        Assert.Equal(expected, NativeBattleDuelSharedPackets.InterpolateColor56CC00(0xFF030303, 0x55010101, firstWeight, secondWeight, 0xAA987654));
    }

    [Fact]
    public void ImpactFlashRetainsPhysicalAllocationAndOrderingTableInsertionOrder()
    {
        NativeBattleModulePacketBatch batch = NativeBattleDuelSharedPackets.Flash6CBF90();
        Assert.Equal(40, batch.AllocationBytes);
        Assert.Equal(new[] { 24, 0, 32 }, batch.Primitives.Select(primitive => primitive.ByteOffset));
        Assert.Equal(new uint[] { 0xE1000020, 0x2A808080, 0xE1000040 }, batch.Primitives.Select(primitive => primitive.Primitive.Words[1]));
        Assert.All(batch.Primitives, primitive => Assert.Equal(8, primitive.Primitive.Bucket));
    }

    [Theory]
    [InlineData(0, 80, 60, 40)]
    [InlineData(1, 60, 45, 30)]
    [InlineData(-2, 120, 90, 60)]
    public void FlashUsesSignedLinearChannelsAndTheActualSmallOrderingTable(int phase, byte red, byte green, byte blue)
    {
        NativeBattleModulePacketBatch result = NativeBattleDuelSharedPackets.Flash6A83B0(80, 60, 40, phase, 4);
        Assert.Equal(24, result.AllocationBytes);
        NativeBattleGeometryPrimitive primitive = Assert.Single(result.Primitives).Primitive;
        Assert.Equal(8, primitive.Bucket);
        Assert.Equal(new uint[] { 0x05000000, 0x2A000000u | red | (uint)green << 8 | (uint)blue << 16, 0, 320, 216u << 16, 216u << 16 | 320 }, primitive.Words);
        Assert.All(primitive.PhysicalWriteMask!.Value, value => Assert.Equal(NativeBattleGfPacketByteState.Known, value));
    }

    [Theory]
    [InlineData(4, 4)]
    [InlineData(5, 4)]
    [InlineData(0, 0)]
    public void ExpiredFlashDoesNotAllocateOrDivide(int phase, int duration)
    {
        NativeBattleModulePacketBatch result = NativeBattleDuelSharedPackets.Flash6A83B0(80, 60, 40, phase, duration);
        Assert.Empty(result.Primitives);
        Assert.Equal(0, result.AllocationBytes);
    }

    [Fact]
    public void RotationIsTheNineActualWordsWithoutInventingTranslationFields()
    {
        Assert.Equal(new short[] { 4096, 0, 0, 0, 4096, 0, 0, 0, 4096 }, NativeBattleDuelSharedPackets.Rotation6D9510(0));
        Assert.Equal(new short[] { 0, 0, 4096, 0, 4096, 0, -4096, 0, 0 }, NativeBattleDuelSharedPackets.Rotation6D9510(1024));
    }
}
