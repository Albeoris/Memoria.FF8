using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAdditional181ModelDrawingTests
{
    [Fact]
    public void Raw_skin_uses_current_bone_IR_XYZ_and_explicit_zero_W_without_deformation()
    {
        var result = Draw(Geometry(false), Context(0), 2000);
        Assert.Equal(new short[] { -400, -400, 2000, 0 }, Words(result.VertexScratch.Read(0, 8)));
        Assert.Equal(new short[] { 400, -400, 2000, 0 }, Words(result.VertexScratch.Read(8, 8)));
        Assert.Equal(0x00556677u, BinaryPrimitives.ReadUInt32LittleEndian(result.Temporary128.Read(0x5C, 4)));
        Assert.Null(result.Memory.GeometryRegisters!.Rotation20[18]);
        Assert.All(result.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value, value => Assert.True(value));
        var saturated = Draw(Geometry(false), Context(0), 40000);
        Assert.Equal((short)32767, Words(saturated.VertexScratch.Read(0, 8))[2]);
        Assert.Equal((short)0, Words(saturated.VertexScratch.Read(0, 8))[3]);
    }

    [Fact]
    public void Paired_FT3_F3_and_FT4_F4_use_literal_sizes_screens_UV_and_current_depth_sidecars()
    {
        var result = Draw(Geometry(true), Context(0), 2000);
        Assert.Equal(116, result.Batch.AllocationBytes);
        Assert.Equal(4, result.Batch.Attempts.Length);
        var tri = result.Batch.Attempts[0].Primitive!;
        var triOverlay = result.Batch.Attempts[1].Primitive!;
        var quad = result.Batch.Attempts[2].Primitive!;
        var quadOverlay = result.Batch.Attempts[3].Primitive!;
        Assert.Equal(new int[] { 32, 20, 40, 24 }, result.Batch.Attempts.Select(attempt => attempt.PacketLength));
        Assert.Equal(new int[] { 0, 32, 52, 92 }, result.Batch.Attempts.Select(attempt => attempt.ByteOffsetRelative));
        Assert.Equal(0x26112233u, tri.Words[1]);
        Assert.Equal(0x22556677u, triOverlay.Words[1]);
        Assert.Equal(0x2E112233u, quad.Words[1]);
        Assert.Equal(0x2A556677u, quadOverlay.Words[1]);
        Assert.Equal(0x00DCBA98u, tri.Words[5]);
        Assert.Equal(0xFFFF8123u, tri.Words[7]);
        Assert.Equal(0xF3456789u, quad.Words[7]);
        Assert.Equal(0xFFFFF345u, quad.Words[9]);
        Assert.Equal(new uint[] { tri.Words[2], tri.Words[4], tri.Words[6] }, triOverlay.Words.Skip(2));
        Assert.Equal(new uint[] { quad.Words[2], quad.Words[4], quad.Words[6], quad.Words[8] }, quadOverlay.Words.Skip(2));
        Assert.Equal(new int[] { 2000, 2000, 2000, 0 }, tri.VertexPayload4);
        Assert.Equal(tri.VertexPayload4, triOverlay.VertexPayload4);
        Assert.Equal(new int[] { 2000, 2000, 2000, 2000 }, quad.VertexPayload4);
        Assert.Equal(quad.VertexPayload4, quadOverlay.VertexPayload4);
        Assert.Equal(tri.Bucket - 1, triOverlay.Bucket);
        Assert.Equal(quad.Bucket - 1, quadOverlay.Bucket);
        Assert.All(result.Batch.Attempts, attempt => Assert.All(attempt.WriteMask, state => Assert.Equal(NativeBattleGfPacketByteState.Known, state)));
    }

    [Fact]
    public void Near_negative_and_zero_area_rejection_keep_sparse_base_prefix_without_overlay()
    {
        var near = Draw(Geometry(false), Context(0), 0);
        Assert.Null(Assert.Single(near.Batch.Attempts).Primitive);
        Assert.Equal(0, near.Batch.AllocationBytes);
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(near.Temporary128.Read(0x2C, 4)));
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, near.Batch.Attempts[0].WriteMask[8]);
        byte[] reversed = Geometry(false);
        BinaryPrimitives.WriteUInt16LittleEndian(reversed.AsSpan(54), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(reversed.AsSpan(56), 1);
        var back = Draw(reversed, Context(0), 2000);
        Assert.Null(Assert.Single(back.Batch.Attempts).Primitive);
        Assert.True(unchecked((int)BinaryPrimitives.ReadUInt32LittleEndian(back.Temporary128.Read(0x2C, 4))) < 0);
        byte[] zero = Geometry(false);
        for (int vertex = 0; vertex < 3; vertex++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(zero.AsSpan(16 + vertex * 6), -400);
        }
        var flat = Draw(zero, Context(0), 2000);
        Assert.Null(Assert.Single(flat.Batch.Attempts).Primitive);
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(flat.Temporary128.Read(0x2C, 4)));
    }

    [Fact]
    public void Fourth_near_flag_is_not_admission_and_no_rectangular_offscreen_test_is_added()
    {
        byte[] geometry = Geometry(true);
        BinaryPrimitives.WriteInt16LittleEndian(geometry.AsSpan(36), -1999);
        var fourth = Draw(geometry, Context(0), 2000);
        Assert.Equal(4, fourth.Batch.Attempts.Length);
        Assert.All(fourth.Batch.Attempts, attempt => Assert.NotNull(attempt.Primitive));
        Assert.Equal(new int[] { 2000, 2000, 2000, 1 }, fourth.Batch.Attempts[2].Primitive!.VertexPayload4);
        byte[] outside = Geometry(false);
        for (int vertex = 0; vertex < 4; vertex++)
        {
            short x = BinaryPrimitives.ReadInt16LittleEndian(outside.AsSpan(14 + vertex * 6));
            BinaryPrimitives.WriteInt16LittleEndian(outside.AsSpan(14 + vertex * 6), (short)(x - 10000));
        }
        var accepted = Draw(outside, Context(0), 2000);
        Assert.Equal(2, accepted.Batch.Attempts.Length);
        Assert.All(accepted.Batch.Attempts, attempt => Assert.NotNull(attempt.Primitive));
        Assert.True(unchecked((int)BinaryPrimitives.ReadUInt32LittleEndian(accepted.Temporary128.Read(0x2C, 4))) > 0);
        Assert.All(new[] { 2, 4, 6 }, index => Assert.True(unchecked((short)accepted.Batch.Attempts[0].Primitive!.Words[index]) < 0));
    }

    [Fact]
    public void Zero_depth_overlay_keeps_same_bucket_and_base_before_overlay_attempt_order()
    {
        var memory = Memory() with { ScriptedGraphics = Memory().ScriptedGraphics! with { Zsf3 = 0, Zsf4 = 0 } };
        var result = Draw(Geometry(true), Context(0), 2000, memory: memory);
        Assert.All(result.Batch.Attempts, attempt => Assert.Equal(17, attempt.Primitive!.Bucket));
        Assert.Equal(new uint[] { 0x26, 0x22, 0x2E, 0x2A }, result.Batch.Attempts.Select(attempt => attempt.Primitive!.Words[1] >> 24));
    }

    [Fact]
    public void Unselected_object_is_lazy_and_actual_workspace_bounds_fail_without_fallback()
    {
        var memory = Memory();
        var result = NativeBattleAdditional181ModelDrawing.DrawBody(memory, Context(0).Write(0x20, new byte[4]), NativeBattleEffectArena.Clear(), Read, 0x1000000, [], NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), Projection, memory.ScriptedGraphics!, 0x196B000, 0);
        Assert.Empty(result.Batch.Attempts);
        Assert.Same(memory.GeometryRegisters, result.Memory.GeometryRegisters);
        Assert.Throws<InvalidDataException>(() => Draw(Geometry(false), Context(0), 2000, 24));
        byte[] Read(int offset, int length) => offset == 0 ? BitConverter.GetBytes(1) : offset == 4 ? BitConverter.GetBytes(8) : throw new InvalidOperationException();
    }

    [Fact]
    public void Shared_base_leaf_parameterizes_only_actual_context_offsets()
    {
        var skin = Draw(Geometry(false), Context(0), 2000);
        byte[] face = Geometry(false).AsSpan(52, 16).ToArray();
        var controls = Memory().ScriptedGraphics!;
        NativeBattleGeometryRegisterStep registers = new(NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770(), true, true, null), NativeBattleScriptedColorRegisters.Unknown());
        var first = NativeBattleSkinnedBodyPackets.EmitBaseFace(face, false, skin.VertexScratch, 4, Context(0), registers, Projection, controls, 17, 2, 0, 0x38, 0x2C, 0x34);
        var second = NativeBattleSkinnedBodyPackets.EmitBaseFace(face, false, skin.VertexScratch, 4, Context(0), registers, Projection, controls, 17, 2, 0, 0x3C, 0x30, 0x38);
        Assert.Equal(NativeBattleTestSnapshots.Json(first.Attempt), NativeBattleTestSnapshots.Json(second.Attempt));
        Assert.Equal(NativeBattleTestSnapshots.Json(first.Registers), NativeBattleTestSnapshots.Json(second.Registers));
        Assert.Equal(first.Temporary.Read(0x2C, 4), second.Temporary.Read(0x30, 4));
        Assert.Equal(first.Temporary.Read(0x34, 4), second.Temporary.Read(0x38, 4));
    }

    [Fact]
    public void Current_scratch_context_and_registers_restore_same_next_pair_through_JSON()
    {
        var first = Draw(Geometry(true), Context(0), 2000);
        var restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleAdditional181ModelStep>(NativeBattleTestSnapshots.Json(first))!;
        NativeBattleAdditional181ModelStep Next(NativeBattleAdditional181ModelStep current) => Draw(Geometry(true), current.Temporary128, 2000, memory: current.Memory, scratch: current.VertexScratch);
        Assert.Equal(NativeBattleTestSnapshots.Json(Next(first)), NativeBattleTestSnapshots.Json(Next(restored)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2000)]
    public void Presentation_capture_preserves_native_skin_scratch_registers_packets_and_overlay_order(int z)
    {
        NativeBattleAdditional181ModelStep plain = Draw(Geometry(true), Context(0), z);
        string detachedNative = NativeBattleTestSnapshots.Json(plain);
        NativeBattleAdditional181ModelStep captured = Draw(Geometry(true), Context(0), z, pilotIdentity: new(181, 0x2306550, 1, 12, 0) { ResourceGeneration = 0x1000000 });
        Assert.Equal(detachedNative, NativeBattleTestSnapshots.Json(captured));
        if (z == 0)
        {
            Assert.All(captured.Batch.Attempts, attempt => Assert.Null(attempt.Primitive));
            return;
        }
        NativeBattlePilotVisualGeometry[] geometry = captured.Batch.Attempts.Select(attempt => Assert.IsType<NativeBattlePilotVisualGeometry>(attempt.Primitive!.PilotGeometry)).ToArray();
        Assert.Equal(4, geometry.Select(item => item.Identity).Distinct().Count());
        Assert.Equal(geometry[0].A, geometry[1].A);
        Assert.Equal(geometry[2].D, geometry[3].D);
        Assert.All(geometry, item => Assert.True(item.UseSourceDepths));
        Assert.Equal(new System.Numerics.Vector3(-400, -400, 2000), geometry[0].A);
        Assert.Equal(new System.Numerics.Vector4(2000, 2000, 2000, 0), geometry[0].SourceDepths);
        plain.VertexScratch.Write(0, new byte[32]);
        Assert.Equal(new System.Numerics.Vector3(-400, -400, 2000), geometry[0].A);
    }

    private static NativeBattleCameraProjection Projection => new(null, 100, 160, 108);
    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess() with { Zsf3 = 0x155, Zsf4 = 0x100 } };

    private static NativeBattleAdditional181ModelStep Draw(byte[] geometry, NativeBattleEffectArena context, int boneZ, int length = 32, NativeBattleEffectMemory? memory = null, NativeBattleEffectArena? scratch = null, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        memory ??= Memory();
        return NativeBattleAdditional181ModelDrawing.DrawBody(memory, context, scratch ?? NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xCC, 40).ToArray()), (offset, count) => geometry.AsSpan(offset, count).ToArray(), 0x1000000, [NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = boneZ }, null)], NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), pilotIdentity.HasValue ? Projection with { View = NativeBattleCuragaMatrices.Identity8DD770() } : Projection, memory.ScriptedGraphics!, 0x196B000, length, pilotIdentity: pilotIdentity);
    }

    private static NativeBattleEffectArena Context(int blend)
    {
        NativeBattleEffectArena result = NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 128).ToArray());
        result = result.Write(4, BitConverter.GetBytes(0x196B000u)).Write(0x1C, BitConverter.GetBytes(0x00112233u)).Write(0x20, BitConverter.GetBytes(1u)).Write(0x2C, BitConverter.GetBytes(blend)).Write(0x5C, BitConverter.GetBytes(0x00556677u));
        return result;
    }

    private static byte[] Geometry(bool quad)
    {
        byte[] data = new byte[quad ? 88 : 68];
        BinaryPrimitives.WriteInt32LittleEndian(data, 1);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4), 8);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(8), 1);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(12), 4);
        short[] xy = [-400, -400, 400, -400, -400, 400, 400, 400];
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(14 + index * 6), xy[index * 2]);
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(16 + index * 6), xy[index * 2 + 1]);
        }
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(40), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(42), quad ? (ushort)1 : (ushort)0);
        Face(52, false);
        if (quad)
        {
            Face(68, true);
        }
        return data;

        void Face(int offset, bool four)
        {
            for (int corner = 0; corner < (four ? 4 : 3); corner++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset + corner * 2), (ushort)(0xF000 | corner));
            }
            if (!four)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset + 6), 0x8123);
            }
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset + 8), 0x12345678);
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset + 12), 0xFEDCBA98);
            if (four)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset + 16), 0xF3456789);
            }
        }
    }

    private static short[] Words(byte[] bytes) => Enumerable.Range(0, bytes.Length / 2).Select(index => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(index * 2))).ToArray();
}
