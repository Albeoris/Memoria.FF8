using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWindBlast29MeshTests
{
    private const uint Address = 0x1700000;
    private static NativeBattleActionMatrix Matrix => NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 };
    private static NativeBattleCameraProjection Projection => new(null, 100, 160, 108);
    private static NativeBattleSummonColors Constants => new(0, 0, 1f / 4096f, BitConverter.Int64BitsToDouble(0x4330000080000000L));

    [Fact]
    public void FT4_matches_existing_packet_leaf_and_publishes_its_88_byte_context_without_104_byte_fields()
    {
        ImmutableArray<byte> mesh = Mesh();
        NativeBattleCuraga88MeshStep result = Emit(mesh);
        NativeBattleGfGeometryBatch expected = NativeBattleCuragaMeshPackets.EmitBatch87EEB0(mesh, Matrix, Projection, new(0xF0, 0xC08040, 2048, null, null, null, 17, 2), colors: Constants);
        Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(result.Batch));
        Assert.NotNull(Assert.Single(result.Batch.Attempts).Primitive);
        Assert.Equal(40, result.Batch.AllocationBytes);
        Assert.Equal(Address + 8, Dword(result.Context88, 4));
        Assert.Equal(Address + 96, Dword(result.Context88, 0x20));
        Assert.Equal(0u, Dword(result.Context88, 0x18));
        Assert.Equal(102400u, Dword(result.Context88, 0x24));
        Assert.Equal(500u, Dword(result.Context88, 0x2C));
        Assert.Equal(0u, Dword(result.Context88, 0x30) & 0x60000);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, result.Context88.Read(0x34, 4));
        Assert.Throws<NotSupportedException>(() => result.Context88.Read(0x28, 4));
        Assert.Throws<NotSupportedException>(() => result.Context88.Read(0x40, 24));
    }

    [Fact]
    public void Flat_fade_uses_flag40_modified_material_and_full_cue_instead_of_Gouraud_flag80()
    {
        NativeBattleEffectArena context = Write(Context(), 0x1C, 0x71);
        context = Write(context, 12, 0x12340800);
        NativeBattleCuraga88MeshStep result = Emit(Mesh(), context);
        Assert.Equal(0x2E332211u, result.Registers.Colors.Material);
        Assert.Equal(0x12340800u, result.Registers.Colors.Ir0);
        Assert.Equal(new uint?[] { 0x12345678, 0xABCDEF01, 0x2E795128 }, result.Registers.Colors.Rgb);
        Assert.Equal(new int?[] { 648, 1296, 1944 }, result.Registers.Colors.Mac);
        Assert.Equal(new int?[] { 648, 1296, 1944 }, result.Registers.Colors.Ir);
        Assert.Equal(0u, result.Registers.Colors.Flags);
        NativeBattleEffectArena unfaded = NativeBattleCuraga88Mesh.PrepareContext(NativeBattleEffectArena.Clear().MarkUnknown(0, 88), Address, 0, 0, null);
        NativeBattleCuraga88MeshStep retained = Emit(Mesh(), Write(unfaded, 0x1C, 0xB0));
        Assert.Null(retained.Registers.Colors.Material);
        Assert.Equal(new uint?[] { null, 0x12345678, 0xABCDEF01 }, retained.Registers.Colors.Rgb);
        Assert.Throws<NotSupportedException>(() => retained.Context88.Read(12, 4));
    }

    [Fact]
    public void Unfaded_FT4_retains_FIFO_and_publishes_fourth_vertex_full_geometry_padding_and_unknown_W()
    {
        NativeBattleEffectArena context = NativeBattleCuraga88Mesh.PrepareContext(NativeBattleEffectArena.Clear().MarkUnknown(0, 88), Address, 0, 0, null);
        List<int> reads = [];
        short? ReadW(int offset)
        {
            reads.Add(offset);
            return offset == 32 ? null : (short)0x5678;
        }
        NativeBattleCuraga88MeshStep result = Emit(Mesh(), context, readVertexW: ReadW);
        Assert.Equal(new[] { 8, 16, 24, 32 }, reads);
        Assert.Equal(new uint?[] { 2000, 2000, 2000, 2000 }, result.Registers.Geometry.Sz);
        Assert.Equal(new uint?[] { 1440u | (704u << 16), 1120u | (1024u << 16), 1440u | (1024u << 16) }, result.Registers.Geometry.Sxy);
        Assert.Equal(new int?[] { 400, 400, 2000 }, result.Registers.Colors.Mac);
        Assert.Equal(new int?[] { 400, 400, 2000 }, result.Registers.Colors.Ir);
        Assert.Equal(0u, result.Registers.Colors.Ir0);
        Assert.Equal(0u, result.Registers.Colors.Flags);
        Assert.Equal(500u, result.Registers.Geometry.Otz);
        Assert.Equal(500u, result.Registers.Geometry.Mac0);
        Assert.Equal((byte)0x21, result.Registers.Geometry.Rotation20[18]);
        Assert.Equal((byte)0x43, result.Registers.Geometry.Rotation20[19]);
        Assert.False(result.Registers.Colors.NormalInputKnown!.Value[6]);
        Assert.False(result.Registers.Colors.NormalInputKnown.Value[7]);
        Assert.Null(result.FarControls.FarRed);
    }

    [Fact]
    public void Backface_rejection_preserves_partial_packet_writes_and_never_loads_fourth_vertex_or_averages()
    {
        List<int> reads = [];
        short? ReadW(int offset)
        {
            reads.Add(offset);
            return 0;
        }
        NativeBattleCuraga88MeshStep result = Emit(Mesh(true), Write(Context(), 0x1C, 0x40), zsf4: null, readVertexW: ReadW);
        Assert.Null(Assert.Single(result.Batch.Attempts).Primitive);
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.Equal(new[] { 8, 24, 16 }, reads);
        Assert.Equal(unchecked((uint)-102400), Dword(result.Context88, 0x24));
        Assert.Null(result.Registers.Geometry.Otz);
        Assert.Null(result.Registers.Colors.Material);
        Assert.Throws<NotSupportedException>(() => result.Context88.Read(0x2C, 4));
        NativeBattleGfGeometryBatch expected = NativeBattleCuragaMeshPackets.EmitBatch87EEB0(Mesh(true), Matrix, Projection, new(0x40, 0xC08040, 2048, null, null, null, 17, 2), colors: Constants);
        Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(result.Batch));
    }

    [Fact]
    public void Near_rejection_keeps_initial_packet_fields_without_clipping_averaging_or_fourth_vertex_load()
    {
        List<int> reads = [];
        short? ReadW(int offset)
        {
            reads.Add(offset);
            return 0;
        }
        NativeBattleCuraga88MeshStep result = Emit(Mesh(), zsf4: null, readVertexW: ReadW, matrix: Matrix with { Z = -2000 });
        Assert.Null(Assert.Single(result.Batch.Attempts).Primitive);
        Assert.Equal(new[] { 8, 16, 24 }, reads);
        Assert.NotEqual(0u, Dword(result.Context88, 0x30) & 0x60000);
        Assert.Null(result.Registers.Geometry.Otz);
        Assert.Equal(0u, result.Registers.Geometry.Mac0);
        Assert.Null(result.Registers.Colors.Material);
        Assert.Throws<NotSupportedException>(() => result.Context88.Read(0x24, 4));
        Assert.Throws<NotSupportedException>(() => result.Context88.Read(0x2C, 4));
    }

    [Fact]
    public void FT4_uses_actual_88_context_palette_and_texture_page_fields()
    {
        NativeBattleEffectArena context = Write(Write(Write(Context(), 0x1C, 0xFF0), 0x10, 0x17), 0x14, 0x23);
        NativeBattleCuraga88MeshStep result = Emit(Mesh(), context);
        NativeBattleGfGeometryBatch expected = NativeBattleCuragaMeshPackets.EmitBatch87EEB0(Mesh(), Matrix, Projection, new(0xFF0, 0xC08040, 2048, 0x17, 0x23, null, 17, 2), colors: Constants);
        Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(result.Batch));
        Assert.Equal(0x17u, Dword(result.Context88, 0x10));
        Assert.Equal(0x23u, Dword(result.Context88, 0x14));
    }

    [Fact]
    public void Screen_rejection_averages_before_rejection_but_does_not_publish_depth_or_shade()
    {
        NativeBattleCuraga88MeshStep result = Emit(Mesh(), projection: new(null, 100, -100, -100));
        Assert.Null(Assert.Single(result.Batch.Attempts).Primitive);
        Assert.Equal(500u, result.Registers.Geometry.Otz);
        Assert.Equal(500u, result.Registers.Geometry.Mac0);
        Assert.Equal(0u, result.Registers.Colors.Flags);
        Assert.Null(result.Registers.Colors.Material);
        Assert.Throws<NotSupportedException>(() => result.Context88.Read(0x2C, 4));
    }

    [Fact]
    public void Full_context_and_canonical_register_owners_restore_for_the_next_draw_without_hydration()
    {
        NativeBattleCuraga88MeshStep first = Emit(Mesh());
        NativeBattleCuraga88MeshStep restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleCuraga88MeshStep>(NativeBattleTestSnapshots.Json(first))!;
        NativeBattleCuraga88MeshStep Next(NativeBattleCuraga88MeshStep prior) => NativeBattleCuraga88Mesh.EmitBatch(Mesh(), Address, Matrix, Projection, prior.Context88, prior.Registers, prior.FarControls, 0, 0, 0x100, Constants, 0x4321, 0x155);
        Assert.Equal(NativeBattleTestSnapshots.Json(Next(first)), NativeBattleTestSnapshots.Json(Next(restored)));
    }

    private static NativeBattleCuraga88MeshStep Emit(ImmutableArray<byte> mesh, NativeBattleEffectArena? context = null, NativeBattleCameraProjection? projection = null, short? zsf4 = 0x100, Func<int, short?>? readVertexW = null, NativeBattleActionMatrix? matrix = null)
    {
        NativeBattleScriptedColorRegisters colors = NativeBattleScriptedColorRegisters.Unknown() with { Rgb = [null, 0x12345678, 0xABCDEF01] };
        return NativeBattleCuraga88Mesh.EmitBatch(mesh, Address, matrix ?? Matrix, projection ?? Projection, context ?? Context(), new(NativeBattleGeometryRegisters.Unknown(), colors), new(1, 2, 3, 4, 5), 0, 0, zsf4, Constants, 0x4321, 0x155, readVertexW);
    }

    private static NativeBattleEffectArena Context() => NativeBattleCuraga88Mesh.PrepareContext(NativeBattleEffectArena.Clear().MarkUnknown(0, 88).Write(0x34, [1, 2, 3, 4]), Address, 0, 2048, 0xC08040);
    private static uint Dword(NativeBattleEffectArena context, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(context.Read(offset, 4));
    private static NativeBattleEffectArena Write(NativeBattleEffectArena context, int offset, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return context.Write(offset, bytes);
    }

    private static ImmutableArray<byte> Mesh(bool reverse = false)
    {
        byte[] mesh = new byte[96];
        BinaryPrimitives.WriteUInt32LittleEndian(mesh, 40);
        BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(4), 4);
        short[] positions = [-400, -400, 0, 0x1111, 400, -400, 0, 0x2222, -400, 400, 0, 0x3333, 400, 400, 0, 0x4444];
        for (int index = 0; index < positions.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(mesh.AsSpan(8 + index * 2), positions[index]);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(52), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(56), 0x2C332211);
        ushort[] indices = reverse ? [0, 4, 2, 6] : [0, 2, 4, 6];
        for (int index = 0; index < indices.Length; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(60 + index * 2), indices[index]);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(68), 0x12340302);
        BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(72), 0x56780605);
        BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(76), 0x0A090807);
        return mesh.ToImmutableArray();
    }
}
