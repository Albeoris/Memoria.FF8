using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect166Controller;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect166LowerTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(5)]
    public void Custom_mesh_capture_preserves_native_loaded_source_and_full_payload(int group)
    {
        NativeBattleGfGeometryBatch Run(bool capture, out string state)
        {
            NativeBattleStaticMeshResource mesh = Mesh(group);
            Assert.Equal((short)-100, BinaryPrimitives.ReadInt16LittleEndian(mesh.Bytes.AsSpan().Slice(8, 2)));
            Assert.Equal((short)-100, BinaryPrimitives.ReadInt16LittleEndian(mesh.Bytes.AsSpan().Slice(10, 2)));
            Assert.Equal((short)0, BinaryPrimitives.ReadInt16LittleEndian(mesh.Bytes.AsSpan().Slice(12, 2)));
            NativeBattlePilotVisualIdentity? identity = capture ? new(166, 1, 2, 3, 4) : null;
            NativeBattleCameraProjection projection = new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108);
            if (group == 2)
            {
                NativeBattleEffect166ModeMesh96Step drawn = NativeBattleEffect166ModeMesh96.DrawLoaded(Memory(), mesh, projection, Context(mesh, 0, 96).Write(0x58, BitConverter.GetBytes(0xE1000020u)).Write(0x5C, BitConverter.GetBytes(0xE1000220u)), NativeBattleGteColorInterpolation.CodeConstants45F270, pilotIdentity: identity);
                state = NativeBattleTestSnapshots.Json(drawn);
                return drawn.Batch;
            }
            NativeBattleEffect166Mesh108Step result = NativeBattleEffect166Mesh108.DrawLoaded(Memory(), mesh, projection, Context(mesh, 0x33, 108), NativeBattleGteColorInterpolation.CodeConstants45F270, pilotIdentity: identity);
            state = NativeBattleTestSnapshots.Json(result);
            return result.Batch;
        }
        NativeBattleGfGeometryBatch native = Run(false, out string baseline);
        string nativeBatch = NativeBattleTestSnapshots.Json(native);
        NativeBattleGfGeometryBatch captured = Run(true, out string state);
        Assert.Equal(baseline, state);
        Assert.Equal(nativeBatch, NativeBattleTestSnapshots.Json(captured));
        NativeBattleGeometryPrimitive primitive = Assert.Single(captured.Attempts).Primitive!;
        NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(primitive.PilotGeometry);
        Assert.Equal(1240u | 824u << 16, primitive.Words[group == 2 ? 3 : 2]);
        Assert.Equal(new System.Numerics.Vector3(-100, -100, 2000), geometry.A);
        Assert.Equal(new System.Numerics.Vector4(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]), geometry.SourceDepths);
        Assert.True(geometry.UseSourceDepths);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Trail_capture_preserves_both_native_surfaces_and_actual_source_depth_FIFO(bool history)
    {
        NativeBattleEffect166Frame Run(bool capture, out NativeBattleGfGeometryBatch output)
        {
            NativeBattleEffect166Frame frame = Frame(history ? 0x5E7AE0u : 0x5E7ED0u);
            NativeBattleModulePoolTask task = Read(frame, 0x23C0550).WriteWord(24, 1).WriteWord(26, 2).WriteWord(12, history ? (ushort)1 : (ushort)0);
            frame = Write(frame, 0x23C0550, task);
            uint path = 0x23C1428;
            frame = frame with { State = frame.State.WriteWord(path + 56, unchecked((ushort)-100)).WriteWord(path + 64, 100) };
            if (history)
            {
                frame = frame with { State = frame.State.WriteDword(path + 440, 1).WriteWord(path + 452, unchecked((ushort)-100)) };
            }
            NativeBattleGfGeometryBatch? batch = null;
            NativeBattleEffect166Inputs inputs = Inputs(0x201) with
            {
                CapturePresentation = capture,
                ReadProjection = _ => new(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, 100, 160, 108),
                Execute = (current, operation) =>
                {
                    batch = Assert.IsType<NativeBattleEffect166DrawBatch>(operation).Batch;
                    return current;
                }
            };
            NativeBattleEffect166Frame result = NativeBattleEffect166Lower.Advance(frame, 0x23C0550, Resources(), inputs).Frame;
            output = Assert.IsType<NativeBattleGfGeometryBatch>(batch);
            return result;
        }
        NativeBattleEffect166Frame native = Run(false, out var nativeBatch);
        string baseline = NativeBattleTestSnapshots.Json(native);
        string packetBaseline = NativeBattleTestSnapshots.Json(nativeBatch);
        NativeBattleEffect166Frame captured = Run(true, out var batch);
        Assert.Equal(baseline, NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(packetBaseline, NativeBattleTestSnapshots.Json(batch));
        Assert.Equal(history ? 28 : 2, batch.Attempts.Length);
        Assert.Equal(batch.Attempts.Length, batch.Attempts.Select(attempt => attempt.Primitive!.PilotGeometry!.Identity).Distinct().Count());
        Assert.All(batch.Attempts, attempt =>
        {
            NativeBattleGeometryPrimitive primitive = attempt.Primitive!;
            NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(primitive.PilotGeometry);
            Assert.Equal(new System.Numerics.Vector4(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]), geometry.SourceDepths);
            Assert.True(geometry.UseSourceDepths);
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Either_physical_path_endpoint_rebirth_breaks_segment_capture_identity(int endpoint)
    {
        NativeBattleEffect166Frame frame = Frame(0x5E7ED0);
        uint path = 0x23C1428;
        NativeBattleModulePoolTask task = Read(frame, 0x23C0550);
        NativeBattlePilotVisualIdentity before = NativeBattleEffect166VisualIdentity.Segment(frame, task, path, 0, 1, 0);
        NativeBattlePilotVisualLifetimes.Allocated(frame.State.Storage, path + 56, endpoint);
        Assert.NotEqual(before, NativeBattleEffect166VisualIdentity.Segment(frame, task, path, 0, 1, 0));
        Assert.Equal(before.Generation, NativeBattleEffect166VisualIdentity.Segment(frame, task, path, 0, 1, 0).Generation);
    }

    [Fact]
    public void Capture_preparation_keeps_unknown_padding_and_uses_native_axis_scale_and_rectangle()
    {
        NativeBattleEffectArena point = NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes((short)-181)).Write(4, BitConverter.GetBytes((short)241));
        NativeBattleEffect166CapturePreparation prepared = NativeBattleEffect166Capture.Prepare(point);
        Assert.Equal(-10, prepared.Matrix32.ReadMatrix().X);
        Assert.Equal(10, prepared.Matrix32.ReadMatrix().Y);
        Assert.Equal(32767, prepared.Matrix32.ReadMatrix().Z);
        Assert.False(prepared.Matrix32.Known[18]);
        Assert.False(prepared.Matrix32.Known[19]);
        Assert.Equal(new short[] { 384, 256, 256, 256 }, Enumerable.Range(0, 4).Select(index => BinaryPrimitives.ReadInt16LittleEndian(prepared.Rectangle8.Read(index * 2, 2))));
        Assert.DoesNotContain(true, prepared.Point8.ReadSnapshot(6, 2).Known);
    }

    [Fact]
    public void Mode_triangle_uses_inline_modes_explicit_SZ_and_does_not_read_unwritten_cue_fields()
    {
        NativeBattleStaticMeshResource mesh = Mesh(2, vertices: 176);
        NativeBattleEffectArena context = Context(mesh, 0, 96).MarkUnknown(12, 12).Write(0x58, BitConverter.GetBytes(0xE1000020u)).Write(0x5C, BitConverter.GetBytes(0xE1000220u));
        NativeBattleEffect166ModeMesh96Step result = NativeBattleEffect166ModeMesh96.DrawLoaded(Memory(), mesh, new(null, 100, 160, 108), context, NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(40, result.Batch.AllocationBytes);
        NativeBattleGeometryPrimitive primitive = Assert.Single(result.Batch.Attempts).Primitive!;
        Assert.Equal(0xE1000020u, primitive.Words[1]);
        Assert.Equal(0xE1000220u, primitive.Words[^1]);
        Assert.Equal(new[] { 2000, 2000, 2000, 0 }, primitive.VertexPayload4);
        Assert.DoesNotContain(true, result.Context96.ReadSnapshot(12, 12).Known);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Mode_zero_or_negative_area_rejects_after_UV_stores_without_allocating(bool zero)
    {
        NativeBattleStaticMeshResource mesh = Mesh(2, degenerate: zero, reverse: !zero);
        NativeBattleEffect166ModeMesh96Step result = NativeBattleEffect166ModeMesh96.DrawLoaded(Memory(), mesh, new(null, 100, 160, 108), Context(mesh, 0, 96), NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(0, result.Batch.AllocationBytes);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[32]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[12]);
    }

    [Theory]
    [InlineData(4, 28)]
    [InlineData(5, 36)]
    public void Custom_Gouraud_tint_uses_each_RGB_byte_and_preserves_code_and_SZ(int group, int length)
    {
        NativeBattleStaticMeshResource mesh = Mesh(group);
        NativeBattleEffectArena context = Context(mesh, 0x33, 108).MarkUnknown(12, 4).Write(0x20, BitConverter.GetBytes(0x0030307Fu));
        NativeBattleEffect166Mesh108Step result = NativeBattleEffect166Mesh108.DrawLoaded(Memory(), mesh, new(null, 100, 160, 108), context, NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(length, result.Batch.AllocationBytes);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Batch.Attempts);
        Assert.Equal((group == 4 ? 0x32000000u : 0x3A000000u) | 0x0030307F, attempt.Primitive!.Words[1]);
        Assert.Equal(4, attempt.Primitive.VertexPayload4.Length);
        Assert.Equal(result.Memory.GeometryRegisters!.Sz.Select(value => unchecked((int)value!.Value)), attempt.Primitive.VertexPayload4);
        Assert.Equal(group == 4 ? new[] { 777, 2000, 2000, 2000 } : new[] { 2000, 2000, 2000, 2000 }, attempt.Primitive.VertexPayload4);
        NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.Resolve(result.Batch, 0, NativeBattleEffectArena.Length, _ => 0);
        NativeBattleEffectPacketWrite delivered = NativeBattleEffectPacketArena.Append(Memory(), 0, NativeBattleLogicalOrdering.Clear(), resolved);
        Assert.Equal(length, delivered.Cursor);
        Assert.DoesNotContain(true, result.Context108.ReadSnapshot(12, 4).Known);
        Assert.Equal(0x0030307Fu, U(result.Context108, 0x58));
    }

    [Fact]
    public void Custom_zero_area_rejects_but_retains_header_and_context_source_colors()
    {
        NativeBattleStaticMeshResource mesh = Mesh(4, degenerate: true);
        NativeBattleEffect166Mesh108Step result = NativeBattleEffect166Mesh108.DrawLoaded(Memory(), mesh, new(null, 100, 160, 108), Context(mesh, 0x33, 108), NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.Null(Assert.Single(result.Batch.Attempts).Primitive);
        Assert.Equal(0x32808080u, U(result.Context108, 0x5C));
        Assert.Equal(0u, U(result.Context108, 0x28));
    }

    [Fact]
    public void Shared_static88_accepts_reached_C3_flat_triangles_with_current_depth_cue()
    {
        NativeBattleStaticMeshResource mesh = Mesh(0);
        NativeBattleEffectArena context = Context(mesh, 0xC3, 88).Write(12, BitConverter.GetBytes(2048));
        NativeBattleStaticMeshContext88Step result = NativeBattleStaticMeshContext88.DrawLoaded(Memory(), mesh, new(null, 100, 160, 108), context, NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(20, result.Batch.AllocationBytes);
        Assert.NotNull(Assert.Single(result.Batch.Attempts).Primitive);
        Assert.Equal(2048u, result.Memory.ScriptedColorRegisters!.Ir0);
        Assert.Equal(0x22404040u, result.Batch.Attempts[0].Primitive!.Words[1]);
    }

    [Fact]
    public void Paused_path_preserves_random_and_does_not_allocate_or_initialize_records()
    {
        NativeBattleEffect166Frame frame = Frame(0x5E5D50);
        NativeBattleEffect166Step step = NativeBattleEffect166Lower.Advance(frame, 0x23C0550, Resources(), Inputs(0x201));
        Assert.Equal(frame.Random, step.Frame.Random);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(frame.State)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(step.Frame.State)));
        Assert.Equal(frame.Staging, step.Frame.Staging);
        Assert.Equal(0, Read(step.Frame, 0x23C0550).ReadWord(12));
    }

    [Fact]
    public void Path_initialization_consumes_eleven_CRT_draws_and_publishes_six_byte_samples_with_unknown_W()
    {
        NativeBattleEffect166Frame frame = Frame(0x5E5D50) with { State = NativeBattleEffect166State.FromUnknown() };
        frame = frame with { State = frame.State.WriteView(0x23C3248, NativeBattleEffectArena.Clear().MarkUnknown(6, 2), 8) };
        NativeBattleEffect166Step step = NativeBattleEffect166Lower.Advance(frame, 0x23C0550, Resources(), Inputs(0));
        NativeCrtRandomState random = frame.Random;
        for (int index = 0; index < 11; index++)
        {
            random = random.Draw(out _);
        }
        Assert.Equal(random, step.Frame.Random);
        Assert.Equal(new uint[] { 0x5E5D50, 0x5E6030, 0x5E7AE0, 0x5E7ED0 }, step.Frame.Effect.Pools[0].Slots.Where(task => task.Callback != 0).Select(task => task.Callback));
        NativeBattleEffectArena samples = step.Frame.State.ReadView(0x23C1460, 48 * 8);
        for (int sample = 0; sample < 48; sample++)
        {
            Assert.All(samples.ReadSnapshot(sample * 8, 6).Known, value => Assert.True(value));
            Assert.DoesNotContain(true, samples.ReadSnapshot(sample * 8 + 6, 2).Known);
        }
        Assert.Equal(1, Read(step.Frame, 0x23C0550).ReadWord(12));
    }

    [Fact]
    public void Paused_history_still_publishes_current_record_before_skipping_motion()
    {
        NativeBattleEffect166Frame frame = Frame(0x5E7AE0);
        NativeBattleEffect166Step step = NativeBattleEffect166Lower.Advance(frame, 0x23C0550, Resources(), Inputs(0x201));
        Assert.Equal(1u, step.Frame.State.ReadDword(0x23C15E0));
        Assert.Equal(0, Read(step.Frame, 0x23C0550).ReadWord(12));
        Assert.Equal(frame.Random, step.Frame.Random);
    }

    [Fact]
    public void Ribbon_uses_two_RTPT_groups_and_delivers_both_G4s_with_current_depth_fifo()
    {
        NativeBattleEffect166Frame frame = Frame(0x5E7ED0);
        frame = Write(frame, 0x23C0550, Read(frame, 0x23C0550).WriteWord(24, 1).WriteWord(26, 2));
        frame = frame with { State = frame.State.WriteWord(0x23C1460, unchecked((ushort)-100)).WriteWord(0x23C1468, 100) };
        NativeBattleGfGeometryBatch? batch = null;
        NativeBattleEffect166Inputs inputs = Inputs(0x201) with
        {
            Execute = (current, operation) =>
            {
                batch = Assert.IsType<NativeBattleEffect166DrawBatch>(operation).Batch;
                return current;
            }
        };
        NativeBattleEffect166Step step = NativeBattleEffect166Lower.Advance(frame, 0x23C0550, Resources(), inputs);
        Assert.NotNull(batch);
        Assert.Equal(72, batch.AllocationBytes);
        Assert.Equal(2, batch.Attempts.Length);
        Assert.All(batch.Attempts, attempt => Assert.Equal(new[] { 2000, 2000, 2000, 2000 }, attempt.Primitive!.VertexPayload4));
        Assert.Equal(batch.Attempts[0].Primitive!.Bucket, batch.Attempts[1].Primitive!.Bucket);
        Assert.Equal(0, Read(step.Frame, 0x23C0550).ReadWord(12));
        Assert.Equal(frame.Random, step.Frame.Random);
    }

    private static NativeBattleEffectMemory Memory(int z = 2000)
    {
        NativeBattleActionMatrix matrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, z, 4096);
        return NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), matrix, true, true, null) with { Sz = [11, 22, 33, 777] }, ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
    }
    private static NativeBattleEffect166Frame Frame(uint callback)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x23C0540, 0x23C0550, 36, 100);
        (pool, _) = pool.Append(callback);
        pool = pool.WriteSlot(0, pool.Slots[0].WriteWord(12, 0).WriteWord(14, 0));
        return new(Memory(), new(166, 165, new(166, 0x5E4E90, 0x5E4E70), null, null) { Pools = [pool] }, new(123), NativeBattleEffect166State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }
    private static NativeBattleEffect166Inputs Inputs(uint flags) => new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => flags, null!, null!, null!, null!, null!, _ => NativeBattlePackedMatrix32.FromMatrix(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 2000, 4096), null), _ => new(null, 100, 160, 108), (frame, _) => frame) { ReadCameraRoll = _ => 0 };
    private static NativeBattleEffect166Resources Resources()
    {
        byte[] axis = new byte[16];
        BinaryPrimitives.WriteInt32LittleEndian(axis.AsSpan(4), -4096);
        return new(null!, null!, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, null!, Enumerable.Repeat(0x00404040u, 12).ToImmutableArray(), [], [], ImmutableDictionary<uint, ImmutableArray<byte>>.Empty.Add(0xD78050, axis.ToImmutableArray()));
    }
    private static NativeBattleEffectArena Context(NativeBattleStaticMeshResource mesh, uint flags, int length) => NativeBattleEffectArena.Clear().MarkUnknown(0, length).Write(0, BitConverter.GetBytes(mesh.Address)).Write(8, new byte[3]).Write(0x1C, BitConverter.GetBytes(flags)).Write(0x20, BitConverter.GetBytes(0x0030307Fu));
    private static uint U(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static NativeBattleStaticMeshResource Mesh(int group, bool degenerate = false, bool reverse = false, int vertices = 4)
    {
        int faceLength = group == 0 ? 12 : group == 5 ? 24 : 20;
        int polygon = 8 + vertices * 8;
        byte[] bytes = new byte[polygon + 32 + faceLength];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, polygon);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), vertices);
        for (int index = 0; index < vertices; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + index * 8), (short)((index & 1) == 0 ? -100 : 100));
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10 + index * 8), (short)(index < 2 ? -100 : 100));
        }
        int face = polygon + 4 * (group + 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(polygon + group * 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face), (group == 0 ? 0x20808080u : group == 2 ? 0x24808080u : group == 4 ? 0x30808080u : 0x38808080u));
        for (int corner = 0; corner < (group == 5 ? 4 : 3); corner++)
        {
            int selected = reverse && corner > 0 ? 3 - corner : corner;
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 4 + corner * 2), degenerate ? (ushort)0 : (ushort)(selected * 2));
        }
        if (group != 0)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 12), group == 2 ? 0x12340203u : 0x00808080u);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 16), group == 2 ? 0x56780405u : 0x00808080u);
        }
        if (group == 5)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 20), 0x00808080);
        }
        return new(0, 0x100000, bytes.ToImmutableArray());
    }
}