using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleFlashPacketsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Critical_tile_stores_every_byte_and_only_an_actual_link_loses_the_three_pointer_bytes(bool linked)
    {
        NativeBattleModulePacketBatch batch = NativeBattleFlashPackets.Emit(new(NativeBattleModuleDrawKind.Flash, 128));
        NativeBattleAllocatedPrimitive allocation = Assert.Single(batch.Primitives);
        Assert.Equal(16, batch.AllocationBytes);
        Assert.Equal((ushort)1, allocation.Flags20);
        Assert.Equal(17, allocation.Primitive.Bucket);
        NativeBattlePrimitiveProcess original = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400();
        NativeBattlePrimitiveProcess process = NativeBattleCommonPacketArena.StorePrimitive(original, 16, allocation.Primitive, linked);
        Assert.Null(original.CommonPacketMemory);
        Assert.Equal(new byte[] { 128, 128, 128, 0x62, 0, 0, 0, 0, 0x40, 1, 0xD8, 0 }, process.ReadCommonPacket(20, 12));
        Assert.Equal(new byte[] { 3 }, process.ReadCommonPacket(19, 1));
        if (linked)
        {
            Assert.Throws<NotSupportedException>(() => process.ReadCommonPacket(16, 3));
        }
        else
        {
            Assert.Equal(new byte[] { 0, 0, 0 }, process.ReadCommonPacket(16, 3));
        }
    }

    [Fact]
    public void Private_flash_overwrites_unknown_bytes_without_allocating_common_memory_and_survives_JSON()
    {
        const int offset = 0x8000;
        NativeBattleEffectMemory original = NativeBattleEffectMemory.FromColdProcess().BeginLoad(3);
        original = original with { Arena = original.Arena!.MarkUnknown(offset, 24) };
        NativeBattleModuleDraw draw = Polygon(offset);
        NativeBattleModulePacketBatch batch = NativeBattleFlashPackets.Emit(draw);
        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(original, offset, NativeBattleLogicalOrdering.Clear(), batch, NativeBattleEffectGouraudWrites.FullDword, 7);
        Assert.Equal(offset + 24, written.Cursor);
        Assert.Equal(JsonSerializer.Serialize(original.PrimitiveProcess), JsonSerializer.Serialize(written.Memory.PrimitiveProcess));
        Assert.Throws<NotSupportedException>(() => original.Arena!.Read(offset, 24));
        Assert.Equal(3, written.Memory.Arena!.Read(offset, 3).Length);
        Assert.Equal(5, written.Memory.Arena!.Read(offset + 3, 1)[0]);
        byte[] body = written.Memory.Arena!.Read(offset + 4, 20);
        Assert.Equal(0x2A808080u, BinaryPrimitives.ReadUInt32LittleEndian(body));
        Assert.Equal(320u | 216u << 16, BinaryPrimitives.ReadUInt32LittleEndian(body.AsSpan(16)));
        NativeBattleEffectMemory restored = JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(written.Memory))!;
        restored.Validate();
        Assert.Equal(body, restored.Arena!.Read(offset + 4, 20));
        Assert.Equal(JsonSerializer.Serialize(draw), JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleModuleDraw>(JsonSerializer.Serialize(draw))!));
        Assert.Equal(7, Assert.Single(written.Ordering.Proxies).ModuleDraw);
    }

    [Fact]
    public void A_private_polygon_requires_its_actual_aligned_arena_receipt()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleFlashPackets.Emit(Polygon(null)));
        Assert.Throws<NotSupportedException>(() => NativeBattleFlashPackets.Emit(Polygon(1)));
        Assert.Throws<NotSupportedException>(() => NativeBattleFlashPackets.Emit(Polygon(NativeBattleEffectArena.Length - 20)));
        Assert.Throws<NotSupportedException>(() => NativeBattleFlashPackets.Emit(new(NativeBattleModuleDrawKind.Flash, 128) { NativeEffectArenaOffset = 0 }));
    }

    private static NativeBattleModuleDraw Polygon(int? offset) => new(NativeBattleModuleDrawKind.Flash, 128) { NativePrimitiveCommand = 0x2A, NativeOrderingBucket = 8, NativeEffectArenaOffset = offset, NativeScreenVertices = [new(0, 0), new(320, 0), new(0, 216), new(320, 216)] };
}
