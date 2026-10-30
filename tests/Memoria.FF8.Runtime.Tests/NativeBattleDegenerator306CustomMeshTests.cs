using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDegenerator306CustomMeshTests
{
    [Fact]
    public void Group20_effect174_near_rejection_preserves_sparse_writes_and_defers_bad_fourth()
    {
        byte[] bytes = Source().Bytes.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(76 + 10), ushort.MaxValue);
        var source = Source() with { Bytes = bytes.ToImmutableArray() };
        var view = new NativeBattleCameraProjection(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108);
        var native = Draw(source, Context(), 0, projection: view);
        string baseline = NativeBattleTestSnapshots.Json(native);
        var captured = Draw(source, Context(), 0, new(174, 1, 2, 3, 4), view);
        Assert.Equal(baseline, NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(0, captured.Batch.AllocationBytes);
        Assert.All(captured.Batch.Attempts, attempt => Assert.Null(attempt.Primitive));
        Assert.All(captured.Batch.Attempts, attempt => Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[8]));
    }

    [Fact]
    public void Group20_effect174_optin_keeps_306_default_native_packets_registers_and_partial_writes()
    {
        var view = new NativeBattleCameraProjection(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108);
        var native = Draw(Source(), Context(), 2000, projection: view);
        string baseline = NativeBattleTestSnapshots.Json(native);
        var identity = new NativeBattlePilotVisualIdentity(174, 0x2396000, 1, 123, 7) { ResourceGeneration = 0x1000000 };
        var captured = Draw(Source(), Context(), 2000, pilotIdentity: identity, projection: view);
        Assert.Equal(baseline, NativeBattleTestSnapshots.Json(captured));
        Assert.All(native.Batch.Attempts, attempt => Assert.Null(attempt.Primitive?.PilotGeometry));
        var geometry = captured.Batch.Attempts.Where(attempt => attempt.Primitive?.PilotGeometry is not null).Select(attempt => attempt.Primitive!.PilotGeometry!).ToArray();
        Assert.NotEmpty(geometry);
        Assert.All(geometry, value =>
        {
            Assert.Equal((ushort)174, value.Identity.Effect);
            Assert.Equal(identity.Generation, value.Identity.Generation);
            Assert.Equal(identity.ResourceGeneration, value.Identity.ResourceGeneration);
            Assert.NotEqual(default(System.Numerics.Vector3), value.A);
        });
        Assert.Equal(geometry.Length, geometry.Select(value => value.Identity).Distinct().Count());
    }

    [Fact]
    public void Group20_effect174_skewed_triangle_and_quad_preserve_exact_native_FIFO_depths()
    {
        var matrix = new NativeBattleActionMatrix([4096, 0, 0, 0, 4096, 0, 5, 7, 4096], 0, 0, 2000, 4096);
        var projection = new NativeBattleCameraProjection(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108);
        var native = Draw(Source(), Context(), projection: projection, loadedMatrix: matrix);
        string baseline = NativeBattleTestSnapshots.Json(native);
        var captured = Draw(Source(), Context(), pilotIdentity: new(174, 1, 2, 3, 4), projection: projection, loadedMatrix: matrix);
        Assert.Equal(baseline, NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(new byte[] { 3, 4 }, captured.Batch.Attempts.Select(attempt => attempt.Primitive!.PilotGeometry!.CornerCount));
        foreach (var attempt in captured.Batch.Attempts)
        {
            var primitive = attempt.Primitive!;
            var geometry = primitive.PilotGeometry!;
            Assert.True(geometry.UseSourceDepths);
            Assert.Equal(new System.Numerics.Vector4(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]), geometry.SourceDepths);
            Assert.NotEqual(MathF.Truncate(geometry.A.Z), geometry.A.Z);
        }
    }

    private const uint Address = 0xCE7A2C;

    [Fact]
    public void Actual_uniform_material_overrides_source_color_and_preserves_exact_FT3_FT4_stores()
    {
        NativeBattleDegenerator306CustomMeshStep step = Draw(Source(), Context());
        Assert.Equal(72, step.Batch.AllocationBytes);
        Assert.Equal(new[] { 32, 40 }, step.Batch.Attempts.Select(value => value.PacketLength));
        Assert.All(step.Batch.Attempts, value => Assert.NotNull(value.Primitive));
        Assert.Equal(0x26123456u, U32(step.Batch.Attempts[0].Data, 4));
        Assert.Equal(0x2E123456u, U32(step.Batch.Attempts[1].Data, 4));
        Assert.Equal(0x123401FAu, U32(step.Batch.Attempts[0].Data, 12));
        Assert.Equal(0x00000006u, U32(step.Batch.Attempts[0].Data, 28));
        Assert.Equal(Address + 116, U32(step.Context108, 0x24));
        Assert.Equal(Address + 8, U32(step.Context108, 4));
        Assert.Throws<NotSupportedException>(() => step.Context108.Read(0x38, 4));
    }

    [Fact]
    public void Zero_area_rejection_keeps_prefix_writes_without_advancing_private_cursor()
    {
        NativeBattleStaticMeshResource source = Source();
        byte[] bytes = source.Bytes.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(52 + 8), 0);
        NativeBattleDegenerator306CustomMeshStep step = Draw(source with { Bytes = bytes.ToImmutableArray() }, Context());
        Assert.Null(step.Batch.Attempts[0].Primitive);
        Assert.Equal(40, step.Batch.AllocationBytes);
        Assert.Equal(0, step.Batch.Attempts[1].ByteOffsetRelative);
        Assert.Equal(NativeBattleGfPacketByteState.Known, step.Batch.Attempts[0].WriteMask[12]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, step.Batch.Attempts[0].WriteMask[8]);
    }

    [Fact]
    public void Negative_area_obeys_caller_backface_flag_and_fourth_projection_is_lazy()
    {
        NativeBattleStaticMeshResource source = Source();
        byte[] bytes = source.Bytes.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(52 + 6), 4);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(52 + 8), 2);
        source = source with { Bytes = bytes.ToImmutableArray() };
        Assert.NotNull(Draw(source, Context()).Batch.Attempts[0].Primitive);
        Assert.Null(Draw(source, Context().Write(0x1C, BitConverter.GetBytes(3u))).Batch.Attempts[0].Primitive);
    }

    [Fact]
    public void Near_rejection_retains_unreached_area_depth_and_fade_as_unknown()
    {
        NativeBattleEffectArena context = Context().Write(0x1C, BitConverter.GetBytes(0xFF3u)).MarkUnknown(12, 12).MarkUnknown(0x28, 12);
        NativeBattleDegenerator306CustomMeshStep step = Draw(Source(), context, 0);
        Assert.Equal(0, step.Batch.AllocationBytes);
        Assert.All(step.Batch.Attempts, value => Assert.Null(value.Primitive));
        Assert.Throws<NotSupportedException>(() => step.Context108.Read(0x28, 4));
        Assert.Throws<NotSupportedException>(() => step.Context108.Read(0x30, 4));
        Assert.NotEqual(0u, U32(step.Context108, 0x34) & 0x60000);
    }

    [Fact]
    public void Warm_context_and_GTE_continue_identically_after_JSON_roundtrip()
    {
        NativeBattleDegenerator306CustomMeshStep first = Draw(Source(), Context().Write(0x1C, BitConverter.GetBytes(0xF3u)).Write(12, BitConverter.GetBytes(2048)));
        NativeBattleDegenerator306CustomMeshStep restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleDegenerator306CustomMeshStep>(NativeBattleTestSnapshots.Json(first))!;
        NativeBattleDegenerator306CustomMeshStep Next(NativeBattleDegenerator306CustomMeshStep step) => NativeBattleDegenerator306CustomMesh.Draw5A3AC0(step.Memory, Source(), step.Context108, new(null, 100, 160, 108), NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(NativeBattleTestSnapshots.Json(Next(first)), NativeBattleTestSnapshots.Json(Next(restored)));
        Assert.Equal(2048u, first.Memory.ScriptedColorRegisters!.Ir0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Group21_actual306_custom_capture_preserves_native_materials_sparse_writes_and_depths(bool reject)
    {
        NativeBattleCameraProjection projection = new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108);
        var source = Source();
        if (reject)
        {
            byte[] data = source.Bytes.ToArray();
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(86), 0x7FFF);
            source = source with { Bytes = data.ToImmutableArray() };
        }
        var native = Draw(source, Context(), depth: reject ? 0 : 2000, projection: projection);
        var captured = Draw(source, Context(), depth: reject ? 0 : 2000, pilotIdentity: new(306, 0x2243490, 1, 2, 3), projection: projection);
        captured = captured with { Batch = NativeBattleDegenerator306Presentation.PreserveNativeDepths(captured.Batch) };
        Assert.Equal(NativeBattleTestSnapshots.Json(native), NativeBattleTestSnapshots.Json(captured));
        if (reject)
        {
            Assert.All(captured.Batch.Attempts, attempt => Assert.Null(attempt.Primitive));
        }
        else
        {
            Assert.Equal(2, captured.Batch.Attempts.Count(attempt => attempt.Primitive is not null));
            foreach (var attempt in captured.Batch.Attempts)
            {
                var primitive = Assert.IsType<NativeBattleGeometryPrimitive>(attempt.Primitive);
                var geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(primitive.PilotGeometry);
                Assert.Equal((ushort)306, geometry.Identity.Effect);
                Assert.True(geometry.UseSourceDepths);
                Assert.Equal(new System.Numerics.Vector4(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]), geometry.SourceDepths);
            }
        }
    }

    private static NativeBattleDegenerator306CustomMeshStep Draw(NativeBattleStaticMeshResource source, NativeBattleEffectArena context, int depth = 2000, NativeBattlePilotVisualIdentity? pilotIdentity = null, NativeBattleCameraProjection? projection = null, NativeBattleActionMatrix? loadedMatrix = null)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        memory = memory with
        {
            GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), loadedMatrix ?? (NativeBattleCuragaMatrices.Identity8DD770() with { Z = depth }), true, true, 0),
            ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(),
            ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(),
            PrimitiveProcess = memory.PrimitiveProcess! with { ModelRendererRegisters = new(1, 2, 3, 4, 5) }
        };
        return NativeBattleDegenerator306CustomMesh.Draw5A3AC0(memory, source, context, projection ?? new(null, 100, 160, 108), NativeBattleGteColorInterpolation.CodeConstants45F270, pilotIdentity: pilotIdentity);
    }

    private static NativeBattleEffectArena Context()
    {
        return NativeBattleEffectArena.Clear().MarkUnknown(0, 108).Write(0, BitConverter.GetBytes(Address)).Write(8, BitConverter.GetBytes(0u)).Write(0x1C, BitConverter.GetBytes(0x33u)).Write(0x20, BitConverter.GetBytes(0x00123456u));
    }

    private static NativeBattleStaticMeshResource Source()
    {
        byte[] bytes = new byte[116];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 40);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 4);
        short[] xyz = [-400, -400, 0, 400, -400, 0, -400, 400, 0, 400, 400, 0];
        for (int vertex = 0; vertex < 4; vertex++)
        {
            for (int lane = 0; lane < 3; lane++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + vertex * 8 + lane * 2), xyz[vertex * 3 + lane]);
            }
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(48), 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(72), 1);
        foreach (int at in new[] { 52, 76 })
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at), 0xFFFFFFFF);
            for (int corner = 0; corner < 4; corner++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(at + 4 + corner * 2), (ushort)(corner * 2));
            }
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at + 12), 0x123401FA);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at + 16), 0x567802F0);
            if (at == 76)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at + 20), 0x04FA03FF);
            }
        }
        return new(0, Address, bytes.ToImmutableArray());
    }

    private static uint U32(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static uint U32(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
}
