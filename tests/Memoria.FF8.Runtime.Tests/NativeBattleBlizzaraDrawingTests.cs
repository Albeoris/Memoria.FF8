using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBlizzaraDrawingTests
{
    private static NativeBattleActionMatrix View => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4096, 4096);
    private static NativeBattleCameraProjection Projection => new(View, 216, 160, 108);
    private static ImmutableArray<NativeBattleModuleProjectedVertex> Corners => [new(XY(0, 0), 4096, 0), new(XY(128, 0), 4096, 0), new(XY(0, 128), 4096, 0)];

    [Fact]
    public void Main_GT3_preserves_source_high_bytes_UV_words_and_actual_proxy_depths_without_DPCS()
    {
        ImmutableArray<byte> source = Face(128);
        NativeBattleGfGeometryBatch batch = NativeBattleBlizzaraDrawing.EmitMainFace6DB5D0(source, Corners, 0x6600C);
        NativeBattleGeometryPrimitive primitive = Assert.Single(batch.Attempts).Primitive!;
        Assert.Equal(40, batch.AllocationBytes);
        Assert.Equal(0x34A0B0C0u, primitive.Words[1]);
        Assert.Equal(0x80112233u, primitive.Words[4]);
        Assert.Equal(0x00445566u, primitive.Words[7]);
        Assert.Equal(0x77880102u, primitive.Words[3]);
        Assert.Equal(0x00B40304u, primitive.Words[6]);
        Assert.Equal(0x0506u, primitive.Words[9]);
        Assert.Equal(new int[] { 4096, 4096, 4096, 0 }, primitive.VertexPayload4.ToArray());
        Assert.Equal(272, primitive.Bucket);
    }

    [Fact]
    public void Accepted_overlay_links_GT3_then_F3_then_E1_and_retains_triangle_depth_FIFO()
    {
        NativeBattleGfGeometryBatch batch = NativeBattleBlizzaraDrawing.EmitMainFace6DB5D0(Face(128), Corners, 0x660C0);
        Assert.Equal(68, batch.AllocationBytes);
        Assert.Equal(new int[] { 0, 40, 60 }, batch.Attempts.Select(attempt => attempt.ByteOffsetRelative).ToArray());
        NativeBattleGeometryPrimitive[] packets = batch.Attempts.Select(attempt => attempt.Primitive!).ToArray();
        Assert.Equal(0x22808080u, packets[1].Words[1]);
        Assert.Equal(new uint[] { 0x04000000, 0x22808080, XY(0, 0), XY(128, 0), XY(0, 128) }, packets[1].Words.ToArray());
        Assert.Equal(new uint[] { 0x01000000, 0xE1000240 }, packets[2].Words.ToArray());
        Assert.Equal(packets[0].VertexPayload4.ToArray(), packets[1].VertexPayload4.ToArray());
        Assert.Equal(new int[] { 0, 0, 0, 0 }, packets[2].VertexPayload4.ToArray());
        Assert.All(packets, primitive => Assert.Equal(packets[0].Bucket, primitive.Bucket));
        Assert.Equal(40, NativeBattleBlizzaraDrawing.EmitMainFace6DB5D0(Face(0), Corners, 0x660C0).AllocationBytes);
    }

    [Fact]
    public void Source_alpha_alone_controls_backface_override_and_rejection_keeps_only_native_partial_stores()
    {
        ImmutableArray<NativeBattleModuleProjectedVertex> backwards = [Corners[0], Corners[2], Corners[1]];
        Assert.Equal(0, NativeBattleBlizzaraDrawing.EmitMainFace6DB5D0(Face(0), backwards, 0x6600C | 0x20).AllocationBytes);
        Assert.Equal(40, NativeBattleBlizzaraDrawing.EmitMainFace6DB5D0(Face(1), backwards, 0x6600C).AllocationBytes);
        ImmutableArray<NativeBattleModuleProjectedVertex> near = Corners.SetItem(0, Corners[0] with { Flags = 0x60000 });
        NativeBattleGfGeometryBatch rejected = NativeBattleBlizzaraDrawing.EmitMainFace6DB5D0(Face(255), near, 0x660C0);
        Assert.Equal(0, rejected.AllocationBytes);
        NativeBattleGfGeometryAttempt prefix = Assert.Single(rejected.Attempts);
        Assert.Null(prefix.Primitive);
        Assert.Equal(NativeBattleGfPacketByteState.Known, prefix.WriteMask[12]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, prefix.WriteMask[8]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, prefix.WriteMask[16]);
        Assert.Throws<NotSupportedException>(() => NativeBattleBlizzaraDrawing.EmitMainFace6DB5D0(Face(255).SetItem(27, 1), Corners, 0x660C0));
    }

    [Fact]
    public void Main_dispatcher_walks_canonical_remapped_vertices_and_advances_by_each_actual_leaf_allocation()
    {
        (ImmutableArray<byte> clone, ImmutableArray<byte> vertices) = MainMesh();
        clone = clone.SetItem(0x454 + 23, 128);
        NativeBattleGfGeometryBatch result = NativeBattleBlizzaraDrawing.EmitMainMesh(clone, vertices, View, Projection, 0x660C0);
        Assert.Equal(223 * 40 + 28, result.AllocationBytes);
        Assert.Equal(225, result.Attempts.Length);
        Assert.Equal(68, result.Attempts[3].ByteOffsetRelative);
        Assert.Throws<InvalidDataException>(() => NativeBattleBlizzaraDrawing.EmitMainMesh(clone, [], View, Projection, 0x660C0));
        Assert.Throws<InvalidDataException>(() => NativeBattleBlizzaraDrawing.EmitMainMesh(clone.SetItem(0x454 + 4, 1), vertices, View, Projection, 0x660C0));
        byte[] hidden = vertices.ToArray();
        for (int corner = 0; corner < 3; corner++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(hidden.AsSpan(corner * 8 + 4), -5000);
        }
        NativeBattleGfGeometryBatch clipped = NativeBattleBlizzaraDrawing.EmitMainMesh(clone, hidden.ToImmutableArray(), View, Projection, 0x660C0);
        Assert.Null(clipped.Attempts[0].Primitive);
        Assert.Equal(0, clipped.Attempts[1].ByteOffsetRelative);
        Assert.Equal(222 * 40, clipped.AllocationBytes);
    }

    [Theory]
    [InlineData(0x6DBB20u, 0, true)]
    [InlineData(0x6DBB20u, 6, false)]
    [InlineData(0x6DBB20u, 18, false)]
    [InlineData(0x6DBB20u, 30, true)]
    [InlineData(0x6DBD20u, 0, true)]
    [InlineData(0x6DBD20u, 6, false)]
    [InlineData(0x6DBD20u, 14, false)]
    [InlineData(0x6DBD20u, 20, true)]
    public void Child_meshes_apply_their_distinct_preupdate_fade_draw_gates(uint callback, int counter, bool absent)
    {
        NativeBattleModulePoolTask task = Task(callback, counter).WriteWord(0x22, 4096).WriteWord(0x1A, 4096);
        NativeBattleBlizzaraMeshDraw? draw = NativeBattleBlizzaraDrawing.DescribeMesh(task, Resources(), View);
        Assert.Equal(absent, draw is null);
        if (draw is not null)
        {
            Assert.Equal(0xF3u, draw.Context.Flags);
            Assert.Equal((uint?)0, draw.Context.FarColor);
        }
    }

    [Fact]
    public void Smoke_and_trail_read_distinct_position_scale_fields_and_original_frame_BYTE()
    {
        NativeBattleModulePoolTask smoke = Task(0x6DBEA0, 0x10F).WriteWord(0x14, 10).WriteWord(0x16, 20).WriteWord(0x18, 30).WriteWord(0x1A, 4000);
        NativeBattleModuleDraw first = NativeBattleBlizzaraDrawing.DescribeSprite(smoke, Resources());
        Assert.Equal(new NativeBattleModuleAnchor(10, 20, 30, 4000), first.Anchor);
        Assert.Equal((ushort)15, first.Phase);
        Assert.Equal(-1000, first.DepthOffset);
        NativeBattleModulePoolTask trail = Task(0x6DBFF0, 14).WriteWord(0x10, 40).WriteWord(0x12, 50).WriteWord(0x14, 60).WriteWord(0x16, 3000);
        NativeBattleModuleDraw second = NativeBattleBlizzaraDrawing.DescribeSprite(trail, Resources());
        Assert.Equal(new NativeBattleModuleAnchor(40, 50, 60, 3000), second.Anchor);
        Assert.Equal(-750, second.DepthOffset);
        Assert.Equal((ushort)0, second.SpriteFlags);
    }

    private static ImmutableArray<byte> Face(byte alpha)
    {
        byte[] face = new byte[28];
        BinaryPrimitives.WriteUInt32LittleEndian(face, 0x34A0B0C0);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(6), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(8), 4);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(10), 0x0506);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(12), 0x77880102);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(16), 0x00B40304);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(20), (uint)alpha << 24 | 0x00112233);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(24), 0x00445566);
        return face.ToImmutableArray();
    }

    private static (ImmutableArray<byte> Clone, ImmutableArray<byte> Vertices) MainMesh()
    {
        byte[] clone = new byte[0x1CBC];
        byte[] vertices = new byte[669 * 8];
        BinaryPrimitives.WriteUInt32LittleEndian(clone, 0x438);
        BinaryPrimitives.WriteInt32LittleEndian(clone.AsSpan(0x450), 223);
        for (int face = 0; face < 223; face++)
        {
            Face(0).AsSpan().CopyTo(clone.AsSpan(0x454 + face * 28));
            for (int corner = 0; corner < 3; corner++)
            {
                int offset = (face * 3 + corner) * 8;
                BinaryPrimitives.WriteUInt16LittleEndian(clone.AsSpan(0x458 + face * 28 + corner * 2), checked((ushort)(offset / 4)));
                BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(offset), corner == 1 ? (short)64 : (short)-64);
                BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(offset + 2), corner == 2 ? (short)64 : (short)-64);
                BinaryPrimitives.WriteUInt16LittleEndian(vertices.AsSpan(offset + 6), 0xA5A5);
            }
        }
        return (clone.ToImmutableArray(), vertices.ToImmutableArray());
    }

    private static NativeBattleModulePoolTask Task(uint callback, int counter) => new NativeBattleModulePoolTask(1, 0, null, callback, new byte[28].ToImmutableArray()).WriteWord(0xC, unchecked((ushort)counter));
    private static uint XY(short x, short y) => unchecked((ushort)x) | unchecked((uint)y << 16);

    private static NativeBattleBlizzaraResources Resources()
    {
        byte[] sprite = new byte[68];
        BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(8), 16);
        for (int frame = 0; frame <= 16; frame++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(10 + frame * 2), 44);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(sprite.AsSpan(44), 1);
        sprite[48] = 1;
        sprite[49] = 128;
        sprite[50] = 1;
        sprite[51] = 0x2C;
        return new(new("animation", []), new(1, 1, 0, 0, sprite.ToImmutableArray()), new(1, 1, []), new(2, 2, []), new(3, 3, []), new(4, 4, []), 1);
    }
}
