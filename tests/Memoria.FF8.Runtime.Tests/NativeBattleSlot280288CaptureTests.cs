using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSlot280288CaptureTests
{
    [Theory]
    [InlineData(280, false, false)]
    [InlineData(280, false, true)]
    [InlineData(280, true, false)]
    [InlineData(280, true, true)]
    [InlineData(288, false, false)]
    [InlineData(288, false, true)]
    [InlineData(288, true, false)]
    [InlineData(288, true, true)]
    public void Actual_ACT_leaf_keeps_native_selected_morph_bytes_packets_and_depths(int effect, bool quad, bool morph)
    {
        NativeBattleEffectMemory Memory()
        {
            NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
            foreach (int offset in new[] { 7152, 9024 })
            {
                for (int vertex = 0; vertex < 4; vertex++)
                {
                    arena = arena.Write(offset + vertex * 8 + 6, BitConverter.GetBytes((ushort)0xBEEF));
                }
            }
            return NativeBattleEffectMemory.FromColdProcess() with { Arena = arena };
        }
        NativeBattleGfAnimationSource source = Source(quad);
        NativeBattleGfAnimationNode node = Node(morph);
        NativeBattlePilotVisualIdentity identity = new((ushort)effect, 0x2507C68, 0, 71, 24) { ResourceGeneration = source.Address };
        NativeBattleEffectMemory nativeMemory;
        NativeBattleEffectMemory capturedMemory;
        NativeBattleGfGeometryBatch native;
        NativeBattleGfGeometryBatch captured;
        if (effect == 280)
        {
            NativeBattleFullCure280Draw a = NativeBattleFullCure280Part.Draw(Memory(), source, node, View, NativeBattleCuragaMatrices.Identity8DD770(), default, 0, Projection, Colors);
            NativeBattleFullCure280Draw b = NativeBattleFullCure280Part.Draw(Memory(), source, node, View, NativeBattleCuragaMatrices.Identity8DD770(), default, 0, Projection, Colors, identity);
            nativeMemory = a.Memory;
            capturedMemory = b.Memory;
            native = a.Batch;
            captured = b.Batch;
        }
        else
        {
            NativeBattleRapture288Draw a = NativeBattleRapture288Part.Draw(Memory(), Staging(), source, node, View, 1024, 123, Projection, Colors);
            NativeBattleRapture288Draw b = NativeBattleRapture288Part.Draw(Memory(), Staging(), source, node, View, 1024, 123, Projection, Colors, identity);
            Assert.Equal(Snapshot(a.Staging), Snapshot(b.Staging));
            nativeMemory = a.Memory;
            capturedMemory = b.Memory;
            native = a.Batch;
            captured = b.Batch;
        }
        Assert.Equal(Snapshot(nativeMemory), Snapshot(capturedMemory));
        Assert.Equal(Snapshot(native), Snapshot(captured));
        Assert.All(native.Attempts, attempt => Assert.Null(attempt.Primitive?.PilotGeometry));
        NativeBattleGeometryPrimitive primitive = Assert.Single(captured.Attempts, attempt => attempt.Primitive?.PilotGeometry is not null).Primitive!;
        NativeBattlePilotVisualGeometry geometry = primitive.PilotGeometry!;
        float scale = effect == 280 ? 1 : 0.25f;
        Assert.Equal(new Vector3((morph ? -350 : -400) * scale, -400 * scale, 0), geometry.A);
        Assert.Equal(quad ? 4 : 3, geometry.CornerCount);
        if (quad)
        {
            Assert.Equal(new Vector3((morph ? 450 : 400) * scale, 400 * scale, 0), geometry.D);
        }
        Assert.Equal(new Vector4(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]), geometry.SourceDepths);
        Assert.True(geometry.UseSourceDepths);
        Assert.Equal(1, geometry.ScreenScale);
        if (morph)
        {
            int offset = effect == 280 ? 7152 : 9024;
            Assert.Equal((ushort)0xBEEF, BinaryPrimitives.ReadUInt16LittleEndian(capturedMemory.Arena!.Read(offset + 6, 2)));
        }
        string before = Snapshot(capturedMemory);
        _ = NativeBattlePilotVisualGeometry.Sample(geometry, geometry with { A = geometry.A + new Vector3(10, 20, 30) }, 0.5f);
        Assert.Equal(before, Snapshot(capturedMemory));
    }

    [Theory]
    [InlineData(280, false)]
    [InlineData(280, true)]
    [InlineData(288, false)]
    [InlineData(288, true)]
    public void Rejected_native_quad_does_not_read_invalid_fourth_selected_operand(int effect, bool capture)
    {
        NativeBattleGfAnimationSource source = Source(true, true);
        NativeBattlePilotVisualIdentity? identity = capture ? new((ushort)effect, 1, 2, 3, 4) : null;
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() };
        NativeBattleActionMatrix behind = View with { Z = -1 };
        NativeBattleGfGeometryBatch batch = effect == 280
            ? NativeBattleFullCure280Part.Draw(memory, source, Node(false), behind, NativeBattleCuragaMatrices.Identity8DD770(), default, 0, Projection, Colors, identity).Batch
            : NativeBattleRapture288Part.Draw(memory, Staging(), source, Node(false), behind, 1024, 0, Projection, Colors, identity).Batch;
        Assert.All(batch.Attempts, attempt => Assert.Null(attempt.Primitive));
        Assert.Equal(0, batch.AllocationBytes);
    }

    [Theory]
    [InlineData(280, false)]
    [InlineData(280, true)]
    [InlineData(288, false)]
    [InlineData(288, true)]
    public void Actual_sprite_temporary_retains_native_packet_ledger_flags_FIFO_and_task_birth(int effect, bool extended)
    {
        NativeBattleModuleResource source = Sprite(extended);
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2507C48, 0x20DFAB8 + 8064, 20, 1);
        (pool, int slot) = pool.Append(effect == 280 ? 0x67EF50u : 0x679D90u);
        NativeBattlePilotVisualIdentity identity = NativeBattleSlot280288Presentation.Task((ushort)effect, pool.Slots[slot], source.Address);
        NativeBattleModuleSpriteTemporaryResult Run(bool capture)
        {
            NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = source, SpriteFrame = source.ProjectedFrame571C80(0), Phase = 0, Anchor = new(100, -200, 300, 0), Scale = effect == 280 ? (short)2048 : (short)4096, DepthOffset = effect == 280 ? -512 : -1024, SpriteFlags = 0, PilotIdentity = capture ? identity : null };
            NativeBattleFileStaging staging = NativeBattleModuleSpriteTemporary.Begin571C80(Staging(), NativeBattleTemporaryMemory.Address, source.Address, 0);
            return NativeBattleModuleSpriteTemporary.Emit571C80(staging, NativeBattleTemporaryMemory.Address, draw, Projection, 0, 17, 2);
        }
        NativeBattleModuleSpriteTemporaryResult native = Run(false);
        NativeBattleModuleSpriteTemporaryResult captured = Run(true);
        Assert.Equal(Snapshot(native.Staging), Snapshot(captured.Staging));
        Assert.Equal(Snapshot(native.Batch), Snapshot(captured.Batch));
        NativeBattleModulePacketBatch retained = NativeBattleSlot280288Presentation.Depths((ushort)effect, captured.Batch);
        Assert.Equal(Snapshot(native.Batch), Snapshot(retained));
        NativeBattleAllocatedPrimitive drawPacket = Assert.Single(retained.Primitives, item => item.Primitive.PilotGeometry is not null);
        Assert.Equal((ushort)0, drawPacket.Flags20);
        NativeBattlePilotVisualGeometry geometry = drawPacket.Primitive.PilotGeometry!;
        Assert.NotNull(geometry.Sprite);
        Assert.True(geometry.UseSourceDepths);
        Assert.Equal(new Vector4(drawPacket.Primitive.VertexPayload4[0], drawPacket.Primitive.VertexPayload4[1], drawPacket.Primitive.VertexPayload4[2], drawPacket.Primitive.VertexPayload4[3]), geometry.SourceDepths);
        Assert.Equal(geometry, Assert.Single(retained.CapturedWrites!.Attempts, attempt => attempt.Primitive?.PilotGeometry is not null).Primitive!.PilotGeometry);
        if (extended)
        {
            Assert.Contains(retained.Primitives, item => item.Primitive.Words.Length == 2 && item.Primitive.PilotGeometry is null);
        }
        Assert.Equal(identity, NativeBattleSlot280288Presentation.Task((ushort)effect, pool.Slots[slot], source.Address));
        pool = pool.Advance((current, _) => new(current, 2)).Pool;
        (pool, slot) = pool.Append(effect == 280 ? 0x67EF50u : 0x679D90u);
        NativeBattlePilotVisualIdentity recycled = NativeBattleSlot280288Presentation.Task((ushort)effect, pool.Slots[slot], source.Address);
        Assert.NotEqual(identity.Generation, recycled.Generation);
        Assert.Equal(geometry, NativeBattlePilotVisualGeometry.Sample(geometry, geometry with { Identity = recycled, A = new(999, 999, 999) }, 0.5f));
    }

    [Fact]
    public void Actual_Rapture_debris_retained_native_leaf_keeps_source_corners_and_FIFO()
    {
        NativeBattleStaticMeshResource source = Debris();
        NativeBattleModuleMeshTemporaryResult Run(bool capture)
        {
            uint temporary = NativeBattleTemporaryMemory.Address;
            NativeBattleFileStaging staging = Staging().Write(temporary, BitConverter.GetBytes(source.Address)).Write(temporary + 28, new byte[4]);
            return NativeBattleModulePackets.EmitRetainedBatch572200(staging, temporary, source.Address, source.Bytes, View, Projection, new(0, null, null, null, null, null, 17, 2), Colors, pilotIdentity: capture ? new(288, 1, 2, 3, 4) { ResourceGeneration = source.Address } : null);
        }
        var native = Run(false);
        var captured = Run(true);
        Assert.Equal(Snapshot(native.Staging), Snapshot(captured.Staging));
        Assert.Equal(Snapshot(native.Batch), Snapshot(captured.Batch));
        NativeBattleGeometryPrimitive primitive = Assert.Single(NativeBattleSlot280288Presentation.Depths(288, captured.Batch).Attempts, attempt => attempt.Primitive?.PilotGeometry is not null).Primitive!;
        Assert.Equal(new Vector3(-400, -400, 0), primitive.PilotGeometry!.A);
        Assert.Equal(new Vector4(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]), primitive.PilotGeometry.SourceDepths);
    }

    [Fact]
    public void Actual_task_source_workspace_domains_separate_concurrent_targets_and_both_Rapture_ACTs()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2507C68, 0x20DFAB8, 1056, 2);
        (pool, int first) = pool.Append(0x679510);
        (pool, int second) = pool.Append(0x679510);
        NativeBattlePilotVisualIdentity a = NativeBattleSlot280288Presentation.Task(288, pool.Slots[first], 0x1000000, 24);
        NativeBattlePilotVisualIdentity b = NativeBattleSlot280288Presentation.Task(288, pool.Slots[second], 0x1000000, 24);
        NativeBattlePilotVisualIdentity c = NativeBattleSlot280288Presentation.Task(288, pool.Slots[first], 0x1001000, 276);
        Assert.NotEqual(a, b);
        Assert.NotEqual(a, c);
        Assert.NotEqual(a, NativeBattleSlot280288Presentation.Task(288, pool.Slots[first], 0x1000000, 276));
        Assert.Equal(a, NativeBattleSlot280288Presentation.Task(288, pool.Slots[first], 0x1000000, 24));
    }

    [Theory]
    [InlineData(280)]
    [InlineData(288)]
    public void Family_depth_boundary_keeps_state_foreign_default_and_explicit_receipts_and_rejects_other_arity(int effect)
    {
        NativeBattlePilotVisualGeometry geometry = new(new((ushort)effect, 1, 2, 3, 4), default, default, default, default, 4) { ScreenScale = 3, ScreenOffsets = [new(1, 2), new(3, 4), new(5, 6), new(7, 8)] };
        NativeBattleGeometryPrimitive state = new(8, [0, 0xE1000220], []) { PilotGeometry = geometry };
        NativeBattleGeometryPrimitive foreign = new(8, [0, 0, 0], [1]) { PilotGeometry = geometry with { Identity = geometry.Identity with { Effect = 1 } } };
        NativeBattleGeometryPrimitive explicitDepth = new(8, [0, 0, 0], [1]) { PilotGeometry = geometry with { SourceDepths = new(4, 3, 2, 1), UseSourceDepths = true } };
        foreach (NativeBattleGeometryPrimitive primitive in new[] { state, foreign, explicitDepth, new NativeBattleGeometryPrimitive(8, [0, 0, 0], []) })
        {
            NativeBattleGfGeometryBatch batch = new([new(0, [], [], primitive)], 0);
            Assert.Same(batch, NativeBattleSlot280288Presentation.Depths((ushort)effect, batch));
        }
        NativeBattleGeometryPrimitive empty = new(8, [0, 0, 0], []) { PilotGeometry = geometry };
        NativeBattleGeometryPrimitive retained = Assert.Single(NativeBattleSlot280288Presentation.Depths((ushort)effect, new NativeBattleGfGeometryBatch([new(0, [], [], empty)], 0)).Attempts).Primitive!;
        Assert.Equal(geometry with { SourceDepths = Vector4.Zero, UseSourceDepths = true }, retained.PilotGeometry);
        NativeBattleGeometryPrimitive invalid = empty with { VertexPayload4 = [1, 2, 3] };
        Assert.Throws<InvalidDataException>(() => NativeBattleSlot280288Presentation.Depths((ushort)effect, new NativeBattleGfGeometryBatch([new(0, [], [], invalid)], 0)));
    }

    [Fact]
    public void Full_cure_morph_publishes_its_original_native_XYZ_W_storage_before_returning_the_draw()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(7158, BitConverter.GetBytes((ushort)0xCAFE)) };
        List<short> published = [];
        NativeBattleFullCure280Draw result = NativeBattleFullCure280Part.Draw(memory, Source(true), Node(true), View, NativeBattleCuragaMatrices.Identity8DD770(), default, 0, Projection, Colors, publishMorph: updated =>
        {
            published.Add(BinaryPrimitives.ReadInt16LittleEndian(updated.Arena!.Read(7152, 2)));
            Assert.Equal((ushort)0xCAFE, BinaryPrimitives.ReadUInt16LittleEndian(updated.Arena.Read(7158, 2)));
        });
        Assert.Equal((short)-350, Assert.Single(published));
        Assert.NotEmpty(result.Batch.Attempts);
        NativeBattleFullCure280Part.Draw(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, Source(true), Node(false), View, NativeBattleCuragaMatrices.Identity8DD770(), default, 0, Projection, Colors, publishMorph: _ => throw new InvalidOperationException("Unmorphed native path must not publish a morph write."));
    }

    [Theory]
    [InlineData(280)]
    [InlineData(288)]
    public void Skewed_native_ACT_depth_payload_keeps_four_distinct_original_lanes(int effect)
    {
        NativeBattleActionMatrix skew = new([4096, 0, 0, 0, 4096, 0, 333, 777, 4096], 0, 0, 2000, 4096);
        NativeBattleCameraProjection projection = new(skew, 200, 160, 108);
        NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() };
        NativeBattleGfGeometryBatch Run(bool capture)
        {
            NativeBattlePilotVisualIdentity? identity = capture ? new((ushort)effect, 1, 2, 3, 4) : null;
            return effect == 280
                ? NativeBattleFullCure280Part.Draw(Memory(), Source(true), Node(false), skew, NativeBattleCuragaMatrices.Identity8DD770(), default, 0, projection, Colors, identity).Batch
                : NativeBattleRapture288Part.Draw(Memory(), Staging(), Source(true), Node(false), skew, 1024, -37, projection, Colors, identity).Batch;
        }
        NativeBattleGfGeometryBatch native = Run(false);
        NativeBattleGfGeometryBatch captured = Run(true);
        Assert.Equal(Snapshot(native), Snapshot(captured));
        NativeBattleGeometryPrimitive primitive = Assert.Single(captured.Attempts, attempt => attempt.Primitive?.PilotGeometry is not null).Primitive!;
        Assert.Equal(4, primitive.VertexPayload4.Distinct().Count());
        Assert.True(primitive.PilotGeometry!.UseSourceDepths);
        Assert.Equal(new Vector4(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]), primitive.PilotGeometry.SourceDepths);
    }

    private static NativeBattleActionMatrix View => NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 };
    private static NativeBattleCameraProjection Projection => new(View, 200, 160, 108);
    private static NativeBattleSummonColors Colors => NativeBattleGteColorInterpolation.CodeConstants45F270;
    private static NativeBattleFileStaging Staging() => NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown().Write(NativeBattleTemporaryMemory.Address, new byte[512]));
    private static string Snapshot<T>(T value) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(value));

    private static NativeBattleGfAnimationNode Node(bool morph)
    {
        byte[] bytes = new byte[44];
        for (int axis = 0; axis < 3; axis++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(24 + axis * 2), 4096);
        }
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(38), (short)(morph ? 2048 : 0));
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(42), (short)(morph ? 1 : 0));
        return new(44, 0, bytes.ToImmutableArray());
    }

    private static NativeBattleGfAnimationSource Source(bool quad, bool invalidFourth = false)
    {
        int directory = 12 + 4 * 2 * 8;
        int group = quad ? 3 : 2;
        byte[] bytes = new byte[16 + directory + 32 + (quad ? 24 : 20)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 16);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), directory);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), 4);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24), 2);
        short[] xy = [-400, -400, 400, -400, -400, 400, 400, 400];
        for (int shape = 0; shape < 2; shape++)
        {
            for (int vertex = 0; vertex < 4; vertex++)
            {
                int at = 28 + (shape * 4 + vertex) * 8;
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(at), (short)(xy[vertex * 2] + shape * 100));
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(at + 2), xy[vertex * 2 + 1]);
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(at + 6), 1234);
            }
        }
        int face = 16 + directory + group * 4;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(face), 1);
        face += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face), quad ? 0x2C332211u : 0x24332211u);
        for (int corner = 0; corner < (quad ? 4 : 3); corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 4 + corner * 2), invalidFourth && corner == 3 ? ushort.MaxValue : (ushort)(corner * 2));
        }
        return new(0x1000000, bytes.ToImmutableArray());
    }

    private static NativeBattleModuleResource Sprite(bool extended)
    {
        byte[] bytes = new byte[38];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), 14);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12), -1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(14), extended ? unchecked((int)0x80000001) : 1);
        bytes[18] = 8;
        bytes[19] = 128;
        bytes[20] = 4;
        bytes[21] = 0x2E;
        return new(0, 0xD70000, 0, 0, bytes.ToImmutableArray());
    }

    private static NativeBattleStaticMeshResource Debris()
    {
        NativeBattleGfAnimationSource act = Source(true);
        byte[] bytes = new byte[96];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 40);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 4);
        act.Data.AsSpan(28, 32).CopyTo(bytes.AsSpan(8));
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(52), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(56), 0x2C332211u);
        for (int corner = 0; corner < 4; corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(60 + corner * 2), (ushort)(corner * 2));
        }
        return new(0, 0x1002000, bytes.ToImmutableArray());
    }
}
