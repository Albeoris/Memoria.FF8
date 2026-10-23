using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePoisonGas171ModelDrawingTests
{
    [Fact]
    public void Raw_skin_publishes_current_bone_IR_XYZ_and_zero_W_with_actual_bounds()
    {
        var actual = Draw(Geometry(false), Context(0), 2000);
        Assert.Equal(new short[] { -400, -400, 2000, 0 }, Words(actual.VertexScratch.Read(0, 8)));
        Assert.Throws<InvalidDataException>(() => Draw(Geometry(false), Context(0), 2000, 24));
        Assert.Null(actual.Memory.GeometryRegisters!.Rotation20[18]);
        Assert.All(actual.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value, value => Assert.True(value));
        Assert.Equal(0u, Dword(actual.Temporary176, 0x2C));
    }

    [Fact]
    public void Full_base_UV1_ignores_source_semibit_and_textured_compounds_have_exact_layout_and_metadata()
    {
        var actual = Draw(Geometry(true), Context(0), 2000);
        Assert.Equal(160, actual.Batch.AllocationBytes);
        Assert.Equal(new int[] { 32, 40, 40, 48 }, actual.Batch.Attempts.Select(attempt => attempt.PacketLength));
        Assert.Equal(new int[] { 0, 32, 72, 112 }, actual.Batch.Attempts.Select(attempt => attempt.ByteOffsetRelative));
        var tri = actual.Batch.Attempts[0].Primitive!;
        var triOverlay = actual.Batch.Attempts[1].Primitive!;
        var quad = actual.Batch.Attempts[2].Primitive!;
        var quadOverlay = actual.Batch.Attempts[3].Primitive!;
        Assert.Equal(0x24112233u, tri.Words[1]);
        Assert.Equal(0x2C112233u, quad.Words[1]);
        Assert.Equal(0xFEDCBA98u, tri.Words[5]);
        Assert.Equal(0xFEDCBA98u, quad.Words[5]);
        Assert.Equal(0x26707070u, triOverlay.Words[2]);
        Assert.Equal(0x2E707070u, quadOverlay.Words[2]);
        Assert.Equal(0xE2000210u, triOverlay.Words[1]);
        Assert.Equal(0xE2000000u, triOverlay.Words[^1]);
        Assert.Equal(0xE2000000u, quadOverlay.Words[^1]);
        Assert.Equal(0x3F545678u, triOverlay.Words[4]);
        Assert.Equal(0x00B9BA98u, triOverlay.Words[6]);
        Assert.Equal(tri.Words[7], triOverlay.Words[8]);
        Assert.Equal(quad.Words[9], quadOverlay.Words[10]);
        Assert.Equal(new int[] { 2000, 2000, 2000, 0 }, tri.VertexPayload4);
        Assert.Equal(new int[] { 2000, 2000, 2000, 2000 }, quad.VertexPayload4);
        Assert.Equal(new int[] { 0, 0, 0, 0 }, triOverlay.VertexPayload4);
        Assert.Equal(new int[] { 0, 0, 0, 0 }, quadOverlay.VertexPayload4);
        Assert.Equal(tri.Bucket - 1, triOverlay.Bucket);
        Assert.Equal(quad.Bucket - 1, quadOverlay.Bucket);
    }

    [Theory]
    [InlineData(80, 38, 138, 81)]
    [InlineData(-100, 114, 214, 157)]
    public void V_scroll_adjusts_all_three_corners_once_and_publishes_full_scratch(int delta, int a, int b, int c)
    {
        var actual = Draw(Geometry(false), Context(0).Write(0x84, BitConverter.GetBytes(delta)), 2000);
        var overlay = actual.Batch.Attempts[1].Primitive!;
        Assert.Equal((uint)a, Dword(actual.Temporary176, 0x70));
        Assert.Equal((uint)b, Dword(actual.Temporary176, 0x74));
        Assert.Equal((uint)c, Dword(actual.Temporary176, 0x78));
        Assert.Equal(0xA5A5A5A5u, Dword(actual.Temporary176, 0x7C));
        Assert.Equal((byte)a, (byte)(overlay.Words[4] >> 8));
        Assert.Equal((byte)b, (byte)(overlay.Words[6] >> 8));
        Assert.Equal((byte)c, (byte)(overlay.Words[8] >> 8));
        Assert.Equal((byte)0x78, (byte)overlay.Words[4]);
        Assert.Equal((ushort)0x3F54, (ushort)(overlay.Words[4] >> 16));
        Assert.Equal((ushort)0xB9, (ushort)(overlay.Words[6] >> 16));
    }

    [Fact]
    public void Quad_scroll_writes_fourth_lane_but_zero_delta_does_not_consume_or_write_scratch()
    {
        byte[] geometry = Geometry(true);
        var scrolled = Draw(geometry, Context(0).Write(0x84, BitConverter.GetBytes(80)), 2000);
        Assert.Equal(195u, Dword(scrolled.Temporary176, 0x7C));
        Assert.Equal((byte)195, (byte)(scrolled.Batch.Attempts[3].Primitive!.Words[10] >> 8));
        var zero = Draw(geometry, Context(0).MarkUnknown(0x70, 16), 2000);
        Assert.Throws<NotSupportedException>(() => zero.Temporary176.Read(0x70, 16));
        Assert.NotNull(zero.Batch.Attempts[3].Primitive);
        Assert.Throws<NotSupportedException>(() => Draw(geometry, Context(0).MarkUnknown(0x3A, 2), 2000));
    }

    [Fact]
    public void Near_and_zero_area_reject_only_base_prefix_without_overlay_or_scroll_fields()
    {
        var near = Draw(Geometry(false), Context(0).MarkUnknown(0x84, 12), 0);
        Assert.Null(Assert.Single(near.Batch.Attempts).Primitive);
        Assert.Equal(0, near.Batch.AllocationBytes);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, near.Batch.Attempts[0].WriteMask[8]);
        Assert.NotEqual(0u, Dword(near.Temporary176, 0x48) & 0x60000);
        byte[] flat = Geometry(false);
        for (int vertex = 0; vertex < 3; vertex++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(flat.AsSpan(16 + vertex * 6), -400);
        }
        var zero = Draw(flat, Context(0), 2000);
        Assert.Null(Assert.Single(zero.Batch.Attempts).Primitive);
        Assert.Equal(0u, Dword(zero.Temporary176, 0x3C));
    }

    [Fact]
    public void Deferred_fourth_near_still_emits_compound_and_zero_bucket_keeps_native_order()
    {
        byte[] geometry = Geometry(true);
        BinaryPrimitives.WriteInt16LittleEndian(geometry.AsSpan(36), -1999);
        var actual = Draw(geometry, Context(0), 2000);
        Assert.Equal(4, actual.Batch.Attempts.Length);
        Assert.All(actual.Batch.Attempts, attempt => Assert.NotNull(attempt.Primitive));
        Assert.Equal(new int[] { 2000, 2000, 2000, 1 }, actual.Batch.Attempts[2].Primitive!.VertexPayload4);
        var memory = Memory() with { ScriptedGraphics = Memory().ScriptedGraphics! with { Zsf3 = 0, Zsf4 = 0 } };
        var zero = Draw(Geometry(true), Context(0), 2000, memory: memory);
        Assert.All(zero.Batch.Attempts, attempt => Assert.Equal(17, attempt.Primitive!.Bucket));
    }

    [Fact]
    public void Physical_context_scratch_and_canonical_registers_restore_identical_next_step()
    {
        var first = Draw(Geometry(true), Context(0).Write(0x84, BitConverter.GetBytes(80)), 2000);
        var restored = JsonSerializer.Deserialize<NativeBattlePoisonGas171ModelStep>(JsonSerializer.Serialize(first))!;
        NativeBattlePoisonGas171ModelStep Next(NativeBattlePoisonGas171ModelStep current) => Draw(Geometry(true), current.Temporary176, 2000, memory: current.Memory, scratch: current.VertexScratch);
        Assert.Equal(JsonSerializer.Serialize(Next(first)), JsonSerializer.Serialize(Next(restored)));
    }

    private static uint Dword(NativeBattleEffectArena view, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(view.Read(offset, 4));

    private static NativeBattleCameraProjection Projection => new(null, 100, 160, 108);
    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess() with { Zsf3 = 0x155, Zsf4 = 0x100 } };

    private static NativeBattlePoisonGas171ModelStep Draw(byte[] geometry, NativeBattleEffectArena context, int boneZ, int length = 32, NativeBattleEffectMemory? memory = null, NativeBattleEffectArena? scratch = null)
    {
        memory ??= Memory();
        return NativeBattlePoisonGas171ModelDrawing.DrawBody(memory, context, scratch ?? NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xCC, 40).ToArray()), (offset, count) => geometry.AsSpan(offset, count).ToArray(), 0x1000000, [NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = boneZ }, null)], NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), Projection, memory.ScriptedGraphics!, 0x196B000, length);
    }

    private static NativeBattleEffectArena Context(int blend)
    {
        NativeBattleEffectArena result = NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 176).ToArray());
        result = result.Write(4, BitConverter.GetBytes(0x196B000u)).Write(0x1C, BitConverter.GetBytes(0x00112233u)).Write(0x20, BitConverter.GetBytes(1u)).Write(0x2C, BitConverter.GetBytes(blend)).Write(0x3A, BitConverter.GetBytes((short)128)).Write(0x6C, BitConverter.GetBytes(0x00707070u)).Write(0x84, new byte[4]).Write(0x88, BitConverter.GetBytes(0xE2000210u)).Write(0x8C, BitConverter.GetBytes(0xE2000000u));
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
