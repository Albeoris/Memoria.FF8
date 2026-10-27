using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRaldo19BallRenderingTests
{
    private static NativeBattleActionMatrix Identity => NativeBattleCuragaMatrices.Identity8DD770();
    private static NativeBattleSummonColors Constants => new(0, 0, 1f / 4096f, BitConverter.Int64BitsToDouble(0x4330000080000000L));
    private static NativeBattleCameraProjection Projection => new(null, 100, 160, 108);

    [Fact]
    public void Actual_layout_uses_AC_actor_A4_morph_and_A6_A8_AA_radii()
    {
        NativeBattleEffectArena ball = Word(Ball(), 0xB0, 9);
        NativeBattleRaldo19BallParameters read = NativeBattleRaldo19BallRendering.Read(ball);
        Assert.Equal((short)3, read.Actor);
        Assert.Equal((short)0, read.Weight);
        Assert.Equal(new NativeBattleVector(400, 400, 400), read.Radii);
        Assert.Throws<InvalidDataException>(() => NativeBattleRaldo19BallRendering.Read(Dword(ball, 0x6C, 0)));
        Assert.Throws<InvalidDataException>(() => NativeBattleRaldo19BallRendering.Read(Word(ball, 0xAC, 11)));
    }

    [Fact]
    public void Prepare_copies_world_padding_preserves_camera_padding_and_publishes_full_MAC_translation()
    {
        NativeBattleRaldo19BallPreparation prepared = Prepare();
        Assert.Equal((short)0x1234, Short(prepared.BallC4, 0x32));
        Assert.Throws<NotSupportedException>(() => prepared.BallC4.Read(0x52, 2));
        Assert.Equal(2000u, Dword(prepared.BallC4, 0x5C));
        Assert.Equal(new int?[] { 0, 0, 2000 }, prepared.Registers.Colors.Mac);
        Assert.Equal(new int?[] { 0, 0, 2000 }, prepared.Registers.Colors.Ir);
        Assert.Equal((byte)0x78, prepared.Registers.Geometry.Rotation20[18]);
        Assert.Equal((byte)0x56, prepared.Registers.Geometry.Rotation20[19]);
        Assert.Equal(new int?[] { 0, 0, 0 }, prepared.Registers.Geometry.Translation);
        Assert.Equal((short)0, BinaryPrimitives.ReadInt16LittleEndian(prepared.Registers.Colors.NormalInput!.Value.AsSpan().Slice(6)));
    }

    [Fact]
    public void Complete_vertex_polygon_pipeline_reuses_ball_math_and_packets_with_exact_partial_owners()
    {
        NativeBattleRaldo19BallPreparation prepared = Prepare();
        NativeBattleEffectArena temporary = NativeBattleEffectArena.Clear().MarkUnknown(0, 0xA8).Write(0x52, [0x88, 0x77]).Write(0x6C, [1, 2, 3, 4]);
        NativeBattleEffectArena scratch = NativeBattleEffectArena.Clear().MarkUnknown(0, 0x1F40).Write(16, [5, 6, 7, 8]);
        NativeBattleRaldo19BallDraw result = Draw(prepared, temporary, scratch);
        NativeBattleRawMesh mesh = Mesh();
        NativeBattleActionMatrix camera = Identity with { Z = 2000 };
        NativeBattleModuleProjectedVertex[] projected = new NativeBattleModuleProjectedVertex[4];
        for (int index = 0; index < 4; index++)
        {
            NativeBattleVector expected = NativeBattleWendigoBallGeometry.Morph(mesh.Vertices[index], Identity, Identity, new(0, 0, 0), 0, new(400, 400, 400));
            Assert.Equal(expected, Vector(result.VertexScratch, index * 20));
            Assert.Equal((short)0x7788, Short(result.VertexScratch, index * 20 + 6));
            projected[index] = NativeBattleModuleProjection.Project45FE10(camera, expected, Projection);
            Assert.Equal(projected[index].Screen, Dword(result.VertexScratch, index * 20 + 8));
            Assert.Equal((uint)projected[index].Depth, Dword(result.VertexScratch, index * 20 + 12));
        }
        NativeBattleGfGeometryBatch expectedPackets = NativeBattleWendigoBallGeometry.Packets(mesh.Polygons, projected.ToImmutableArray(), 0x00808080);
        Assert.Equal(NativeBattleTestSnapshots.Json(expectedPackets), NativeBattleTestSnapshots.Json(result.Batch));
        Assert.Equal(new byte[] { 5, 6, 7, 8 }, result.VertexScratch.Read(16, 4));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, result.TemporaryA8.Read(0x6C, 4));
        Assert.Equal(102400u, Dword(result.TemporaryA8, 0x7C));
        Assert.Equal(125u, Dword(result.TemporaryA8, 0x78));
        Assert.Equal(3u, Dword(result.TemporaryA8, 0x8C));
        Assert.Equal(new uint?[] { projected[0].Screen, projected[1].Screen, projected[2].Screen }, result.Registers.Geometry.Sxy);
        Assert.Equal(new uint?[] { 2000, 2000, 2000, 2000 }, result.Registers.Geometry.Sz);
        Assert.Null(result.Registers.Geometry.Otz);
        Assert.Equal(new int?[] { 400, 400, 2000 }, result.Registers.Colors.Mac);
        Assert.Equal(0xAA123456u, result.Registers.Colors.Material);
        Assert.Equal(0u, result.Registers.Colors.Ir0);
        Assert.Equal(0u, result.Registers.Colors.Flags);
        Assert.Equal(new NativeBattleScriptedBackColor(100, 200, 300), result.Lighting.BackColor);
        Assert.Equal((byte)0x55, result.Lighting.LightMatrix!.Bytes[18]);
        Assert.Equal((byte)0x44, result.Lighting.LightMatrix.Bytes[19]);
    }

    [Fact]
    public void Unknown_temporary_W_stays_unknown_and_actual_cold_restore_preserves_next_step()
    {
        NativeBattleRaldo19BallPreparation prepared = Prepare();
        NativeBattleRaldo19BallDraw first = Draw(prepared, NativeBattleEffectArena.Clear().MarkUnknown(0, 0xA8), NativeBattleEffectArena.Clear().MarkUnknown(0, 0x1F40));
        Assert.Throws<NotSupportedException>(() => first.VertexScratch.Read(6, 2));
        Assert.False(first.Registers.Colors.NormalInputKnown!.Value[6]);
        Assert.False(first.Registers.Colors.NormalInputKnown.Value[7]);
        NativeBattleRaldo19BallDraw restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleRaldo19BallDraw>(NativeBattleTestSnapshots.Json(first))!;
        NativeBattleRaldo19BallDraw Next(NativeBattleRaldo19BallDraw previous)
        {
            return NativeBattleRaldo19BallRendering.DrawMeshes(prepared.BallC4, previous.TemporaryA8, previous.VertexScratch, [Mesh()], [Identity, Identity with { X = 100, Y = 200, Z = 300 }], _ => 0x4455, 1, 0x00808080, Projection, 0, 0, previous.Registers, previous.Lighting, Constants);
        }
        Assert.Equal(NativeBattleTestSnapshots.Json(Next(first)), NativeBattleTestSnapshots.Json(Next(restored)));
    }

    [Fact]
    public void GPF_and_GPL_round_separate_products_shift_FIFO_and_retain_full_cue_and_material()
    {
        NativeBattleScriptedColorRegisters colors = new([10, 20, 30], [-3, 5, 9000], [0x11, 0x22, 0x33], null) { Material = 0xAA123456, Ir0 = 0xABCD0800 };
        NativeBattleGeometryRegisterStep first = NativeBattleRaldo19BallRendering.Weighted(new(NativeBattleGeometryRegisters.Unknown(), colors), false, Constants);
        Assert.Equal(new int?[] { -2, 2, 4500 }, first.Colors.Mac);
        Assert.Equal(new uint?[] { 0x22, 0x33, 0xAAFF0200 }, first.Colors.Rgb);
        Assert.Equal(0x00280000u, first.Colors.Flags);
        first = first with { Colors = first.Colors with { Ir = [3, -5, 9000] } };
        NativeBattleGeometryRegisterStep second = NativeBattleRaldo19BallRendering.Weighted(first, true, Constants);
        Assert.Equal(new int?[] { 0, 0, 9000 }, second.Colors.Mac);
        Assert.Equal(new uint?[] { 0x33, 0xAAFF0200, 0xAAFF0000 }, second.Colors.Rgb);
        Assert.Equal(0x00080000u, second.Colors.Flags);
        Assert.Equal(0xABCD0800u, second.Colors.Ir0);
        Assert.Equal(0xAA123456u, second.Colors.Material);
        Assert.Null(second.Colors.NormalInput);
        Assert.Equal(NativeBattleTestSnapshots.Json(first.Geometry), NativeBattleTestSnapshots.Json(second.Geometry));
    }

    [Fact]
    public void Unknown_material_code_preserves_unknown_FIFO_without_blocking_known_interpolation_or_cold_continuation()
    {
        NativeBattleScriptedColorRegisters colors = new([10, 20, 30], [64, 128, 256], [0x11, 0x22, 0x33], null) { Ir0 = 4096 };
        NativeBattleGeometryRegisterStep unknown = NativeBattleRaldo19BallRendering.Weighted(new(NativeBattleGeometryRegisters.Unknown(), colors), false, Constants);
        NativeBattleGeometryRegisterStep known = NativeBattleRaldo19BallRendering.Weighted(new(NativeBattleGeometryRegisters.Unknown(), colors with { Material = 0xAB123456 }), false, Constants);
        Assert.Equal(new uint?[] { 0x22, 0x33, null }, unknown.Colors.Rgb);
        Assert.Equal(new uint?[] { 0x22, 0x33, 0xABFF8040 }, known.Colors.Rgb);
        Assert.Equal(known.Colors.Mac, unknown.Colors.Mac);
        Assert.Equal(known.Colors.Ir, unknown.Colors.Ir);
        Assert.Equal(known.Colors.Flags, unknown.Colors.Flags);
        Assert.Equal(0x00080000u, unknown.Colors.Flags);
        Assert.Null(unknown.Colors.Material);
        NativeBattleGeometryRegisterStep restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleGeometryRegisterStep>(NativeBattleTestSnapshots.Json(unknown))!;
        NativeBattleGeometryRegisterStep next = NativeBattleRaldo19BallRendering.Weighted(unknown, true, Constants);
        Assert.Equal(NativeBattleTestSnapshots.Json(next), NativeBattleTestSnapshots.Json(NativeBattleRaldo19BallRendering.Weighted(restored, true, Constants)));
        Assert.Equal(new int?[] { 128, 256, 512 }, next.Colors.Mac);
        Assert.Equal(new uint?[] { 0x33, null, null }, next.Colors.Rgb);
        Assert.Equal(0x00180000u, next.Colors.Flags);
    }

    private static NativeBattleRaldo19BallPreparation Prepare()
    {
        NativeBattleScriptedColorRegisters colors = NativeBattleScriptedColorRegisters.Unknown() with { Material = 0xAA123456, Rgb = [0x11, 0x22, 0x33] };
        return NativeBattleRaldo19BallRendering.Prepare(Ball().MarkUnknown(0x52, 2), Identity with { Z = 2000 }, 0x1234, Identity, 0x5678, new(NativeBattleGeometryRegisters.Unknown(), colors));
    }

    private static NativeBattleRaldo19BallDraw Draw(NativeBattleRaldo19BallPreparation prepared, NativeBattleEffectArena temporary, NativeBattleEffectArena scratch)
    {
        return NativeBattleRaldo19BallRendering.DrawMeshes(prepared.BallC4, temporary, scratch, [Mesh()], [Identity, Identity with { X = 100, Y = 200, Z = 300 }], _ => 0x4455, 1, 0x00808080, Projection, 0, 0, prepared.Registers, NativeBattleScriptedLightingMemory.Unknown(), Constants);
    }

    private static NativeBattleRawMesh Mesh()
    {
        NativeBattleVector[] points = [new(-400, -400, 0), new(400, -400, 0), new(-400, 400, 0), new(400, 400, 0)];
        byte[] packed = new byte[30];
        for (int index = 0; index < points.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(packed.AsSpan(index * 6), points[index].X);
            BinaryPrimitives.WriteInt16LittleEndian(packed.AsSpan(index * 6 + 2), points[index].Y);
            BinaryPrimitives.WriteInt16LittleEndian(packed.AsSpan(index * 6 + 4), points[index].Z);
        }
        byte[] polygon = new byte[20];
        for (int corner = 0; corner < 4; corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(polygon.AsSpan(corner * 2), (ushort)corner);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(polygon.AsSpan(8), 0x12340302);
        BinaryPrimitives.WriteUInt32LittleEndian(polygon.AsSpan(12), 0x56780605);
        BinaryPrimitives.WriteUInt32LittleEndian(polygon.AsSpan(16), 0x0A090807);
        return new(points.Select(point => new NativeBattleRawVertex(point, 0)).ToImmutableArray(), [new(true, polygon.ToImmutableArray())]) { PackedVertexGroups = [new(0, 4, packed.ToImmutableArray()), new(1, 0, new byte[6].ToImmutableArray())] };
    }

    private static NativeBattleEffectArena Ball()
    {
        NativeBattleEffectArena ball = NativeBattleEffectArena.Clear().Write(0, new byte[0xC4]);
        foreach (int offset in new[] { 0, 8, 16 })
        {
            ball = Word(ball, offset, 4096);
        }
        ball = Dword(ball, 0x64, NativeBattleRaldo19BallRendering.DescriptorAddress);
        ball = Dword(ball, 0x68, 0x1D972C0 + 3 * 0x9C);
        ball = Dword(ball, 0x6C, 0x1D8E054);
        ball = Word(ball, 0xAC, 3);
        foreach (int offset in new[] { 0xA6, 0xA8, 0xAA })
        {
            ball = Word(ball, offset, 400);
        }
        return ball;
    }
    private static short Short(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static NativeBattleVector Vector(NativeBattleEffectArena arena, int offset) => new(Short(arena, offset), Short(arena, offset + 2), Short(arena, offset + 4));
    private static NativeBattleEffectArena Word(NativeBattleEffectArena arena, int offset, ushort value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return arena.Write(offset, bytes);
    }
    private static NativeBattleEffectArena Dword(NativeBattleEffectArena arena, int offset, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return arena.Write(offset, bytes);
    }
}
