using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMorph172ModelDrawingTests
{
    [Fact]
    public void Presentation_220_body_and_overlay_keep_actual_post_bone_corners_full_FIFO_and_native_ledger()
    {
        var camera = new NativeBattleActionMatrix([4096, 0, 0, 0, 4096, 0, 5, 7, 4096], 0, 0, 300, 4096);
        var projection = new NativeBattleCameraProjection(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108);
        var native = Draw(Geometry(true), Context(0), 2000, camera: camera, projection: projection);
        string baseline = NativeBattleTestSnapshots.Json(native);
        var captured = Draw(Geometry(true), Context(0), 2000, pilotIdentity: new(172, 1, 2, 3, 4), camera: camera, projection: projection);
        Assert.Equal(baseline, NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(4, captured.Batch.Attempts.Length);
        Assert.Equal(4, captured.Batch.Attempts.Select(attempt => attempt.Primitive!.PilotGeometry!.Identity).Distinct().Count());
        for (int index = 0; index < captured.Batch.Attempts.Length; index += 2)
        {
            var body = captured.Batch.Attempts[index].Primitive!;
            var overlay = captured.Batch.Attempts[index + 1].Primitive!;
            var geometry = body.PilotGeometry!;
            Assert.Equal((ushort)172, geometry.Identity.Effect);
            System.Numerics.Vector3[] source = [new(-400, -400, 2000), new(400, -400, 2000), new(-400, 400, 2000), new(400, 400, 2000)];
            for (int corner = 0; corner < geometry.CornerCount; corner++)
            {
                var point = source[corner];
                Assert.Equal(new System.Numerics.Vector3(point.X, point.Y, 2300 + (5 * point.X + 7 * point.Y) / 4096), geometry.Corner(corner));
            }
            Assert.True(geometry.UseSourceDepths);
            Assert.Equal(new System.Numerics.Vector4(body.VertexPayload4[0], body.VertexPayload4[1], body.VertexPayload4[2], body.VertexPayload4[3]), geometry.SourceDepths);
            Assert.Equal(geometry.A, overlay.PilotGeometry!.A);
            Assert.Equal(geometry.SourceDepths, overlay.PilotGeometry.SourceDepths);
            Assert.Equal(body.VertexPayload4, overlay.VertexPayload4);
            Assert.Equal(body.Bucket - 1, overlay.Bucket);
            Assert.NotEqual(geometry.Identity, overlay.PilotGeometry.Identity);
        }
        Assert.All(native.Batch.Attempts, attempt => Assert.Null(attempt.Primitive?.PilotGeometry));
    }

    [Fact]
    public void Presentation_preserves_native_fourth_scratch_copy_validation_before_rejected_NCLIP()
    {
        byte[] source = Geometry(true);
        BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(70), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(72), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(74), 0xFFFF);
        var nativeContext = Context(0).Write(0xB4, new byte[4]);
        var capturedContext = Context(0).Write(0xB4, new byte[4]);
        var projection = new NativeBattleCameraProjection(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108);
        var native = Assert.Throws<InvalidDataException>(() => Draw(source, nativeContext, 2000, projection: projection));
        var captured = Assert.Throws<InvalidDataException>(() => Draw(source, capturedContext, 2000, pilotIdentity: new(172, 1, 2, 3, 4), projection: projection));
        Assert.Equal(native.Message, captured.Message);
        Assert.Contains("physical skin workspace", captured.Message);
        Assert.Equal(NativeBattleTestSnapshots.Json(nativeContext), NativeBattleTestSnapshots.Json(capturedContext));
    }

    [Fact]
    public void Raw_skin_writes_XYZ_only_and_preserves_physical_scratch_W()
    {
        var actual = Draw(Geometry(false), Context(0), 2000);
        Assert.Equal(new short[] { -400, -400, 2000, unchecked((short)0xCCCC) }, Words(actual.VertexScratch.Read(0, 8)));
        Assert.Throws<InvalidDataException>(() => Draw(Geometry(false), Context(0), 2000, 24));
        Assert.Null(actual.Memory.GeometryRegisters!.Rotation20[18]);
    }

    [Fact]
    public void Base_and_optional_Gouraud_pairs_preserve_full_UV1_depth_sidecars_and_order()
    {
        var actual = Draw(Geometry(true), Context(0), 2000);
        Assert.Equal(136, actual.Batch.AllocationBytes);
        Assert.Equal(new int[] { 32, 28, 40, 36 }, actual.Batch.Attempts.Select(item => item.PacketLength));
        Assert.Equal(new int[] { 0, 32, 60, 100 }, actual.Batch.Attempts.Select(item => item.ByteOffsetRelative));
        Assert.Equal(0x24112233u, actual.Batch.Attempts[0].Primitive!.Words[1]);
        Assert.Equal(0x2C112233u, actual.Batch.Attempts[2].Primitive!.Words[1]);
        for (int index = 0; index < 4; index += 2)
        {
            var body = actual.Batch.Attempts[index].Primitive!;
            var overlay = actual.Batch.Attempts[index + 1].Primitive!;
            Assert.Equal(0xFEDCBA98u, body.Words[5]);
            Assert.Equal(index == 0 ? 0x32u : 0x3Au, overlay.Words[1] >> 24);
            Assert.Equal(body.VertexPayload4, overlay.VertexPayload4);
            Assert.Equal(body.Bucket - 1, overlay.Bucket);
        }
        Assert.Equal((uint)(int)BinaryPrimitives.ReadInt16LittleEndian(actual.Temporary220.Read(0x76, 2)), actual.Memory.ScriptedColorRegisters!.Ir0);
    }

    [Fact]
    public void All_outside_retains_W_and_skips_DPCS_but_still_subtracts_origins()
    {
        var context = Context(0).Write(0xB4, BitConverter.GetBytes(1)).Write(0xA8, BitConverter.GetBytes(100));
        var actual = Draw(Geometry(false), context, 2000);
        Assert.Equal(32, actual.Batch.AllocationBytes);
        Assert.Single(actual.Batch.Attempts);
        Assert.Equal(-500, BinaryPrimitives.ReadInt16LittleEndian(actual.Temporary220.Read(0x58, 2)));
        Assert.Equal(0xCCCC, BinaryPrimitives.ReadUInt16LittleEndian(actual.Temporary220.Read(0x5E, 2)));
        Assert.Null(actual.Memory.ScriptedColorRegisters!.Material);
    }

    [Fact]
    public void Mixed_inside_consumes_actual_outside_W_and_unknown_W_fails_only_when_reached()
    {
        var context = Context(0).Write(0xA8, BitConverter.GetBytes(-400)).Write(0xAC, BitConverter.GetBytes(-400)).Write(0xB0, BitConverter.GetBytes(2000)).Write(0xB4, BitConverter.GetBytes(1));
        var scratch = NativeBattleEffectArena.Clear().Write(0, new byte[40]).Write(14, BitConverter.GetBytes((short)2048)).Write(22, BitConverter.GetBytes((short)1024));
        var actual = Draw(Geometry(false), context, 2000, scratch: scratch);
        Assert.Equal(60, actual.Batch.AllocationBytes);
        Assert.Equal((uint)1024, actual.Memory.ScriptedColorRegisters!.Ir0);
        Assert.Throws<NotSupportedException>(() => Draw(Geometry(false), context, 2000, scratch: scratch.MarkUnknown(14, 2)));
        var outside = context.Write(0xB4, BitConverter.GetBytes(0));
        Assert.Equal(32, Draw(Geometry(false), outside, 2000, scratch: scratch.MarkUnknown(14, 2)).Batch.AllocationBytes);
    }

    [Fact]
    public void Zero_depth_overlay_still_links_one_bucket_before_base()
    {
        var memory = Memory() with { ScriptedGraphics = Memory().ScriptedGraphics! with { Zsf3 = 0, Zsf4 = 0 } };
        var actual = Draw(Geometry(true), Context(0), 2000, memory: memory);
        Assert.Equal(new int[] { 17, 16, 17, 16 }, actual.Batch.Attempts.Select(item => item.Primitive!.Bucket));
    }

    [Fact]
    public void Positive_NCLIP_accepts_near_triple_and_defers_fourth_projection()
    {
        var actual = Draw(Geometry(true), Context(0), 1);
        Assert.Equal(4, actual.Batch.Attempts.Length);
        Assert.All(actual.Batch.Attempts, item => Assert.NotNull(item.Primitive));
        Assert.Equal(new int[] { 1, 1, 1, 1 }, actual.Batch.Attempts[2].Primitive!.VertexPayload4);
    }

    [Fact]
    public void FAR_is_loaded_even_when_object_mask_excludes_everything_and_high_color_BYTE_is_unread()
    {
        var actual = Draw(Geometry(false), Context(0).Write(0x20, new byte[4]).MarkUnknown(0xBB, 1), 2000);
        Assert.Empty(actual.Batch.Attempts);
        Assert.Equal(0x20u << 4, actual.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
        Assert.Equal(0x40u << 4, actual.Memory.PrimitiveProcess.ModelRendererRegisters.FarGreen);
        Assert.Equal(0x80u << 4, actual.Memory.PrimitiveProcess.ModelRendererRegisters.FarBlue);
    }

    [Fact]
    public void JSON_preserves_physical_W_and_registers_for_next_body_step()
    {
        var first = Draw(Geometry(true), Context(0), 2000);
        var restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleMorph172ModelStep>(NativeBattleTestSnapshots.Json(first))!;
        NativeBattleMorph172ModelStep Next(NativeBattleMorph172ModelStep current) => Draw(Geometry(true), current.Temporary220, 2000, memory: current.Memory, scratch: current.VertexScratch);
        Assert.Equal(NativeBattleTestSnapshots.Json(Next(first)), NativeBattleTestSnapshots.Json(Next(restored)));
    }

    private static uint Dword(NativeBattleEffectArena view, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(view.Read(offset, 4));

    private static NativeBattleCameraProjection Projection => new(null, 100, 160, 108);
    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess() with { Zsf3 = 0x155, Zsf4 = 0x100 } };

    private static NativeBattleMorph172ModelStep Draw(byte[] geometry, NativeBattleEffectArena context, int boneZ, int length = 32, NativeBattleEffectMemory? memory = null, NativeBattleEffectArena? scratch = null, NativeBattlePilotVisualIdentity? pilotIdentity = null, NativeBattleActionMatrix? camera = null, NativeBattleCameraProjection? projection = null)
    {
        memory ??= Memory();
        return NativeBattleMorph172ModelDrawing.DrawBody(memory, context, scratch ?? NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xCC, 40).ToArray()), (offset, count) => geometry.AsSpan(offset, count).ToArray(), 0x1000000, [NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = boneZ }, null)], NativeBattlePackedMatrix32.FromMatrix(camera ?? NativeBattleCuragaMatrices.Identity8DD770(), null), projection ?? Projection, memory.ScriptedGraphics!, 0x196B000, length, NativeBattleGteColorInterpolation.CodeConstants45F270, pilotIdentity: pilotIdentity);
    }

    private static NativeBattleEffectArena Context(int blend)
    {
        NativeBattleEffectArena result = NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 220).ToArray());
        result = result.Write(4, BitConverter.GetBytes(0x196B000u)).Write(0x1C, BitConverter.GetBytes(0x00112233u)).Write(0x20, BitConverter.GetBytes(1u)).Write(0xA8, new byte[12]).Write(0xB4, BitConverter.GetBytes(10000)).Write(0xB8, BitConverter.GetBytes(0x00804020u));
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
