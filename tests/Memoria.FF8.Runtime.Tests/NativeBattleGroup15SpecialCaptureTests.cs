using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGroup15SpecialCaptureTests
{
    [Theory]
    [InlineData(186, 0)]
    [InlineData(190, 0)]
    [InlineData(190, 1)]
    public void Static_capture_preserves_complete_native_ledger_and_actual_selected_operands(int effect, int copy)
    {
        const uint source = 0x10000000;
        uint selected = NativeBattleBlaster186Profile.MutableFrameAddresses[15];
        NativeBattleActionMatrix camera = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleCameraProjection projection = new(camera, 100, 160, 108);
        NativeBattleStaticMeshResource mesh = StaticMesh(source);
        NativeBattlePilotVisualIdentity identity = TaskIdentity((ushort)effect, copy) with { ResourceGeneration = source };
        List<int> plainReads = [];
        List<int> capturedReads = [];
        NativeBattleStaticMeshContext88Step plain = Draw(null, plainReads);
        NativeBattleStaticMeshContext88Step captured = Draw(identity, capturedReads);
        Assert.Equal(plainReads, capturedReads);
        Assert.Equal(new[] { 0, 8, 16, 24 }, capturedReads);
        Assert.Equal(NativeBattleTestSnapshots.Json(plain.Memory), NativeBattleTestSnapshots.Json(captured.Memory));
        Assert.Equal(NativeBattleTestSnapshots.Json(plain.Context), NativeBattleTestSnapshots.Json(captured.Context));
        AssertLedger(plain.Batch, captured.Batch);
        NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(Assert.Single(captured.Batch.Attempts.Where(attempt => attempt.Primitive is not null)).Primitive!.PilotGeometry);
        Assert.Equal(identity.ResourceGeneration, geometry.Identity.ResourceGeneration);
        Assert.Equal(new Vector3(-200, -100, 2000), geometry.A);
        Assert.Equal(new Vector3(200, -100, 2000), geometry.B);
        Assert.Equal(new Vector3(-200, 100, 2000), geometry.C);
        Assert.Equal(new Vector3(200, 100, 2000), geometry.D);
        Assert.False(captured.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value[6]);
        Assert.False(captured.Memory.ScriptedColorRegisters.NormalInputKnown.Value[7]);

        NativeBattleStaticMeshContext88Step Draw(NativeBattlePilotVisualIdentity? pilot, List<int> reads)
        {
            NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 88).Write(0, BitConverter.GetBytes(source)).Write(4, BitConverter.GetBytes(selected)).Write(8, new byte[4]).Write(0x10, new byte[4]).Write(0x18, new byte[4]).Write(0x1C, BitConverter.GetBytes(0x2033u));
            return NativeBattleStaticMeshContext88.DrawLoaded(Memory(), mesh, projection, context, NativeBattleGteColorInterpolation.CodeConstants45F270, selectedVerticesAddress: selected, readSelectedVertex: offset =>
            {
                reads.Add(offset);
                NativeBattleVector[] vertices = [new(-200, -100, 0), new(200, -100, 0), new(-200, 100, 0), new(200, 100, 0)];
                return (vertices[offset / 8], null);
            }, pilotIdentity: pilot);
        }
    }

    [Fact]
    public void Selected_rejection_preserves_partial_writes_without_reading_the_fourth_operand()
    {
        const uint source = 0x10000000;
        const uint selected = 0x20000000;
        List<int> plainReads = [];
        List<int> capturedReads = [];
        NativeBattleStaticMeshContext88Step plain = Draw(null, plainReads);
        NativeBattleStaticMeshContext88Step captured = Draw(TaskIdentity(186, 0), capturedReads);
        Assert.Equal(new[] { 0, 8, 16 }, capturedReads);
        Assert.Equal(plainReads, capturedReads);
        Assert.Equal(NativeBattleTestSnapshots.Json(plain.Memory), NativeBattleTestSnapshots.Json(captured.Memory));
        Assert.Equal(NativeBattleTestSnapshots.Json(plain.Context), NativeBattleTestSnapshots.Json(captured.Context));
        AssertLedger(plain.Batch, captured.Batch);
        Assert.All(captured.Batch.Attempts, attempt => Assert.Null(attempt.Primitive));

        NativeBattleStaticMeshContext88Step Draw(NativeBattlePilotVisualIdentity? pilot, List<int> reads)
        {
            NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 88).Write(0, BitConverter.GetBytes(source)).Write(4, BitConverter.GetBytes(selected)).Write(8, new byte[4]).Write(0x18, new byte[4]).Write(0x1C, BitConverter.GetBytes(0x20F3u));
            return NativeBattleStaticMeshContext88.DrawLoaded(Memory(), StaticMesh(source), new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108), context, NativeBattleGteColorInterpolation.CodeConstants45F270, selectedVerticesAddress: selected, readSelectedVertex: offset =>
            {
                Assert.NotEqual(24, offset);
                reads.Add(offset);
                return (new((short)offset, 0, -2000), null);
            }, pilotIdentity: pilot);
        }
    }

    [Theory]
    [InlineData(186, 0xD1DD5Cu)]
    [InlineData(186, 0xD1DDD0u)]
    [InlineData(186, 0xD1DF7Cu)]
    [InlineData(186, 0xD1E128u)]
    [InlineData(190, 0xD187A0u)]
    [InlineData(190, 0xD1899Cu)]
    [InlineData(190, 0xD18B48u)]
    public void Loaded_sprite_capture_preserves_all_writes_and_uses_true_camera(int effect, uint source)
    {
        NativeBattleCameraProjection projection = new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108);
        NativeBattleModuleResource resource = Sprite(source);
        NativeBattleModuleSpriteDraw plain = Draw(null);
        NativeBattleModuleSpriteDraw captured = Draw(TaskIdentity((ushort)effect, 0) with { ResourceGeneration = source });
        Assert.Equal(NativeBattleTestSnapshots.Json(plain.Memory), NativeBattleTestSnapshots.Json(captured.Memory));
        Assert.Equal(NativeBattleTestSnapshots.Json(plain.Temporary180), NativeBattleTestSnapshots.Json(captured.Temporary180));
        AssertLedger(plain.Batch, captured.Batch);
        NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(Assert.Single(captured.Batch.Attempts.Where(attempt => attempt.Primitive is not null)).Primitive!.PilotGeometry);
        Assert.Equal((ushort)effect, geometry.Identity.Effect);
        Assert.Equal((long)source, geometry.Identity.ResourceGeneration);
        Assert.Equal(2000, geometry.Sprite!.Origin.Z);

        NativeBattleModuleSpriteDraw Draw(NativeBattlePilotVisualIdentity? identity)
        {
            NativeBattleEffectArena temporary = NativeBattleEffectArena.Clear().MarkUnknown(0, 180).Write(0, BitConverter.GetBytes(source)).Write(4, new byte[2]).Write(0x24, new byte[2]);
            return NativeBattleModuleSpriteDrawing.Draw571C80(Memory(), temporary, _ => resource, projection, 0, 17, pilotIdentity: identity);
        }
    }

    [Theory]
    [InlineData(0x1C)]
    [InlineData(0x3CC)]
    [InlineData(0x4B4)]
    [InlineData(0x2C)]
    public void Effect277_ACT_selected_capture_retains_native_packet_and_workspace_domain(int workspace)
    {
        byte[] source = new byte[132];
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(16), 64);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(88), 1);
        Span<byte> face = source.AsSpan(92, 20);
        BinaryPrimitives.WriteUInt32LittleEndian(face, 0x26404040);
        BinaryPrimitives.WriteUInt16LittleEndian(face[6..], 2);
        BinaryPrimitives.WriteUInt16LittleEndian(face[8..], 4);
        byte[] vertices = new byte[24];
        short[] points = [-64, -64, 0, 0, 64, -64, 0, 0, -64, 64, 0, 0];
        for (int index = 0; index < points.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(index * 2), points[index]);
        }
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 };
        NativeBattleCameraProjection projection = new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108);
        NativeBattleGfGeometryContext context = new(0x2030, 0, 0, null, null, 0, 17, 2);
        NativeBattlePilotVisualIdentity identity = TaskIdentity(277, 0x80) with { ResourceGeneration = ((long)0x10000000 << 32) | (uint)workspace };
        NativeBattleGfGeometryBatch plain = NativeBattleGfGeometryPackets.EmitBatch7043B0(source.ToImmutableArray(), 16, vertices.ToImmutableArray(), matrix, projection, context);
        NativeBattleGfGeometryBatch captured = NativeBattleGfGeometryPackets.EmitBatch7043B0(source.ToImmutableArray(), 16, vertices.ToImmutableArray(), matrix, projection, context, pilotIdentity: identity);
        AssertLedger(plain, captured);
        NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(Assert.Single(captured.Attempts.Where(attempt => attempt.Primitive is not null)).Primitive!.PilotGeometry);
        Assert.Equal(identity.ResourceGeneration, geometry.Identity.ResourceGeneration);
        Assert.Equal(new Vector3(-64, -64, 2000), geometry.A);
        Assert.Equal(new Vector3(64, -64, 2000), geometry.B);
        Assert.Equal(new Vector3(-64, 64, 2000), geometry.C);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2048)]
    public void Effect277_direct_beam_capture_keeps_native_fade_and_depth_payload(int fade)
    {
        byte[] source = StaticMesh(0x10000000).Bytes.ToArray();
        short[] vertices = [-64, -64, 0, 0, 64, -64, 0, 0, -64, 64, 0, 0, 64, 64, 0, 0];
        for (int index = 0; index < vertices.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(8 + index * 2), vertices[index]);
        }
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 };
        NativeBattleCameraProjection projection = new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108);
        NativeBattleModuleMeshContext context = new(fade == 0 ? 0x30u : 0xF0u, fade == 0 ? null : 0u, fade == 0 ? null : fade, null, null, null, 17, 2);
        NativeBattleGfGeometryBatch plain = Draw(null);
        NativeBattleGfGeometryBatch captured = Draw(TaskIdentity(277, 0x60000) with { ResourceGeneration = 73 });
        AssertLedger(plain, captured);
        NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(Assert.Single(captured.Attempts.Where(attempt => attempt.Primitive is not null)).Primitive!.PilotGeometry);
        Assert.Equal(new Vector3(-64, -64, 2000), geometry.A);
        Assert.Equal(new Vector3(64, 64, 2000), geometry.D);

        NativeBattleGfGeometryBatch Draw(NativeBattlePilotVisualIdentity? identity)
        {
            return NativeBattleModulePackets.EmitBatch572200(source.ToImmutableArray(), 0, default, matrix, projection, context, NativeBattleGteColorInterpolation.CodeConstants45F270, pilotIdentity: identity);
        }
    }

    [Theory]
    [InlineData(-0x400)]
    [InlineData(-0x300)]
    public void Effect277_glow_and_spark_capture_keep_depth_bias_and_native_allocations(int depth)
    {
        NativeBattleModuleResource resource = Sprite(0x10000000);
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resource, SpriteFrame = resource.ProjectedFrame571C80(0), Phase = 0, Anchor = new(0, 0, 2000, 0), Scale = 0xC00, DepthOffset = depth, SpriteFlags = 0 };
        NativeBattleCameraProjection projection = new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108);
        NativeBattleModulePacketBatch plain = NativeBattleModulePackets.EmitSprite571DC0(draw, projection, 0, 17, 2);
        NativeBattleModulePacketBatch captured = NativeBattleModulePackets.EmitSprite571DC0(draw with { PilotIdentity = TaskIdentity(277, 0) with { ResourceGeneration = resource.Address } }, projection, 0, 17, 2);
        Assert.Equal(plain.AllocationBytes, captured.AllocationBytes);
        Assert.Equal(plain.Primitives.Length, captured.Primitives.Length);
        for (int index = 0; index < plain.Primitives.Length; index++)
        {
            Assert.Equal(plain.Primitives[index].Flags20, captured.Primitives[index].Flags20);
            Assert.Equal(plain.Primitives[index].ByteOffset, captured.Primitives[index].ByteOffset);
            Assert.Equal(plain.Primitives[index].Primitive.Words, captured.Primitives[index].Primitive.Words);
            Assert.Equal(plain.Primitives[index].Primitive.VertexPayload4, captured.Primitives[index].Primitive.VertexPayload4);
            Assert.Equal(plain.Primitives[index].Primitive.Bucket, captured.Primitives[index].Primitive.Bucket);
        }
        Assert.NotNull(Assert.Single(captured.Primitives).Primitive.PilotGeometry);
    }

    private static NativeBattlePilotVisualIdentity TaskIdentity(ushort effect, int primitive)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x10000, 0x20000, 0x40, 2);
        (pool, _) = pool.Append(0x30000);
        return NativeBattlePilotVisualLifetimes.Identity(effect, pool.Slots[pool.Head!.Value], primitive);
    }

    private static NativeBattleEffectMemory Memory()
    {
        return NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
    }

    private static void AssertLedger(NativeBattleGfGeometryBatch plain, NativeBattleGfGeometryBatch captured)
    {
        Assert.Equal(plain.AllocationBytes, captured.AllocationBytes);
        Assert.Equal(plain.NativeUnlinkedAllocation, captured.NativeUnlinkedAllocation);
        Assert.Equal(plain.Attempts.Length, captured.Attempts.Length);
        for (int index = 0; index < plain.Attempts.Length; index++)
        {
            NativeBattleGfGeometryAttempt a = plain.Attempts[index];
            NativeBattleGfGeometryAttempt b = captured.Attempts[index];
            Assert.Equal(a.ByteOffsetRelative, b.ByteOffsetRelative);
            Assert.Equal(a.Data, b.Data);
            Assert.Equal(a.WriteMask, b.WriteMask);
            Assert.Equal(a.Primitive is null, b.Primitive is null);
            if (a.Primitive is { } primitive)
            {
                Assert.Equal(primitive.Bucket, b.Primitive!.Bucket);
                Assert.Equal(primitive.Words, b.Primitive.Words);
                Assert.Equal(primitive.VertexPayload4, b.Primitive.VertexPayload4);
            }
        }
    }

    private static NativeBattleStaticMeshResource StaticMesh(uint address)
    {
        byte[] bytes = new byte[96];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 40);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 4);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(52), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(56), 0x2C404040);
        for (int corner = 0; corner < 4; corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(60 + corner * 2), (ushort)(corner * 2));
        }
        return new(0, address, bytes.ToImmutableArray());
    }

    private static NativeBattleModuleResource Sprite(uint address)
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
        return new(0, address, 0, 0, bytes.ToImmutableArray());
    }
}
