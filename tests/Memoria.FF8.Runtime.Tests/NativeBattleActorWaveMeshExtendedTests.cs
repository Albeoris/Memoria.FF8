using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleActorWaveMeshExtendedTests
{
    private const uint Address = 0x1744D34;
    private static NativeBattleActionMatrix Matrix => NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 };
    private static NativeBattleCameraProjection Projection => new(null, 100, 160, 108);
    private static NativeBattleSummonColors Constants => new(0, 0, 1f / 4096f, BitConverter.Int64BitsToDouble(0x4330000080000000L));

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(7)]
    public void All_selected_groups_preserve_Esuna_packet_bytes_and_publish_native_depth(int group)
    {
        ImmutableArray<byte> mesh = Mesh(group);
        NativeBattleActorWaveMeshStep result = Emit(mesh);
        NativeBattleEsunaMeshContext context = new(0xF0, 0xC08040, 2048, 0, 0, 0, new(0, 0, 256, 256), new(0, 0, 256, 256), 17, 2);
        NativeBattleGfGeometryBatch expected = NativeBattleRestorationMeshPackets.EmitBatch88E240(mesh, Matrix, Projection, context, Constants);
        uint depth = group is 0 or 2 or 4 ? 499u : 500u;
        expected = expected with { Attempts = expected.Attempts.Select(attempt => attempt.Primitive is null ? attempt : attempt with { Primitive = attempt.Primitive with { Bucket = 17 + ((int)depth >> 2) } }).ToImmutableArray() };
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(result.Batch));
        Assert.NotNull(Assert.Single(result.Batch.Attempts).Primitive);
        Assert.Equal(depth, result.Registers.Geometry.Otz);
        Assert.Equal(depth, result.Registers.Geometry.Mac0);
        Assert.Equal(depth, Dword(result.Context104, 0x38));
        Assert.Equal(Address + (uint)mesh.Length, Dword(result.Context104, 0x2C));
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, result.Context104.Read(0x40, 8));
    }

    [Theory]
    [InlineData(4, 0x328A6231u, 0x32A47339u, 0x32795128u, 0x32332211u)]
    [InlineData(5, 0xC0A47339u, 0xC0BD8C42u, 0x3A795128u, 0x3A332211u)]
    [InlineData(7, 0xC0A47339u, 0xC0BD8C42u, 0x3E795128u, 0x3E332211u)]
    public void Gouraud_fade_publishes_native_FIFO_code_order_and_full_cue(int group, uint first, uint second, uint third, uint material)
    {
        NativeBattleEffectArena context = Dword(Context(), 0x14, 0xB2);
        context = Dword(context, 12, 0x12340800);
        NativeBattleActorWaveMeshStep result = Emit(Mesh(group), context);
        Assert.Equal(new uint?[] { first, second, third }, result.Registers.Colors.Rgb);
        Assert.Equal(material, result.Registers.Colors.Material);
        Assert.Equal(0x12340800u, result.Registers.Colors.Ir0);
        Assert.Equal(new int?[] { 648, 1296, 1944 }, result.Registers.Colors.Mac);
        Assert.Equal(new int?[] { 648, 1296, 1944 }, result.Registers.Colors.Ir);
        Assert.Equal(0u, result.Registers.Colors.Flags);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(7)]
    public void Gouraud_flag40_does_not_interpolate_and_flag80_does(int group)
    {
        NativeBattleActorWaveMeshStep plain = Emit(Mesh(group), Dword(Context(), 0x14, 0x70));
        Assert.Null(plain.Registers.Colors.Material);
        NativeBattleActorWaveMeshStep faded = Emit(Mesh(group), Dword(Context(), 0x14, 0xB0));
        Assert.NotNull(faded.Registers.Colors.Material);
    }

    [Theory]
    [InlineData(3, 0xB0u)]
    [InlineData(4, 0x70u)]
    [InlineData(5, 0x70u)]
    [InlineData(7, 0x70u)]
    public void Inactive_leaf_fade_bit_does_not_read_unknown_fade_operands(int group, uint flags)
    {
        NativeBattleEffectArena context = NativeBattleActorWaveMesh.PrepareContext(NativeBattleEffectArena.Clear().MarkUnknown(0, 104), Address, 0, 0, null);
        NativeBattleActorWaveMeshStep result = Emit(Mesh(group), Dword(context, 0x14, flags));
        Assert.NotNull(Assert.Single(result.Batch.Attempts).Primitive);
        Assert.Null(result.Registers.Colors.Material);
        Assert.Null(result.FarControls.FarRed);
        Assert.Throws<NotSupportedException>(() => result.Context104.Read(12, 4));
    }

    [Theory]
    [InlineData(0x155, 499u)]
    [InlineData(0x100, 375u)]
    [InlineData(-1, 1048574u)]
    public void AVG3_uses_signed_ZSF3_and_logical_shift_without_reading_SZ0(int zsf3, uint expected)
    {
        NativeBattleActorWaveMeshStep result = Emit(Mesh(4), zsf3: (short)zsf3);
        Assert.Null(result.Registers.Geometry.Sz[0]);
        Assert.Equal(expected, result.Registers.Geometry.Otz);
        Assert.Equal(expected, result.Registers.Geometry.Mac0);
        Assert.Equal(17 + ((int)expected >> 2), Assert.Single(result.Batch.Attempts).Primitive!.Bucket);
        Assert.Throws<NotSupportedException>(() => Emit(Mesh(4), zsf3: null));
    }

    [Fact]
    public void Screen_rejected_G3_still_averages_without_publishing_context_depth_or_color()
    {
        NativeBattleActorWaveMeshStep result = Emit(Mesh(4), projection: new(null, 100, -100, -100));
        Assert.Null(Assert.Single(result.Batch.Attempts).Primitive);
        Assert.Equal(499u, result.Registers.Geometry.Otz);
        Assert.Equal(0u, result.Registers.Colors.Flags);
        Assert.Null(result.Registers.Colors.Material);
        Assert.Throws<NotSupportedException>(() => result.Context104.Read(0x38, 4));
    }

    [Fact]
    public void Unknown_mutable_vertex_W_is_preserved_and_rejected_quad_never_reads_fourth_W()
    {
        List<int> reads = [];
        short? ReadW(int offset)
        {
            reads.Add(offset);
            return offset == 32 ? null : (short)0x5678;
        }
        NativeBattleActorWaveMeshStep result = Emit(Mesh(7), readVertexW: ReadW);
        Assert.Equal(new[] { 8, 16, 24, 32 }, reads);
        Assert.False(result.Registers.Colors.NormalInputKnown!.Value[6]);
        Assert.False(result.Registers.Colors.NormalInputKnown.Value[7]);
        reads.Clear();
        NativeBattleEffectArena context = Dword(Context(), 0x14, 0x80);
        NativeBattleActorWaveMeshStep rejected = Emit(Mesh(7, true), context, zsf3: null, zsf4: null, readVertexW: ReadW);
        Assert.Null(Assert.Single(rejected.Batch.Attempts).Primitive);
        Assert.Equal(new[] { 8, 24, 16 }, reads);
        Assert.Null(rejected.Registers.Geometry.Otz);
        Assert.Null(rejected.Registers.Colors.Material);
    }

    [Theory]
    [InlineData(0x10u, true)]
    [InlineData(0x20u, false)]
    public void FT3_backface_override_uses_flat_flag10_and_never_loads_fourth(uint flag, bool accepted)
    {
        List<int> reads = [];
        NativeBattleEffectArena context = Dword(Context(), 0x14, flag);
        NativeBattleActorWaveMeshStep result = Emit(Mesh(2, true), context, zsf3: accepted ? (short)0x100 : null, zsf4: null, readVertexW: offset =>
        {
            reads.Add(offset);
            return (short)0x4321;
        });
        Assert.Equal(accepted, Assert.Single(result.Batch.Attempts).Primitive is not null);
        Assert.Equal(new[] { 8, 24, 16 }, reads);
        Assert.Equal(accepted ? (uint?)375u : null, result.Registers.Geometry.Otz);
    }

    private static NativeBattleActorWaveMeshStep Emit(ImmutableArray<byte> mesh, NativeBattleEffectArena? context = null, NativeBattleCameraProjection? projection = null, short? zsf3 = 0x155, short? zsf4 = 0x100, Func<int, short?>? readVertexW = null)
    {
        NativeBattleScriptedColorRegisters colors = NativeBattleScriptedColorRegisters.Unknown() with { Rgb = [null, 0x12345678, 0xABCDEF01] };
        return NativeBattleActorWaveMesh.EmitBatch(mesh, Address, Matrix, projection ?? Projection, context ?? Context(), new(NativeBattleGeometryRegisters.Unknown(), colors), new(1, 2, 3, 4, 5), 0, 0, zsf4, Constants, 0x4321, zsf3, readVertexW);
    }

    [Fact]
    public void F3_fade_publishes_DPCS_FIFO_after_AVSZ3_and_keeps_actual_context_padding()
    {
        NativeBattleActorWaveMeshStep result = Emit(Mesh(0));
        Assert.Equal(new uint?[] { 0x12345678, 0xABCDEF01, 0x20795128 }, result.Registers.Colors.Rgb);
        Assert.Equal(0x20332211u, result.Registers.Colors.Material);
        Assert.Equal(2048u, result.Registers.Colors.Ir0);
        Assert.Equal(new int?[] { 648, 1296, 1944 }, result.Registers.Colors.Mac);
        Assert.Equal(499u, result.Registers.Geometry.Otz);
        Assert.Equal(499u, result.Registers.Geometry.Mac0);
        Assert.Equal(499u, Dword(result.Context104, 0x38));
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, result.Context104.Read(0x40, 8));
        NativeBattleActorWaveMeshStep restored = JsonSerializer.Deserialize<NativeBattleActorWaveMeshStep>(JsonSerializer.Serialize(result))!;
        Assert.Equal(JsonSerializer.Serialize(result.Registers), JsonSerializer.Serialize(restored.Registers));
    }

    [Fact]
    public void F3_screen_rejection_keeps_AVSZ3_but_does_not_publish_accepted_depth_or_DPCS()
    {
        NativeBattleActorWaveMeshStep result = Emit(Mesh(0), projection: Projection with { OffsetX = 4000 });
        Assert.Null(Assert.Single(result.Batch.Attempts).Primitive);
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.Equal(499u, result.Registers.Geometry.Otz);
        Assert.Equal(499u, result.Registers.Geometry.Mac0);
        Assert.Equal(0u, result.Registers.Colors.Flags);
        Assert.All(result.Context104.ReadSnapshot(0x38, 4).Known, Assert.False);
        Assert.Null(result.Registers.Colors.Material);
        Assert.Equal(new uint?[] { null, 0x12345678, 0xABCDEF01 }, result.Registers.Colors.Rgb);
        Assert.All(result.Batch.Attempts[0].WriteMask.Take(8), state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
    }

    [Fact]
    public void Selected_ACT_vertices_keep_the_real_pointer_and_unknown_W_without_rebasing_a_twelve_byte_header()
    {
        ImmutableArray<byte> original = Mesh(0);
        byte[] shape = new byte[original.Length + 4];
        original.AsSpan().Slice(0, 8).CopyTo(shape);
        original.AsSpan().Slice(8).CopyTo(shape.AsSpan(12));
        BinaryPrimitives.WriteUInt32LittleEndian(shape, 44);
        BinaryPrimitives.WriteUInt32LittleEndian(shape.AsSpan(8), 0xDEADBEEF);
        Assert.Equal(new ushort[] { 0, 2, 4 }, Enumerable.Range(0, 3).Select(index => BinaryPrimitives.ReadUInt16LittleEndian(shape.AsSpan(52 + index * 2))));
        NativeBattleEffectArena scratch = NativeBattleEffectArena.Clear().Write(0, original.AsSpan().Slice(8, 32));
        foreach (int offset in new[] { 6, 14, 22, 30 })
        {
            scratch = scratch.MarkUnknown(offset, 2);
        }
        List<int> reads = [];
        NativeBattleEffectArena context = Dword(Context(), 0x14, 0x20F0);
        NativeBattleActorWaveMeshStep result = Draw(scratch);
        Assert.Equal(new[] { 0, 8, 16 }, reads);
        Assert.Equal(0x2704220u, Dword(result.Context104, 4));
        Assert.Equal(Address + (uint)shape.Length, Dword(result.Context104, 0x2C));
        Assert.Equal(JsonSerializer.Serialize(Emit(original).Batch), JsonSerializer.Serialize(result.Batch));
        Assert.Null(result.Registers.Geometry.V1[6]);
        Assert.Null(result.Registers.Geometry.V2[6]);
        NativeBattleEffectArena restored = JsonSerializer.Deserialize<NativeBattleEffectArena>(JsonSerializer.Serialize(scratch))!;
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(Draw(restored)));

        NativeBattleActorWaveMeshStep Draw(NativeBattleEffectArena vertices)
        {
            return NativeBattleActorWaveMesh.EmitBatch(shape.ToImmutableArray(), Address, Matrix, Projection, context, new(NativeBattleGeometryRegisters.Unknown(), NativeBattleScriptedColorRegisters.Unknown() with { Rgb = [null, 0x12345678, 0xABCDEF01] }), new(1, 2, 3, 4, 5), 0, 0, 0x100, Constants, 0x4321, 0x155, selectedVerticesAddress: 0x2704220, readSelectedVertex: offset =>
            {
                reads.Add(offset);
                byte[] position = vertices.Read(offset, 6);
                (byte[] padding, bool[] known) = vertices.ReadSnapshot(offset + 6, 2);
                return (new(BinaryPrimitives.ReadInt16LittleEndian(position), BinaryPrimitives.ReadInt16LittleEndian(position.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(position.AsSpan(4))), known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(padding) : null);
            });
        }
    }

    private static NativeBattleEffectArena Context() => NativeBattleActorWaveMesh.PrepareContext(NativeBattleEffectArena.Clear().MarkUnknown(0, 104).Write(0x40, [1, 2, 3, 4, 5, 6, 7, 8]), Address, 0, 2048, 0xC08040);
    private static uint Dword(NativeBattleEffectArena context, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(context.Read(offset, 4));
    private static NativeBattleEffectArena Dword(NativeBattleEffectArena context, int offset, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return context.Write(offset, bytes);
    }

    private static ImmutableArray<byte> Mesh(int group, bool reverse = false)
    {
        int stride = group switch { 0 => 12, 2 or 4 => 20, 7 => 36, _ => 24 };
        byte[] mesh = new byte[72 + stride];
        BinaryPrimitives.WriteUInt32LittleEndian(mesh, 40);
        BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(4), 4);
        short[] positions = [-400, -400, 0, 0x1111, 400, -400, 0, 0x2222, -400, 400, 0, 0x3333, 400, 400, 0, 0x4444];
        for (int index = 0; index < positions.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(mesh.AsSpan(8 + index * 2), positions[index]);
        }
        int at = 40 + group * 4;
        BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(at), 1);
        at += 4;
        uint code = group switch { 0 => 0x20000000u, 2 => 0x24000000u, 3 => 0x2C000000u, 4 => 0x30000000u, 5 => 0x38000000u, _ => 0x3C000000u };
        BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(at), code | 0x332211);
        ushort[] offsets = reverse ? [0, 4, 2, 6] : [0, 2, 4, 6];
        for (int index = 0; index < (group is 0 or 2 or 4 ? 3 : 4); index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(at + 4 + index * 2), offsets[index]);
        }
        if (group == 2)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(at + 10), 0x0807);
            BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(at + 12), 0x12340302);
            BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(at + 16), 0x56780605);
        }
        if (group is 3 or 7)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(at + 12), 0x12340302);
            BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(at + 16), 0x56780605);
            BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(at + 20), 0x0A090807);
        }
        if (group is 4 or 5 or 7)
        {
            int color = at + (group == 7 ? 24 : 12);
            BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(color), 0xA0554422);
            BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(color + 4), 0xB0886633);
            if (group != 4)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(color + 8), 0xC0BB9944);
            }
        }
        return mesh.ToImmutableArray();
    }
}
