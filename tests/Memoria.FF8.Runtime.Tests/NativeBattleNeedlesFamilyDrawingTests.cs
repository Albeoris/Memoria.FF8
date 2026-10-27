using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleNeedlesFamilyDrawingTests
{
    private static readonly NativeBattleCameraProjection Projection = new(null, 100, 160, 108);

    [Fact]
    public void Needle_preparation_uses_three_IR_columns_then_truncated_long_translation_preserving_partial_padding()
    {
        NativeBattleEffectArena context = Context().Write(0x5F, [0xAA]).MarkUnknown(0x5E, 1);
        NativeBattleEffectArena slot = NativeBattleEffectArena.Clear();
        for (int index = 0; index < 9; index++)
        {
            slot = slot.Write(0x18 + index * 2, BitConverter.GetBytes((short)(index % 4 == 0 ? 4096 : 0)));
        }
        slot = slot.Write(0x2C, BitConverter.GetBytes(70000)).Write(0x30, BitConverter.GetBytes(-70000)).Write(0x34, BitConverter.GetBytes(2000));
        NativeBattlePackedMatrix32 camera = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = 1, Y = 2, Z = 3 }, null);
        NativeBattleNeedlesPrepareStep result = NativeBattleNeedlesFamilyDrawing.PrepareNeedle(Memory(), context, slot, camera);
        Assert.Equal(new int?[] { 4465, -4462, 2003 }, result.Memory.GeometryRegisters!.Translation);
        Assert.Equal(new[] { 4465, -4462, 2003 }, Enumerable.Range(0, 3).Select(axis => I32(result.Context220, 0x60 + axis * 4)));
        Assert.Null(result.Memory.GeometryRegisters.Rotation20[18]);
        Assert.Equal((byte?)0xAA, result.Memory.GeometryRegisters.Rotation20[19]);
        Assert.Equal((byte)0, result.Memory.ScriptedColorRegisters!.NormalInput!.Value[6]);
        Assert.Equal((byte)0, result.Memory.ScriptedColorRegisters.NormalInput.Value[7]);
        Assert.Equal(context.Read(0x6C, 112), result.Context220.Read(0x6C, 112));
    }

    [Fact]
    public void Four_G3_faces_emit_28byte_packets_with_full_color_words_and_canonical_AVSZ()
    {
        NativeBattleNeedlesDrawStep result = Draw(Context());
        Assert.Equal(112, result.Batch.AllocationBytes);
        Assert.Equal(4, result.Batch.Attempts.Length);
        Assert.All(result.Batch.Attempts, attempt =>
        {
            Assert.Equal(28, attempt.PacketLength);
            Assert.Equal(0x06000000u, attempt.Primitive!.Words[0]);
            Assert.Equal(0x30112233u, attempt.Primitive.Words[1]);
            Assert.Equal(0xAA445566u, attempt.Primitive.Words[3]);
            Assert.Equal(0xBB778899u, attempt.Primitive.Words[5]);
            Assert.Equal(17 + (int)(result.Memory.GeometryRegisters!.Otz!.Value >> 2), attempt.Primitive.Bucket);
        });
        Assert.Equal(result.Memory.GeometryRegisters!.Otz, unchecked((uint)I32(result.Context220, 8)));
        Assert.True(I32(result.Context220, 0) > 0);
        Assert.Null(result.Memory.ScriptedColorRegisters!.Material);
    }

    [Fact]
    public void Ordering_uses_actual_retained_ZSF3_instead_of_packet_encoders_default_control()
    {
        NativeBattleEffectMemory memory = Memory();
        memory = memory with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(memory.GeometryRegisters!, NativeBattleCuragaMatrices.Identity8DD770(), true, true, null), ScriptedGraphics = memory.ScriptedGraphics! with { Zsf3 = 0x2AA } };
        NativeBattleNeedlesDrawStep result = NativeBattleNeedlesFamilyDrawing.DrawNeedle(memory, Context(), Projection, NativeBattleGteColorInterpolation.CodeConstants45F270);
        uint otz = result.Memory.GeometryRegisters!.Otz!.Value;
        Assert.All(result.Batch.Attempts, attempt => Assert.Equal(17 + (int)(otz >> 2), attempt.Primitive!.Bucket));
        Assert.Equal(otz, unchecked((uint)I32(result.Context220, 8)));
    }

    [Fact]
    public void Near_reject_publishes_only_tag_and_first_color_and_retains_context_clip_and_depth()
    {
        NativeBattleNeedlesDrawStep result = Draw(Context(z: 0));
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.All(result.Batch.Attempts, attempt =>
        {
            Assert.Null(attempt.Primitive);
            Assert.All(attempt.WriteMask.Take(8), state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
            Assert.All(attempt.WriteMask.Skip(8), state => Assert.Equal(NativeBattleGfPacketByteState.Untouched, state));
        });
        Assert.Equal(0x12345678, I32(result.Context220, 0));
        Assert.Equal(0x23456789, I32(result.Context220, 8));
        Assert.NotEqual(0, I32(result.Context220, 12) & 0x60000);
    }

    [Fact]
    public void Offscreen_reject_still_publishes_AVSZ_GTE_but_keeps_context_OTZ_unwritten()
    {
        NativeBattleNeedlesDrawStep result = Draw(Context(x: 4000));
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.All(result.Batch.Attempts, attempt => Assert.Null(attempt.Primitive));
        Assert.True(I32(result.Context220, 0) > 0);
        Assert.NotNull(result.Memory.GeometryRegisters!.Otz);
        Assert.NotEqual(0x23456789u, result.Memory.GeometryRegisters.Otz);
        Assert.Equal(0x23456789, I32(result.Context220, 8));
        Assert.All(result.Batch.Attempts, attempt => Assert.All(new[] { 8, 16, 24 }, offset => Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[offset])));
    }

    [Fact]
    public void Zero_area_reject_keeps_AVSZ_unwritten()
    {
        NativeBattleEffectArena context = Context();
        for (int index = 0; index < 4; index++)
        {
            context = context.Write(0x8C + index * 20 + 8, BitConverter.GetBytes((ushort)2));
        }
        NativeBattleNeedlesDrawStep result = Draw(context);
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.Equal(0, I32(result.Context220, 0));
        Assert.Null(result.Memory.GeometryRegisters!.Otz);
    }

    [Fact]
    public void Face_indices_cannot_cross_the_four_actual_packed_vertices()
    {
        NativeBattleEffectArena context = Context().Write(0x90, BitConverter.GetBytes((ushort)8));
        Assert.Throws<InvalidDataException>(() => Draw(context));
    }

    [Theory]
    [InlineData(100)]
    [InlineData(200)]
    public void Spark_uses_inherited_H_and_separate_signed_focal_shadow_preserving_physical_matrix_unknowns(int focal)
    {
        NativeBattlePackedMatrix32 identity = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null);
        bool[] known = identity.Known.ToArray();
        known[2] = false;
        NativeBattlePackedMatrix32 retained = identity with { Known = known.ToImmutableArray() };
        NativeBattlePackedMatrix32 camera = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = 1, Y = 2, Z = 3 }, null);
        NativeBattleNeedlesSparkStep result = NativeBattleNeedlesFamilyDrawing.PrepareSpark(Memory(), retained, camera, new(400, 0, 2000), 77, 256, 7, -333, Projection with { FocalLength = (ushort)focal });
        Assert.Equal(-326, BinaryPrimitives.ReadInt32LittleEndian(result.Matrix.Bytes.AsSpan(28, 4)));
        Assert.False(result.Matrix.Known[2]);
        Assert.False(result.Matrix.Known[18]);
        Assert.Null(result.Memory.GeometryRegisters!.Rotation20[2]);
        Assert.Equal(new int?[] { 1, 2, 3 }, result.Memory.GeometryRegisters.Translation);
        uint screen = result.Memory.GeometryRegisters.Sxy[2]!.Value;
        Assert.Equal(unchecked((short)screen) / 8 - 160, BinaryPrimitives.ReadInt32LittleEndian(result.Matrix.Bytes.AsSpan(20, 4)));
        Assert.Null(result.Memory.ScriptedColorRegisters!.Material);
        NativeBattleNeedlesSparkStep restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleNeedlesSparkStep>(NativeBattleTestSnapshots.Json(result))!;
        Assert.False(restored.Matrix.Known[2]);
        Assert.Equal(result.Memory.GeometryRegisters.Translation, restored.Memory.GeometryRegisters!.Translation);
    }

    [Fact]
    public void Spark_preserves_unknown_anchor_W_in_retained_vector_without_requiring_it_for_projection()
    {
        NativeBattlePackedMatrix32 matrix = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null);
        NativeBattleNeedlesSparkStep result = NativeBattleNeedlesFamilyDrawing.PrepareSpark(Memory(), matrix, matrix, new(400, 0, 2000), null, 256, 0, 333, Projection);
        Assert.NotNull(result.Memory.GeometryRegisters!.Sxy[2]);
        Assert.False(result.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value[6]);
        Assert.False(result.Memory.ScriptedColorRegisters.NormalInputKnown.Value[7]);
        Assert.All(result.Memory.ScriptedColorRegisters.NormalInputKnown.Value.Take(6), value => Assert.True(value));
        NativeBattleNeedlesSparkStep restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleNeedlesSparkStep>(NativeBattleTestSnapshots.Json(result))!;
        Assert.False(restored.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value[6]);
        Assert.False(restored.Memory.ScriptedColorRegisters.NormalInputKnown.Value[7]);
    }

    private static NativeBattleNeedlesDrawStep Draw(NativeBattleEffectArena context)
    {
        NativeBattleEffectMemory memory = Memory();
        memory = memory with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(memory.GeometryRegisters!, NativeBattleCuragaMatrices.Identity8DD770(), true, true, null) };
        return NativeBattleNeedlesFamilyDrawing.DrawNeedle(memory, context, Projection, NativeBattleGteColorInterpolation.CodeConstants45F270);
    }

    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
    private static int I32(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt32LittleEndian(arena.Read(offset, 4));
    private static NativeBattleEffectArena Context(short z = 2000, short x = -400)
    {
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 220).Write(0, BitConverter.GetBytes(0x12345678)).Write(8, BitConverter.GetBytes(0x23456789));
        short[] vertices = [x, -400, z, 11, unchecked((short)(x + 800)), -400, z, 22, x, 400, z, 33, unchecked((short)(x + 800)), 400, z, 44];
        for (int index = 0; index < vertices.Length; index++)
        {
            context = context.Write(0x6C + index * 2, BitConverter.GetBytes(vertices[index]));
        }
        for (int index = 0; index < 4; index++)
        {
            byte[] face = new byte[20];
            BinaryPrimitives.WriteUInt32LittleEndian(face, 0x30112233);
            BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(6, 2), 2);
            BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(8, 2), 4);
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(12, 4), 0xAA445566);
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(16, 4), 0xBB778899);
            context = context.Write(0x8C + index * 20, face);
        }
        return context;
    }
}
