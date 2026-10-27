using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBeamLaser134StaticTests
{
    private const uint Address = 0x1202214;

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    public void F0_actual_quad_groups_publish_packet_color_feedback_and_fourth_projection(int group)
    {
        NativeBattleStaticMeshContext88Step result = Draw(group, Context(0xF0, 0x12340800));
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Batch.Attempts);
        uint material = group == 3 ? 0x2E112233u : 0x3A112233u;
        Assert.Equal(group == 3 ? 40 : 36, result.Batch.AllocationBytes);
        Assert.Equal((material & 0xFF000000u) | 0x081119u, attempt.Primitive!.Words[1]);
        Assert.Equal(material, result.Memory.ScriptedColorRegisters!.Material);
        Assert.Equal(0x12340800u, result.Memory.ScriptedColorRegisters.Ir0);
        Assert.Equal(new int?[] { 408, 272, 136 }, result.Memory.ScriptedColorRegisters.Mac);
        Assert.Equal(new int?[] { 408, 272, 136 }, result.Memory.ScriptedColorRegisters.Ir);
        Assert.Equal(500u, result.Memory.GeometryRegisters!.Otz);
        Assert.Equal(142, attempt.Primitive.Bucket);
        Assert.Equal((short)44, BinaryPrimitives.ReadInt16LittleEndian(result.Memory.ScriptedColorRegisters.NormalInput!.Value.AsSpan(6, 2)));
        Assert.Throws<NotSupportedException>(() => result.Context.Read(11, 1));
        if (group == 3)
        {
            Assert.Equal(0x12345678u, attempt.Primitive.Words[3]);
            Assert.Equal(0x9ABCDEF0u, attempt.Primitive.Words[5]);
            Assert.Equal(0x76544321u, attempt.Primitive.Words[7]);
            Assert.Equal(0x7654u, attempt.Primitive.Words[9]);
        }
        else
        {
            Assert.Equal(0xCC222A33u, attempt.Primitive.Words[3]);
            Assert.Equal(0xCC3B444Cu, attempt.Primitive.Words[5]);
            Assert.Equal(0xCC444C55u, attempt.Primitive.Words[7]);
            Assert.Equal(new uint?[] { 0xCC3B444C, 0xCC444C55, 0x3A081119 }, result.Memory.ScriptedColorRegisters.Rgb);
        }
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    public void Near_reject_preserves_actual_prefixes_without_consuming_unknown_cue_or_FAR(int group)
    {
        NativeBattleEffectArena context = Context(0xF0, 0).MarkUnknown(8, 8);
        NativeBattleStaticMeshContext88Step result = Draw(group, context, z: 0);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.All(attempt.WriteMask.Take(8), state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[8]);
        if (group == 3)
        {
            Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[12]);
            Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[20]);
            Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[28]);
            Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[36]);
        }
        Assert.Null(result.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
        Assert.Null(result.Memory.PrimitiveProcess.ModelRendererRegisters.FarGreen);
        Assert.Null(result.Memory.PrimitiveProcess.ModelRendererRegisters.FarBlue);
        Assert.Null(result.Memory.ScriptedColorRegisters!.Material);
        Assert.Throws<NotSupportedException>(() => result.Context.Read(0x2C, 4));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    public void Unfaded_flags30_publish_individual_unknown_FAR_lanes_without_reading_unconsumed_cue(int group)
    {
        NativeBattleEffectArena context = Context(0x30, 0).MarkUnknown(8, 1).MarkUnknown(10, 6).Write(9, [0x22]);
        NativeBattleEffectMemory memory = Memory();
        NativeBattlePrimitiveProcess primitive = memory.PrimitiveProcess!;
        memory = memory with { PrimitiveProcess = primitive with { ModelRendererRegisters = primitive.ModelRendererRegisters! with { FarRed = 0x999, FarGreen = 0x888, FarBlue = 0x777 } } };
        NativeBattleStaticMeshContext88Step result = Draw(group, context, memory);
        Assert.NotNull(Assert.Single(result.Batch.Attempts).Primitive);
        Assert.Null(result.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
        Assert.Equal(0x220u, result.Memory.PrimitiveProcess.ModelRendererRegisters.FarGreen);
        Assert.Null(result.Memory.PrimitiveProcess.ModelRendererRegisters.FarBlue);
        Assert.Null(result.Memory.ScriptedColorRegisters!.Material);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    public void Accepted_F0_shading_requires_the_unknown_FAR_lane_when_it_is_consumed(int group)
    {
        Assert.Throws<NotSupportedException>(() => Draw(group, Context(0xF0, 2048).MarkUnknown(9, 1)));
    }

    [Fact]
    public void F0_uses_signed_low_cue_WORD_and_retains_full_IR0()
    {
        NativeBattleStaticMeshContext88Step result = Draw(3, Context(0xF0, 0x1357F800));
        Assert.Equal(0x2E19334Cu, Assert.Single(result.Batch.Attempts).Primitive!.Words[1]);
        Assert.Equal(0x1357F800u, result.Memory.ScriptedColorRegisters!.Ir0);
        Assert.Equal(new int?[] { 1224, 816, 408 }, result.Memory.ScriptedColorRegisters.Mac);
    }

    [Fact]
    public void G4_then_FT4_JSON_continuation_preserves_native_feedback_and_partial_context()
    {
        NativeBattleStaticMeshContext88Step first = Draw(5, Context(0xF0, 2048));
        NativeBattleStaticMeshContext88Step restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleStaticMeshContext88Step>(NativeBattleTestSnapshots.Json(first))!;
        NativeBattleStaticMeshContext88Step Next(NativeBattleStaticMeshContext88Step current) => Draw(3, current.Context, current.Memory);
        Assert.Equal(NativeBattleTestSnapshots.Json(Next(first)), NativeBattleTestSnapshots.Json(Next(restored)));
    }

    private static NativeBattleEffectArena Context(uint flags, uint cue) => NativeBattleEffectArena.Clear().MarkUnknown(0, 88).Write(0, BitConverter.GetBytes(Address)).Write(8, [0, 0, 0]).Write(12, BitConverter.GetBytes(cue)).Write(0x1C, BitConverter.GetBytes(flags));
    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() with { Rgb = [0x11010203, 0x22112233, 0x33445566] }, ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
    private static NativeBattleStaticMeshContext88Step Draw(int group, NativeBattleEffectArena context, NativeBattleEffectMemory? memory = null, int z = 2000) => NativeBattleStaticMeshContext88.Draw(memory ?? Memory(), Mesh(group), NativeBattleCuragaMatrices.Identity8DD770() with { Z = z }, new(null, 100, 160, 108), context, NativeBattleGteColorInterpolation.CodeConstants45F270);
    private static NativeBattleStaticMeshResource Mesh(int group)
    {
        byte[] bytes = new byte[96];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 40);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4, 4), 4);
        short[] vertices = [-400, -400, 0, 11, 400, -400, 0, 22, -400, 400, 0, 33, 400, 400, 0, 44];
        for (int index = 0; index < vertices.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + index * 2, 2), vertices[index]);
        }
        int face = 44 + group * 4;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(face - 4, 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face, 4), group == 3 ? 0x2E112233u : 0x3A112233u);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 6, 2), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 8, 2), 4);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 10, 2), 6);
        uint[] payload = group == 3 ? [0x12345678, 0x9ABCDEF0, 0x76544321] : [0xAA445566, 0xBB778899, 0xCC8899AA];
        for (int index = 0; index < payload.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 12 + index * 4, 4), payload[index]);
        }
        return new(0, Address, bytes.ToImmutableArray());
    }
}
