using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSpriteCaptureTests
{
    [Fact]
    public void ExtendedSpriteRetainsPhysicalE1PaddingAndNativeNonsequentialInsertion()
    {
        NativeBattleGeometryPrimitive environment = new(17, [0x1000000, 0xE1000220], [0, 0, 0, 0]);
        NativeBattleGeometryPrimitive sprite = new(17, [0x9000000, 0x2C808080, 1, 2, 3, 4, 5, 6, 7, 8], [100, 100, 100, 100]);
        NativeBattleModulePacketBatch source = new([new(environment, 40, 0), new(sprite, 0, 0)], 52);
        NativeBattleGfGeometryBatch captured = NativeBattleModulePackets.CaptureSprite571DC0(source);
        Assert.Equal(12, captured.Attempts[0].PacketLength);
        Assert.Equal(40, captured.Attempts[0].ByteOffsetRelative);
        Assert.All(captured.Attempts[0].WriteMask.Skip(8), state => Assert.Equal(NativeBattleGfPacketByteState.Untouched, state));
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(48, new byte[] { 9, 10, 11, 12 }) };
        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(memory, 0, NativeBattleLogicalOrdering.Clear(), captured);
        Assert.Equal(52, written.Cursor);
        Assert.Equal(new byte[] { 9, 10, 11, 12 }, written.Memory.Arena!.Read(48, 4));
        Assert.Equal(0xE1000220u, BinaryPrimitives.ReadUInt32LittleEndian(written.Memory.Arena.Read(44, 4)));
    }
}
