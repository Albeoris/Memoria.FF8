using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDegenerator306ModelDrawingTests
{
    [Fact]
    public void Selected_bone_transform_deforms_actual_rows_and_overwrites_W_with_zero()
    {
        NativeBattleDegenerator306ModelStep result = Draw(Geometry(false), Context(2048), 2000);
        Assert.Equal(new short[] { -200, -200, 1000, 0 }, Words(result.VertexScratch.Read(0, 8)));
        Assert.Equal(new short[] { 200, -200, 1000, 0 }, Words(result.VertexScratch.Read(8, 8)));
        Assert.Equal(0xABCDu, (uint)BinaryPrimitives.ReadUInt16LittleEndian(result.Temporary136.Read(0x66, 2)));
        Assert.Equal(0xA5A5A5A5u, BinaryPrimitives.ReadUInt32LittleEndian(result.Temporary136.Read(0x80, 4)));
        Assert.Null(result.Memory.GeometryRegisters!.Rotation20[18]);
        Assert.All(result.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value, value => Assert.True(value));
    }

    [Fact]
    public void Actual_FT3_and_FT4_have_native_commands_UV_DWORD_sign_extension_and_sparse_rejects()
    {
        NativeBattleDegenerator306ModelStep result = Draw(Geometry(true), Context(0), 2000);
        Assert.Equal(72, result.Batch.AllocationBytes);
        Assert.Equal(2, result.Batch.Attempts.Length);
        NativeBattleGfGeometryAttempt tri = result.Batch.Attempts[0];
        NativeBattleGfGeometryAttempt quad = result.Batch.Attempts[1];
        Assert.Equal(0x26112233u, tri.Primitive!.Words[1]);
        Assert.Equal(0x00DCBA98u, tri.Primitive.Words[5]);
        Assert.Equal(0xFFFF8123u, tri.Primitive.Words[7]);
        Assert.Equal(0x2E112233u, quad.Primitive!.Words[1]);
        Assert.Equal(0xF3456789u, quad.Primitive.Words[7]);
        Assert.Equal(0xFFFFF345u, quad.Primitive.Words[9]);
        Assert.All(tri.WriteMask, state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
        Assert.All(quad.WriteMask, state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
    }

    [Fact]
    public void Backface_and_near_rejection_keep_only_reached_prefix_and_context_stores()
    {
        byte[] reversed = Geometry(false);
        BinaryPrimitives.WriteUInt16LittleEndian(reversed.AsSpan(54), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(reversed.AsSpan(56), 1);
        NativeBattleDegenerator306ModelStep back = Draw(reversed, Context(0), 2000);
        NativeBattleGfGeometryAttempt rejected = Assert.Single(back.Batch.Attempts);
        Assert.Null(rejected.Primitive);
        Assert.Equal(0, back.Batch.AllocationBytes);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, rejected.WriteMask[8]);
        Assert.True(unchecked((int)BinaryPrimitives.ReadUInt32LittleEndian(back.Temporary136.Read(0x30, 4))) < 0);
        NativeBattleDegenerator306ModelStep near = Draw(Geometry(false), Context(0), 0);
        Assert.Null(Assert.Single(near.Batch.Attempts).Primitive);
        Assert.Equal(0xA5A5A5A5u, BinaryPrimitives.ReadUInt32LittleEndian(near.Temporary136.Read(0x30, 4)));
    }

    [Fact]
    public void Unselected_object_skips_unknown_payload_and_workspace_bounds_are_actual()
    {
        NativeBattleEffectArena context = Context(0).Write(0x20, BitConverter.GetBytes(0u));
        NativeBattleEffectMemory memory = Memory();
        NativeBattleDegenerator306ModelStep result = NativeBattleDegenerator306ModelDrawing.DrawBody(memory, context, NativeBattleEffectArena.Clear(), Read, 0x1000000, [], NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), Projection, memory.ScriptedGraphics!, 0x196B000, 0);
        Assert.Empty(result.Batch.Attempts);
        Assert.Same(memory.GeometryRegisters, result.Memory.GeometryRegisters);
        Assert.Throws<InvalidDataException>(() => Draw(Geometry(false), Context(0), 2000, 24));
        byte[] Read(int offset, int length) => offset == 0 ? BitConverter.GetBytes(1) : offset == 4 ? BitConverter.GetBytes(8) : throw new InvalidOperationException("Unselected object payload was read.");
    }

    [Fact]
    public void Physical_views_and_canonical_registers_survive_independent_JSON_next()
    {
        NativeBattleDegenerator306ModelStep result = Draw(Geometry(true), Context(-4096), 2000);
        NativeBattleDegenerator306ModelStep restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleDegenerator306ModelStep>(NativeBattleTestSnapshots.Json(result))!;
        NativeBattleDegenerator306ModelStep Next(NativeBattleDegenerator306ModelStep current) => Draw(Geometry(true), current.Temporary136, 2000, 32, current.Memory, current.VertexScratch);
        Assert.Equal(NativeBattleTestSnapshots.Json(Next(result)), NativeBattleTestSnapshots.Json(Next(restored)));
    }

    private static NativeBattleCameraProjection Projection => new(null, 100, 160, 108);
    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess() with { Zsf3 = 0x155, Zsf4 = 0x100 } };

    private static NativeBattleDegenerator306ModelStep Draw(byte[] geometry, NativeBattleEffectArena context, int boneZ, int length = 32, NativeBattleEffectMemory? memory = null, NativeBattleEffectArena? scratch = null)
    {
        memory ??= Memory();
        return NativeBattleDegenerator306ModelDrawing.DrawBody(memory, context, scratch ?? NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xCC, 40).ToArray()), (offset, count) => geometry.AsSpan(offset, count).ToArray(), 0x1000000, [NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = boneZ }, null)], NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), Projection, memory.ScriptedGraphics!, 0x196B000, length);
    }

    private static NativeBattleEffectArena Context(int blend)
    {
        NativeBattleEffectArena result = NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 136).ToArray());
        result = result.Write(4, BitConverter.GetBytes(0x196B000u)).Write(0x1C, BitConverter.GetBytes(0x00112233u)).Write(0x20, BitConverter.GetBytes(1u)).Write(0x2C, BitConverter.GetBytes(blend)).Write(0x60, new byte[6]).Write(0x66, BitConverter.GetBytes((ushort)0xABCD));
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
