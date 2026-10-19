using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using Xunit;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleModuleSpriteTemporaryTests
{
    [Fact]
    public void PhysicalContextPublishesItsResourcePointerAndReachedQuadFieldsWithoutChangingPackets()
    {
        uint address = NativeBattleTemporaryMemory.Address + 72;
        NativeBattleModuleResource resource = Resource(1);
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resource, SpriteFrame = resource.ProjectedFrame571C80(0), Anchor = new(0, 0, 0, 0), Scale = 4096, SpriteFlags = 8 };
        NativeBattleFileStaging staging = NativeBattleModuleSpriteTemporary.Begin571C80(Cold(), address, resource.Address, 8);
        NativeBattleModuleSpriteTemporaryResult result = NativeBattleModuleSpriteTemporary.Emit571C80(staging, address, draw, Projection(), 0, 17, 2);
        Assert.Equal(resource.Address, Dword(result.Staging, address));
        Assert.Equal(resource.Address + 18, Dword(result.Staging, address + 44));
        Assert.Equal(ushort.MaxValue, Word(result.Staging, address + 40));
        Assert.Equal(256u, Dword(result.Staging, address + 60));
        Assert.Equal(0u, Dword(result.Staging, address + 64));
        Assert.Equal(unchecked((ushort)-64), Word(result.Staging, address + 68));
        Assert.Equal(64, Word(result.Staging, address + 92));
        Assert.Equal(4096, Word(result.Staging, address + 100));
        Assert.Equal(1024u, Dword(result.Staging, address + 128));
        Assert.Equal(new byte[] { 0xA5, 0xA5 }, result.Staging.Read(address + 118, 2));
        NativeBattleModulePacketBatch expected = NativeBattleModulePackets.EmitSprite571DC0(draw, Projection(), 0, 17, 2);
        Assert.Equal(expected.AllocationBytes, result.Batch.AllocationBytes);
        Assert.Equal(JsonSerializer.Serialize(expected.Primitives), JsonSerializer.Serialize(result.Batch.Primitives));
        Assert.NotNull(result.Batch.CapturedWrites);
    }

    [Fact]
    public void EmptyFrameKeepsPriorQuadScratchAndRetiresItsAnimationWithoutProducingPackets()
    {
        uint address = NativeBattleTemporaryMemory.Address + 80;
        NativeBattleModuleResource resource = Resource(0);
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resource, SpriteFrame = resource.ProjectedFrame571C80(0), Anchor = new(0, 0, 0, 0), Scale = 4096, SpriteFlags = 8 };
        NativeBattleFileStaging staging = NativeBattleModuleSpriteTemporary.Begin571C80(Cold(), address, resource.Address, 8);
        NativeBattleModuleSpriteTemporaryResult result = NativeBattleModuleSpriteTemporary.Emit571C80(staging, address, draw, Projection(), 0, 17, 2);
        Assert.Equal(uint.MaxValue, Dword(result.Staging, address + 60));
        Assert.Equal(0xA5A5A5A5u, Dword(result.Staging, address + 64));
        Assert.Equal(0xA5A5A5A5u, Dword(result.Staging, address + 100));
        Assert.Equal(0u, Dword(result.Staging, address + 48));
        Assert.Empty(result.Batch.Primitives);
        Assert.Equal(0, result.Batch.AllocationBytes);
    }

    private static NativeBattleModuleResource Resource(int quads)
    {
        byte[] bytes = new byte[18 + quads * 20];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), 14);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12), -1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(14), quads);
        if (quads != 0)
        {
            bytes[18] = 8;
            bytes[19] = 128;
            bytes[20] = 4;
            bytes[21] = 0x2E;
        }
        return new(0, 0xD70000, 0, 0, bytes.ToImmutableArray());
    }

    private static NativeBattleCameraProjection Projection() => new(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 1024, 4096), 256, 160, 108);
    private static NativeBattleFileStaging Cold() => NativeBattleFileStaging.Unknown().Write(NativeBattleTemporaryMemory.Address, Enumerable.Repeat((byte)0xA5, NativeBattleTemporaryMemory.ReachedLength).ToArray());
    private static ushort Word(NativeBattleFileStaging staging, uint address) => BinaryPrimitives.ReadUInt16LittleEndian(staging.Read(address, 2));
    private static uint Dword(NativeBattleFileStaging staging, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(address, 4));
}
