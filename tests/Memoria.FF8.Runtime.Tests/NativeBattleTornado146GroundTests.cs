using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTornado146GroundTests
{
    [Fact]
    public void Grid_retains_all_sentinels_record_payloads_and_prepend_order()
    {
        NativeBattleTornado146Work seed = new(NativeBattleTornado146State.FromColdProcess());
        seed.WriteDword(0x2491E50 + 521 * 12 + 4, 0xAABBCCDD);
        NativeBattleStaticMeshResource mesh = Crater(400, 700);
        Dictionary<int, uint> context = [];
        NativeBattleTornado146State state = NativeBattleTornado146Ground.Initialize615160(seed.Freeze(), mesh, (offset, value) => context[offset] = value);
        NativeBattleTornado146Work work = new(state);
        Assert.Equal(2u, work.Dword(0x2491E4C));
        Assert.Equal(0x2491E5Cu, work.Dword(0x2491BC0 + 31 * 4));
        Assert.Equal(0x2491E50u, work.Dword(0x2491E5C));
        Assert.Equal(1u, work.Dword(0x2491E50));
        Assert.Equal(mesh.Address + 60, work.Dword(0x2491E50 + 4));
        Assert.Equal(mesh.Address + 72, work.Dword(0x2491E5C + 4));
        Assert.Equal(1u, work.Dword(0x2491E50 + 521 * 12));
        Assert.Equal(0xAABBCCDDu, work.Dword(0x2491E50 + 521 * 12 + 4));
        Assert.Equal(mesh.Address + 84, context[0x38]);
        NativeBattleTornado146State restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleTornado146State>(NativeBattleTestSnapshots.Json(state))!;
        NativeBattleTornado146GroundVertex result = NativeBattleTornado146Ground.Deform6167E0(new(restored), mesh, new(0, -900, 0), new(0, 0, 0), 4096);
        Assert.Equal((short)700, result.Position.Y);
    }

    [Theory]
    [InlineData(4096, 400, 1118)]
    [InlineData(1024, 100, 279)]
    [InlineData(-1024, -100, -279)]
    public void Deformation_uses_signed_mean_height_fade_and_weight_division(int fade, short height, short weight)
    {
        NativeBattleStaticMeshResource mesh = Crater(400);
        NativeBattleTornado146Work work = new(NativeBattleTornado146Ground.Initialize615160(NativeBattleTornado146State.FromColdProcess(), mesh));
        Dictionary<int, uint> context = [];
        NativeBattleTornado146GroundVertex result = NativeBattleTornado146Ground.Deform6167E0(work, mesh, new(0, -900, 0), new(0, 0, 0), fade, (offset, value) => context[offset] = value);
        Assert.Equal(new NativeBattleVector(0, height, 0), result.Position);
        Assert.Equal(weight, result.Weight);
        Assert.Equal(1u, context[0x60]);
        Assert.True(unchecked((int)(context[0x94] & context[0xA4] & context[0xB4])) < 0);
    }

    [Fact]
    public void Boundary_zero_crosses_match_but_empty_neighbor_cells_preserve_original_height()
    {
        NativeBattleStaticMeshResource mesh = Crater(400);
        NativeBattleTornado146Work work = new(NativeBattleTornado146Ground.Initialize615160(NativeBattleTornado146State.FromColdProcess(), mesh));
        Dictionary<int, uint> context = [];
        NativeBattleTornado146GroundVertex edge = NativeBattleTornado146Ground.Deform6167E0(work, mesh, new(-100, 27, -100), new(0, 0, 0), 4096, (offset, value) => context[offset] = value);
        Assert.Equal((short)400, edge.Position.Y);
        Assert.Contains(uint.MaxValue, context.Values);
        NativeBattleTornado146GroundVertex empty = NativeBattleTornado146Ground.Deform6167E0(work, mesh, new(1000, 27, 1000), new(0, 0, 0), 4096);
        Assert.Equal(new NativeBattleTornado146GroundVertex(new(1000, 27, 1000), 0), empty);
    }

    [Fact]
    public void Triangle_height_is_its_signed_mean_rather_than_position_interpolation()
    {
        NativeBattleStaticMeshResource source = Crater(0);
        byte[] bytes = source.Bytes.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10), 100);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(18), 400);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(26), 700);
        NativeBattleStaticMeshResource mesh = source with { Bytes = bytes.ToImmutableArray() };
        NativeBattleTornado146Work work = new(NativeBattleTornado146Ground.Initialize615160(NativeBattleTornado146State.FromColdProcess(), mesh));
        NativeBattleTornado146GroundVertex result = NativeBattleTornado146Ground.Deform6167E0(work, mesh, new(-100, -900, -100), new(0, 0, 0), 4096);
        Assert.Equal((short)400, result.Position.Y);
    }

    [Fact]
    public void Capture_projection_preserves_focal_and_uses_signed_truncation_for_its_center()
    {
        NativeBattleCameraProjection projection = NativeBattleTornado146Ground.CaptureProjection(new(-18, 1200, -17), 320);
        Assert.Equal((ushort)320, projection.FocalLength);
        Assert.Equal((short)128, projection.OffsetX);
        Assert.Equal((short)128, projection.OffsetY);
        Assert.Equal(8, projection.View!.X);
        Assert.Equal(-2, projection.View.Y);
        Assert.Equal(320, projection.View.Z);
        Assert.Equal((short)244, projection.View.Rotation[0]);
        Assert.Equal((short)-257, projection.View.Rotation[5]);
        Assert.Equal((short)0, projection.View.Rotation[7]);
    }

    [Fact]
    public void Flat_triangle_has_no_backface_gate_and_uses_stage_base_and_fixed_depth_shift()
    {
        NativeBattleGfGeometryBatch batch = Emit(false, [0, 0, 0]);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Equal(32, batch.AllocationBytes);
        Assert.Equal(4122, attempt.Primitive!.Bucket);
        Assert.Equal(0x24808080u, attempt.Primitive.Words[1]);
        Assert.Equal(0x7FFFu, attempt.Primitive.Words[7]);
        Assert.All(attempt.WriteMask, value => Assert.Equal(NativeBattleGfPacketByteState.Known, value));
    }

    [Theory]
    [InlineData(false, 40, 0x34000000u)]
    [InlineData(true, 52, 0x3C000000u)]
    public void Weighted_faces_use_per_corner_color_and_their_own_AVSZ_shift(bool quad, int allocation, uint command)
    {
        NativeBattleGfGeometryBatch batch = Emit(quad, quad ? [0, 4096, 2048, 4096] : [0, 4096, 2048]);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Equal(allocation, batch.AllocationBytes);
        Assert.Equal(4137, attempt.Primitive!.Bucket);
        Assert.Equal(command | 0x808080, attempt.Primitive.Words[1]);
        Assert.Equal(command, attempt.Primitive.Words[4]);
        Assert.Equal(command | 0x404040, attempt.Primitive.Words[7]);
        Assert.Equal(quad ? 0x01020304u : 0x7FFFu, attempt.Primitive.Words[9]);
    }

    [Fact]
    public void Signed_weights_that_cancel_select_flat_packets()
    {
        NativeBattleGfGeometryBatch batch = Emit(false, [300, -300, 0]);
        Assert.Equal(32, batch.AllocationBytes);
        Assert.Equal(0x24808080u, Assert.Single(batch.Attempts).Primitive!.Words[1]);
    }

    [Fact]
    public void Rejected_flat_face_retains_early_material_stores_without_advancing_allocation()
    {
        NativeBattleGfGeometryBatch batch = Emit(false, [0, 0, 0], near: true);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(0, batch.AllocationBytes);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[20]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[8]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[28]);
    }

    private static NativeBattleGfGeometryBatch Emit(bool quad, short[] weights, bool near = false)
    {
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, near ? 1 : 4000, 4096);
        NativeBattleCameraProjection projection = new(view, 160, 160, 108);
        NativeBattleVector[] positions = [new(-100, 0, 0), new(0, 100, 0), new(100, 0, 0), new(100, 100, 0)];
        ImmutableArray<NativeBattleTornado146GroundVertex> vertices = Enumerable.Range(0, quad ? 4 : 3).Select(index => new NativeBattleTornado146GroundVertex(positions[index], weights[index])).ToImmutableArray();
        byte[] bytes = new byte[quad ? 24 : 20];
        for (int index = 0; index < (quad ? 4 : 3); index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(index * 2), (ushort)index);
        }
        if (!quad)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 0x7FFF);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 0xAABBCCDD);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), 0x11223344);
        if (quad)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), 0x01020304);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(quad ? 20 : 16), quad ? 0x2C808080u : 0x24808080u);
        return NativeBattleTornado146GroundPackets.Emit616AC0([new(quad, bytes.ToImmutableArray())], vertices, view, projection, 0x155, 0x100, 0, 0, NativeBattleGteColorInterpolation.CodeConstants45F270);
    }

    private static NativeBattleStaticMeshResource Crater(params short[] heights)
    {
        int faces = 8 + heights.Length * 24;
        byte[] bytes = new byte[faces + 4 + heights.Length * 12];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, faces);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), heights.Length * 3);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(faces), heights.Length);
        for (int triangle = 0; triangle < heights.Length; triangle++)
        {
            NativeBattleVector[] positions = [new(-100, heights[triangle], -100), new(100, heights[triangle], -100), new(0, heights[triangle], 100)];
            for (int corner = 0; corner < 3; corner++)
            {
                int vertex = 8 + triangle * 24 + corner * 8;
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(vertex), positions[corner].X);
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(vertex + 2), positions[corner].Y);
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(vertex + 4), positions[corner].Z);
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(faces + 4 + triangle * 12 + 4 + corner * 2), (ushort)(triangle * 6 + corner * 2));
            }
        }
        return new(0, 0xDD5564, bytes.ToImmutableArray());
    }
}
