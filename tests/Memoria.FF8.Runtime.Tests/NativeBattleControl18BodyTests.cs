using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleControl18BodyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Effect77_body_capture_keeps_native_partial_writes_and_face_identity(bool quad)
    {
        AssertSelectedCapture(77, quad);
    }

    [Theory]
    [InlineData((ushort)266, false)]
    [InlineData((ushort)266, true)]
    [InlineData((ushort)267, false)]
    [InlineData((ushort)267, true)]
    [InlineData((ushort)268, false)]
    [InlineData((ushort)268, true)]
    [InlineData((ushort)269, false)]
    [InlineData((ushort)269, true)]
    [InlineData((ushort)270, false)]
    [InlineData((ushort)270, true)]
    public void Selected_death_body_retains_actual_world_depth_resource_and_native_partial_writes(ushort effect, bool quad)
    {
        AssertSelectedCapture(effect, quad);
    }

    private static void AssertSelectedCapture(ushort effect, bool quad)
    {
        byte[] polygon = Polygon(quad, 0);
        System.Numerics.Vector3[] world = [new(1, 2, 3), new(4, 5, 6), new(7, 8, 9), new(10, 11, 12)];
        NativeBattlePilotVisualIdentity identity = new(effect, 0x1000, 2, 3, 4) { ResourceGeneration = 5 };
        NativeBattleGfGeometryBatch plain = NativeBattleControl18Body.Emit(quad ? [] : polygon, quad ? polygon : [], Points(4), new byte[1], 0xAA112233, null, 17);
        NativeBattleGfGeometryBatch captured = NativeBattleControl18Body.Emit(quad ? [] : polygon, quad ? polygon : [], Points(4), new byte[1], 0xAA112233, null, 17, world, identity);
        Assert.Equal(NativeBattleTestSnapshots.Json(plain), NativeBattleTestSnapshots.Json(captured));
        NativeBattlePilotVisualGeometry geometry = Assert.Single(captured.Attempts).Primitive!.PilotGeometry!;
        Assert.NotNull(geometry);
        Assert.Equal(world[0], geometry.A);
        Assert.Equal(world[1], geometry.B);
        Assert.Equal(world[2], geometry.C);
        Assert.Equal((byte)(quad ? 4 : 3), geometry.CornerCount);
        Assert.Equal(identity.ResourceGeneration, geometry.Identity.ResourceGeneration);
        Assert.Equal(effect, geometry.Identity.Effect);
        NativeBattleGfGeometryBatch nativeDepths = NativeBattleCinematicActorPresentation.NativeDepths(captured);
        Assert.True(Assert.Single(nativeDepths.Attempts).Primitive!.PilotGeometry!.UseSourceDepths);
        Assert.Equal(new System.Numerics.Vector4(4096, 4096, 4096, quad ? 4096 : 0), Assert.Single(nativeDepths.Attempts).Primitive!.PilotGeometry!.SourceDepths);
    }

    [Fact]
    public void Unlit_vertex_rejection_keeps_partial_SXY_stores_at_the_same_cursor_as_the_next_accepted_packet()
    {
        byte[] points = Points(7);
        BinaryPrimitives.WriteUInt16LittleEndian(points.AsSpan(6), 0x8000);
        NativeBattleGfGeometryBatch batch = NativeBattleControl18Body.Emit(Polygon(false, 0), Polygon(true, 3), points, new byte[2], 0xAA112233, null, 17);
        Assert.Equal(40, batch.AllocationBytes);
        Assert.Equal(2, batch.Attempts.Length);
        Assert.All(batch.Attempts, attempt => Assert.Equal(0, attempt.ByteOffsetRelative));
        NativeBattleGfGeometryAttempt rejected = batch.Attempts[0];
        Assert.Null(rejected.Primitive);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, rejected.WriteMask[3]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, rejected.WriteMask[4]);
        Assert.Equal(NativeBattleGfPacketByteState.Known, rejected.WriteMask[8]);
        Assert.Equal(NativeBattleGfPacketByteState.Known, rejected.WriteMask[16]);
        Assert.Equal(NativeBattleGfPacketByteState.Known, rejected.WriteMask[24]);
        Assert.NotNull(batch.Attempts[1].Primitive);
    }

    [Fact]
    public void Only_unlit_triangles_reject_a_vertical_span_crossing_zero_and210()
    {
        byte[] points = Points(4);
        BinaryPrimitives.WriteUInt32LittleEndian(points, 0xFFFF0000);
        BinaryPrimitives.WriteUInt32LittleEndian(points.AsSpan(8), 211u << 16);
        NativeBattleGfGeometryBatch batch = NativeBattleControl18Body.Emit(Polygon(false, 0), Polygon(true, 0), points, new byte[2], 0x808080, null, 17);
        Assert.Null(batch.Attempts[0].Primitive);
        Assert.NotNull(batch.Attempts[1].Primitive);
        Assert.Equal(40, batch.AllocationBytes);
    }

    [Fact]
    public void Lit_branch_ignores_vertex_and_vertical_rejection_and_keeps_physical_normal_indices_across_hidden_faces()
    {
        byte[] points = Points(4);
        for (int vertex = 0; vertex < 4; vertex++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(points.AsSpan(vertex * 8 + 6), 0x8000);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(points, 0xFFFF0000);
        BinaryPrimitives.WriteUInt32LittleEndian(points.AsSpan(8), 211u << 16);
        byte[] triangles = [.. Polygon(false, 0), .. Polygon(false, 0)];
        List<int> faces = [];
        uint Light(int face, ImmutableArray<uint> screen, uint material)
        {
            faces.Add(face);
            Assert.Equal(3, screen.Length);
            Assert.Equal(0xAA112233u, material);
            return 0xCCABCDEF;
        }
        NativeBattleGfGeometryBatch batch = NativeBattleControl18Body.Emit(triangles, Polygon(true, 0), points, new byte[] { 1, 0, 0 }, 0xAA112233, Light, 17);
        Assert.Equal(new[] { 1, 2 }, faces);
        Assert.Equal(72, batch.AllocationBytes);
        Assert.Equal(0x24ABCDEFu, BinaryPrimitives.ReadUInt32LittleEndian(batch.Attempts[0].Data.AsSpan(4, 4)));
        Assert.Equal(0x2CABCDEFu, BinaryPrimitives.ReadUInt32LittleEndian(batch.Attempts[1].Data.AsSpan(4, 4)));
        Assert.All(batch.Attempts, attempt => Assert.NotNull(attempt.Primitive));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Body_uses_opaque_packet_codes_and_original_TPAGE_without_the_shard_semitransparency_bit(bool quad)
    {
        byte[] polygon = Polygon(quad, 0);
        NativeBattleGfGeometryBatch batch = NativeBattleControl18Body.Emit(quad ? [] : polygon, quad ? polygon : [], Points(4), new byte[1], 0xAA112233, null, 17);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Equal(quad ? 0x2C112233u : 0x24112233u, BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan(4, 4)));
        Assert.Equal((ushort)0x0F0E, BinaryPrimitives.ReadUInt16LittleEndian(attempt.Data.AsSpan(22, 2)));
        Assert.All(attempt.WriteMask.Take(3), state => Assert.Equal(NativeBattleGfPacketByteState.Untouched, state));
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[30]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[31]);
        NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(attempt.Primitive);
        Assert.Equal(273, primitive.Bucket);
        Assert.Equal(quad ? new[] { 4096, 4096, 4096, 4096 } : new[] { 4096, 4096, 4096, 0 }, primitive.VertexPayload4);
    }

    [Fact]
    public void Insertion_rejection_consumes_full_packets_after_all_body_stores()
    {
        NativeBattleGfGeometryBatch batch = NativeBattleControl18Body.Emit(Polygon(false, 0), Polygon(true, 0), Points(4, 65535), new byte[2], 0x808080, null, 17);
        Assert.Equal(72, batch.AllocationBytes);
        Assert.Equal(new[] { 0, 32 }, batch.Attempts.Select(attempt => attempt.ByteOffsetRelative));
        Assert.All(batch.Attempts, attempt =>
        {
            Assert.Null(attempt.Primitive);
            Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[3]);
            Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[4]);
        });
    }

    private static byte[] Polygon(bool quad, int first)
    {
        byte[] result = Enumerable.Range(0, quad ? 20 : 16).Select(index => (byte)index).ToArray();
        for (int vertex = 0; vertex < (quad ? 4 : 3); vertex++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(vertex * 2), (ushort)(0xF000 | first + vertex));
        }
        return result;
    }
    private static byte[] Points(int count, ushort depth = 4096)
    {
        byte[] result = new byte[count * 8];
        for (int vertex = 0; vertex < count; vertex++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(vertex * 8), (uint)(vertex + 1));
            BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(vertex * 8 + 4), depth);
        }
        return result;
    }
}
