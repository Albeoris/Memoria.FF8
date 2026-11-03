namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSpriteCaptureTests
{
    [Fact]
    public void LogicalSpritePacketsCannotFabricateMissingPhysicalStores()
    {
        NativeBattleGeometryPrimitive environment = new(17, [0x1000000, 0xE1000220], [0, 0, 0, 0]);
        NativeBattleGeometryPrimitive sprite = new(17, [0x9000000, 0x2C808080, 1, 2, 3, 4, 5, 6, 7, 8], [100, 100, 100, 100]);
        NativeBattleModulePacketBatch source = new([new(environment, 40, 0), new(sprite, 0, 0)], 52);
        Assert.Throws<NotSupportedException>(() => NativeBattleModulePackets.CaptureSprite571DC0(source));
    }

    [Fact]
    public void CapturingAnActualReceiptPreservesTheOriginalStoresAndKnownness()
    {
        NativeBattleGfGeometryBatch actual = new([new(0, [0, 0, 0, 9], [NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Known], null)], 0);
        NativeBattleModulePacketBatch source = new([], 0) { CapturedWrites = actual };

        Assert.Same(actual, NativeBattleModulePackets.CaptureSprite571DC0(source));
    }
}
