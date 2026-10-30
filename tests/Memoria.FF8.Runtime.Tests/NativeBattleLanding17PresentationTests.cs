using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleLanding17PresentationTests
{
    [Theory]
    [InlineData(2048)]
    [InlineData(6143)]
    public void Spark_capture_retains_original_scaling_depth_bias_packet_bytes_and_birth_identity(int scale)
    {
        byte[] bytes = new byte[38];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), 14);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 0xFFFF);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(14), 1);
        bytes[18] = bytes[20] = 2;
        bytes[19] = 100;
        bytes[21] = 0x2C;
        bytes[22] = 10;
        bytes[23] = 20;
        NativeBattleModuleResource resource = new(0, 0x1810000, 0, 0, bytes.ToImmutableArray());
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2556FC8, 0x20DFAB8, 32, 1);
        (pool, int slot) = pool.Append(0x700B70);
        NativeBattlePilotVisualIdentity identity = NativeBattlePilotVisualLifetimes.Identity(17, pool.Slots[slot]) with { ResourceGeneration = resource.Address };
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resource, SpriteFrame = resource.SpriteFrame(0), Anchor = new(0, 0, 4096, 0), Scale = scale, DepthOffset = -(scale >> 2), SpriteFlags = 0 };
        NativeBattleCameraProjection projection = new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108);
        NativeBattleModulePacketBatch plain = NativeBattleModulePackets.EmitSprite571DC0(draw, projection, 0, 17, 2);
        NativeBattleModulePacketBatch captured = NativeBattleModulePackets.EmitSprite571DC0(draw with { PilotIdentity = identity }, projection, 0, 17, 2);
        Assert.Equal(plain.AllocationBytes, captured.AllocationBytes);
        NativeBattleAllocatedPrimitive a = Assert.Single(plain.Primitives);
        NativeBattleAllocatedPrimitive b = Assert.Single(captured.Primitives);
        Assert.Equal(a.ByteOffset, b.ByteOffset);
        Assert.Equal(a.Flags20, b.Flags20);
        Assert.Equal(a.Primitive.Words, b.Primitive.Words);
        Assert.Equal(a.Primitive.VertexPayload4, b.Primitive.VertexPayload4);
        NativeBattlePilotVisualGeometry visual = Assert.IsType<NativeBattlePilotVisualGeometry>(b.Primitive.PilotGeometry);
        NativeBattleActionMatrix camera = NativeBattlePoseMatrices.ModuleSpriteCamera(draw, projection.View!, 0);
        Assert.Equal((float)camera.Z, visual.A.Z);
        Assert.Equal(identity, visual.Identity);
        pool = pool.Advance((current, _) => new(current, 2)).Pool;
        (pool, slot) = pool.Append(0x700B70);
        Assert.NotEqual(identity.Generation, NativeBattlePilotVisualLifetimes.Identity(17, pool.Slots[slot]).Generation);
    }

    [Fact]
    public void Physical_append_ownership_is_independent_of_geometry_capture_and_not_serialized()
    {
        NativeBattleGeometryPrimitive primitive = new(17, [0x01000000, 0xE1000000], []);
        NativeBattleGfGeometryBatch batch = new([new(0, new byte[8].ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 8).ToImmutableArray(), primitive), new(8, new byte[8].ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 8).ToImmutableArray(), null)], 8);
        ImmutableArray<NativeBattlePrimitiveReference> references = NativeBattleEffectPresentationPackets.Record([], "20DFAB8", 0, 128, batch);
        Assert.Equal(new NativeBattlePrimitiveReference("20DFAB8", 0, 128), Assert.Single(references));
        NativeBattleOriginalEffectState effect = new(17, 0, new(17, 0x700360, 0), null, 0) { PresentationPackets = references };
        string serialized = JsonSerializer.Serialize(effect);
        Assert.DoesNotContain("PresentationPackets", serialized);
        Assert.Empty(JsonSerializer.Deserialize<NativeBattleOriginalEffectState>(serialized)!.PresentationPackets);
    }
}
