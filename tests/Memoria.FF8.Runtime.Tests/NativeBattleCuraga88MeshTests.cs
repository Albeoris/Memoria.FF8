using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCuraga88MeshTests
{
    private const uint Address = 0x1744D34;
    private static NativeBattleActionMatrix Matrix => NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 };
    private static NativeBattleCameraProjection Projection => new(null, 100, 160, 108);
    private static NativeBattleSummonColors Constants => new(0, 0, 1f / 4096f, BitConverter.Int64BitsToDouble(0x4330000080000000L));

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(7)]
    public void All_selected_groups_preserve_Curaga_packet_bytes_and_publish_native_depth(int group)
    {
        ImmutableArray<byte> mesh = Mesh(group);
        NativeBattleCuraga88MeshStep result = Emit(mesh);
        NativeBattleCuragaMeshContext context = new(0xF0, 0xC08040, 2048, null, null, null, 17, 2);
        NativeBattleGfGeometryBatch expected = NativeBattleCuragaMeshPackets.EmitBatch87EEB0(mesh, Matrix, Projection, context, colors: Constants);
        uint depth = group == 4 ? 499u : 500u;
        expected = expected with { Attempts = expected.Attempts.Select(attempt => attempt.Primitive is null ? attempt : attempt with { Primitive = attempt.Primitive with { Bucket = 17 + ((int)depth >> 2) } }).ToImmutableArray() };
        Assert.Equal(SnapshotJson(expected), SnapshotJson(result.Batch));
        Assert.NotNull(Assert.Single(result.Batch.Attempts).Primitive);
        Assert.Equal(depth, result.Registers.Geometry.Otz);
        Assert.Equal(depth, result.Registers.Geometry.Mac0);
        Assert.Equal(depth, Dword(result.Context88, 0x2C));
        Assert.Equal(Address + (uint)mesh.Length, Dword(result.Context88, 0x20));
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, result.Context88.Read(0x34, 8));
    }

    [Theory]
    [InlineData(4, 0x328A6231u, 0x32A47339u, 0x32795128u, 0x32332211u)]
    [InlineData(5, 0xC0A47339u, 0xC0BD8C42u, 0x3A795128u, 0x3A332211u)]
    [InlineData(7, 0xC0A47339u, 0xC0BD8C42u, 0x3E795128u, 0x3E332211u)]
    public void Gouraud_fade_publishes_native_FIFO_code_order_and_full_cue(int group, uint first, uint second, uint third, uint material)
    {
        NativeBattleEffectArena context = Dword(Context(), 0x1C, 0xB2);
        context = Dword(context, 12, 0x12340800);
        NativeBattleCuraga88MeshStep result = Emit(Mesh(group), context);
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
        NativeBattleCuraga88MeshStep plain = Emit(Mesh(group), Dword(Context(), 0x1C, 0x70));
        Assert.Null(plain.Registers.Colors.Material);
        NativeBattleCuraga88MeshStep faded = Emit(Mesh(group), Dword(Context(), 0x1C, 0xB0));
        Assert.NotNull(faded.Registers.Colors.Material);
    }

    [Theory]
    [InlineData(4, 0x70u)]
    [InlineData(5, 0x70u)]
    [InlineData(7, 0x70u)]
    public void Inactive_leaf_fade_bit_does_not_read_unknown_fade_operands(int group, uint flags)
    {
        NativeBattleEffectArena context = NativeBattleCuraga88Mesh.PrepareContext(NativeBattleEffectArena.Clear().MarkUnknown(0, 88), Address, 0, 0, null);
        NativeBattleCuraga88MeshStep result = Emit(Mesh(group), Dword(context, 0x1C, flags));
        Assert.NotNull(Assert.Single(result.Batch.Attempts).Primitive);
        Assert.Null(result.Registers.Colors.Material);
        Assert.Null(result.FarControls.FarRed);
        Assert.Throws<NotSupportedException>(() => result.Context88.Read(12, 4));
    }

    [Theory]
    [InlineData(0x155, 499u)]
    [InlineData(0x100, 375u)]
    [InlineData(-1, 1048574u)]
    public void AVG3_uses_signed_ZSF3_and_logical_shift_without_reading_SZ0(int zsf3, uint expected)
    {
        NativeBattleCuraga88MeshStep result = Emit(Mesh(4), zsf3: (short)zsf3);
        Assert.Null(result.Registers.Geometry.Sz[0]);
        Assert.Equal(expected, result.Registers.Geometry.Otz);
        Assert.Equal(expected, result.Registers.Geometry.Mac0);
        Assert.Equal(17 + ((int)expected >> 2), Assert.Single(result.Batch.Attempts).Primitive!.Bucket);
        Assert.Throws<NotSupportedException>(() => Emit(Mesh(4), zsf3: null));
    }

    [Fact]
    public void Screen_rejected_G3_still_averages_without_publishing_context_depth_or_color()
    {
        NativeBattleCuraga88MeshStep result = Emit(Mesh(4), projection: new(null, 100, -100, -100));
        Assert.Null(Assert.Single(result.Batch.Attempts).Primitive);
        Assert.Equal(499u, result.Registers.Geometry.Otz);
        Assert.Equal(0u, result.Registers.Colors.Flags);
        Assert.Null(result.Registers.Colors.Material);
        Assert.Throws<NotSupportedException>(() => result.Context88.Read(0x2C, 4));
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
        NativeBattleCuraga88MeshStep result = Emit(Mesh(7), readVertexW: ReadW);
        Assert.Equal(new[] { 8, 16, 24, 32 }, reads);
        Assert.False(result.Registers.Colors.NormalInputKnown!.Value[6]);
        Assert.False(result.Registers.Colors.NormalInputKnown.Value[7]);
        reads.Clear();
        NativeBattleEffectArena context = Dword(Context(), 0x1C, 0x80);
        NativeBattleCuraga88MeshStep rejected = Emit(Mesh(7, true), context, zsf3: null, zsf4: null, readVertexW: ReadW);
        Assert.Null(Assert.Single(rejected.Batch.Attempts).Primitive);
        Assert.Equal(new[] { 8, 24, 16 }, reads);
        Assert.Null(rejected.Registers.Geometry.Otz);
        Assert.Null(rejected.Registers.Colors.Material);
    }

    [Fact]
    public void Unfaded_quad_publishes_complete_geometry_and_retains_color_FIFO()
    {
        NativeBattleEffectArena context = NativeBattleCuraga88Mesh.PrepareContext(NativeBattleEffectArena.Clear().MarkUnknown(0, 88), Address, 0, 0, null);
        NativeBattleCuraga88MeshStep result = Emit(Mesh(5), context);
        Assert.Equal(new uint?[] { 2000, 2000, 2000, 2000 }, result.Registers.Geometry.Sz);
        Assert.Equal(new uint?[] { 1440u | (704u << 16), 1120u | (1024u << 16), 1440u | (1024u << 16) }, result.Registers.Geometry.Sxy);
        Assert.Equal(new int?[] { 400, 400, 2000 }, result.Registers.Colors.Mac);
        Assert.Equal(new int?[] { 400, 400, 2000 }, result.Registers.Colors.Ir);
        Assert.Equal(0u, result.Registers.Colors.Ir0);
        Assert.Equal(0u, result.Registers.Colors.Flags);
        Assert.Equal(new uint?[] { null, 0x12345678, 0xABCDEF01 }, result.Registers.Colors.Rgb);
        Assert.Null(result.Registers.Colors.Material);
        Assert.Equal((short)0x4444, BinaryPrimitives.ReadInt16LittleEndian(result.Registers.Colors.NormalInput!.Value.AsSpan().Slice(6)));
        Assert.Equal(102400u, Dword(result.Context88, 0x24));
        Assert.Equal(500u, Dword(result.Context88, 0x2C));
        Assert.Throws<NotSupportedException>(() => result.Context88.Read(8, 4));
        Assert.Null(result.FarControls.FarBlue);
    }

    [Fact]
    public void Restored_full_owner_produces_identical_next_GTE_packet_and_context_without_hydration()
    {
        NativeBattleCuraga88MeshStep first = Emit(Mesh(7));
        NativeBattleCuraga88MeshStep frozen = NativeBattleStorageSnapshots.Capture(first);
        string json = JsonSerializer.Serialize(frozen);
        NativeBattleCuraga88MeshStep restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleCuraga88MeshStep>(json)!);
        NativeBattleCuraga88MeshStep Next(NativeBattleCuraga88MeshStep retained)
        {
            return NativeBattleCuraga88Mesh.EmitBatch(Mesh(4), Address, Matrix, Projection, retained.Context88, retained.Registers, retained.FarControls, 0, 0, 0x100, Constants, 0x4321, 0x155);
        }
        Assert.Equal(SnapshotJson(Next(first)), SnapshotJson(Next(restored)));
        Assert.Equal(0u, Dword(frozen.Context88, 0x18));
        Assert.Equal(0u, Dword(frozen.Context88, 0x30) & 0x60000);
        Assert.NotEqual(0u, Dword(frozen.Context88, 0x24));
        Assert.Throws<NotSupportedException>(() => frozen.Context88.Read(0x28, 4));
        Assert.Throws<NotSupportedException>(() => frozen.Context88.Read(0x40, 24));
    }

    private static NativeBattleCuraga88MeshStep Emit(ImmutableArray<byte> mesh, NativeBattleEffectArena? context = null, NativeBattleCameraProjection? projection = null, short? zsf3 = 0x155, short? zsf4 = 0x100, Func<int, short?>? readVertexW = null)
    {
        NativeBattleScriptedColorRegisters colors = NativeBattleScriptedColorRegisters.Unknown() with { Rgb = [null, 0x12345678, 0xABCDEF01] };
        return NativeBattleCuraga88Mesh.EmitBatch(mesh, Address, Matrix, projection ?? Projection, context ?? Context(), new(NativeBattleGeometryRegisters.Unknown(), colors), new(1, 2, 3, 4, 5), 0, 0, zsf4, Constants, 0x4321, zsf3, readVertexW);
    }

    private static NativeBattleEffectArena Context() => NativeBattleCuraga88Mesh.PrepareContext(NativeBattleEffectArena.Clear().MarkUnknown(0, 88).Write(0x34, [1, 2, 3, 4, 5, 6, 7, 8]), Address, 0, 2048, 0xC08040);
    private static uint Dword(NativeBattleEffectArena context, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(context.Read(offset, 4));
    private static NativeBattleEffectArena Dword(NativeBattleEffectArena context, int offset, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return context.Write(offset, bytes);
    }

    private static ImmutableArray<byte> Mesh(int group, bool reverse = false)
    {
        int stride = group switch { 4 => 20, 7 => 36, _ => 24 };
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
        uint code = group switch { 3 => 0x2C000000u, 4 => 0x30000000u, 5 => 0x38000000u, _ => 0x3C000000u };
        BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(at), code | 0x332211);
        ushort[] offsets = reverse ? [0, 4, 2, 6] : [0, 2, 4, 6];
        for (int index = 0; index < (group == 4 ? 3 : 4); index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(at + 4 + index * 2), offsets[index]);
        }
        if (group is 3 or 7)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(at + 12), 0x12340302);
            BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(at + 16), 0x56780605);
            BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(at + 20), 0x0A090807);
        }
        if (group != 3)
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
    private static string SnapshotJson<T>(T value) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(value));
    private static T RoundTrip<T>(T value) => NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<T>(SnapshotJson(value))!);

}
