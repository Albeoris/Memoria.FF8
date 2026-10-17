using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShadowTests
{
    [Theory]
    [InlineData(0, 1, -48)]
    [InlineData(8, 1, 0)]
    [InlineData(0, 2, 0)]
    public void Bound_publication_updates_the_rolling_minimum_only_for_the_unlocked_default_sequence(ushort flags2, byte sequence, short expectedMinimum)
    {
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        //One actual compressed pose: XYZ=(0,-3,0), unscaled identity bone.
        NativeBattlePoseSource source = new(skeleton.ToImmutableArray(), [1, 0, 0, 0, 8, 0, 0, 0, 1, 0x80, 2, 0]);
        NativeBattleGraphicsModel model = new(0, 3) { Flags2 = flags2, Sequence = sequence, MinimumY146 = 0, SequenceState = new() { Pose = 0, PoseFrame114 = 1 } };
        NativeBattleGraphicsModel published = NativeBattlePoseMatrices.PublishShadowBounds5086F0(model, source);
        Assert.Equal(expectedMinimum, published.MinimumY146);
        Assert.Equal((short)-48, published.GeometryBounds52!.Minimum.Y);
        Assert.Equal((short)0, model.MinimumY146);
        NativeBattleGraphicsModel unknown = NativeBattlePoseMatrices.PublishShadowBounds5086F0(model with { MinimumY146 = null }, source);
        Assert.Null(unknown.MinimumY146);
        Assert.Equal((short)-100, NativeBattlePoseMatrices.PublishShadowBounds5086F0(model with { MinimumY146 = -100 }, source).MinimumY146);
    }

    [Fact]
    public void Shadow_fan_preserves_native_packet_order_quarter_depth_and_gouraud_edges()
    {
        NativeBattleModuleProjectedVertex[] vertices = Projected();
        vertices[2] = vertices[2] with { Depth = 1007 };
        ImmutableArray<NativeBattleGeometryPrimitive> packets = NativeBattleShadowPackets.Emit5088A0(vertices, 0x32A1C3E5, true, 17, 4);
        Assert.Equal(10, packets.Length);
        Assert.Equal(new uint[] { 0x01000000, 0xE1000620 }, packets[0].Words);
        Assert.Equal(new uint[] { 0x01000000, 0xE1000640 }, packets[^1].Words);
        Assert.All(packets, packet => Assert.Equal(79, packet.Bucket));
        Assert.Equal(new uint[] { 0x06000000, 0x32A1C3E5, vertices[0].Screen, 0, vertices[1].Screen, 0, vertices[2].Screen }, packets[1].Words);
        Assert.Equal(new int[] { 700, 700, 704, 0 }, packets[1].VertexPayload4);
        Assert.Equal(vertices[9].Screen, packets[8].Words[6]);
        Assert.Equal(new int[] { 0, 0, 0, 0 }, packets[0].VertexPayload4);
        Assert.Equal(new int[] { 0, 0, 0, 0 }, packets[^1].VertexPayload4);
        ImmutableArray<NativeBattleGeometryPrimitive> flat = NativeBattleShadowPackets.Emit5088A0(vertices, 0x32A1C3E5, false, 17, 4);
        Assert.Equal(0x32506172u, flat[1].Words[1]);
        Assert.Equal(flat[1].Words[1], flat[1].Words[3]);
        Assert.Equal(flat[1].Words[1], flat[1].Words[5]);
    }

    [Fact]
    public void Whole_shadow_uses_all_clip_flags_and_first_winding_but_accepts_degenerate_or_screen_clamped_points()
    {
        NativeBattleModuleProjectedVertex[] vertices = Projected();
        vertices[9] = vertices[9] with { Flags = 0x80020000 };
        Assert.Empty(NativeBattleShadowPackets.Emit5088A0(vertices, 0x32000000, true, 0, 16));
        vertices = Projected();
        (vertices[1], vertices[2]) = (vertices[2], vertices[1]);
        Assert.Empty(NativeBattleShadowPackets.Emit5088A0(vertices, 0x32000000, true, 0, 16));
        vertices[2] = vertices[1] with { Flags = 0x80004000 };
        Assert.Equal(10, NativeBattleShadowPackets.Emit5088A0(vertices, 0x32000000, true, 0, 16).Length);
    }

    [Fact]
    public void Per_triangle_precision_depth_must_be_positive_and_draw_modes_survive_an_empty_fan()
    {
        NativeBattleModuleProjectedVertex[] vertices = Projected();
        vertices[9] = vertices[9] with { Depth = 303 };
        ImmutableArray<NativeBattleGeometryPrimitive> packets = NativeBattleShadowPackets.Emit5088A0(vertices, 0x32000000, true, 17, 16);
        Assert.Equal(9, packets.Length);
        vertices[0] = vertices[0] with { Depth = 303 };
        Assert.Equal(2, NativeBattleShadowPackets.Emit5088A0(vertices, 0x32000000, true, 17, 16).Length);
        Assert.Throws<InvalidDataException>(() => NativeBattleShadowPackets.Emit5088A0(vertices[..9], 0, true, 0, 16));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Rejected_shadow_fan_retains_the_last_full_candidate_tail_after_the_final_E1_overwrites_its_first_eight_bytes(bool gouraud)
    {
        NativeBattleModuleProjectedVertex[] vertices = Projected();
        vertices[0] = vertices[0] with { Depth = 303 };
        NativeBattleGfGeometryBatch batch = NativeBattleShadowPackets.EmitBatch5088A0(vertices, 0x32A1C3E5, gouraud, 17, 16);
        Assert.Equal(16, batch.AllocationBytes);
        Assert.Equal(10, batch.Attempts.Length);
        Assert.Equal(8, batch.Attempts.Count(attempt => attempt.Primitive is null));
        Assert.All(batch.Attempts.Skip(1), attempt => Assert.Equal(8, attempt.ByteOffsetRelative));
        NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400(), NativeBattleLogicalOrdering.Clear(), batch);
        Assert.Equal(16, written.Process.ByteCursor);
        Assert.Equal(2, written.Ordering.Proxies.Length);
        Assert.Equal(new byte[] { 0x28, 0x88, 0xC4 }, written.Process.ReadCommonPacket(8, 3));
        byte[] expected = new byte[20];
        uint edgeColor = gouraud ? 0u : 0x32506172u;
        uint[] tail = [vertices[0].Screen, edgeColor, vertices[8].Screen, edgeColor, vertices[9].Screen];
        for (int index = 0; index < tail.Length; index++)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(4 * index), tail[index]);
        }
        Assert.Equal(expected, written.Process.ReadCommonPacket(16, 20));
        NativeBattlePrimitiveProcess clone = System.Text.Json.JsonSerializer.Deserialize<NativeBattlePrimitiveProcess>(System.Text.Json.JsonSerializer.Serialize(written.Process))!;
        Assert.Equal(expected, clone.ReadCommonPacket(16, 20));
        Assert.Throws<NotSupportedException>(() => clone.ReadCommonPacket(36, 1));
    }

    [Fact]
    public void Accepted_shadow_packets_allocate_only_their_native240bytes_and_keep_exact_proxy_offsets()
    {
        NativeBattleGfGeometryBatch batch = NativeBattleShadowPackets.EmitBatch5088A0(Projected(), 0x32FFFFFF, true, 17, 16);
        NativeBattlePrimitiveProcess source = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400() with { ByteCursor = 12 };
        NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(source, NativeBattleLogicalOrdering.Clear(), batch);
        Assert.Equal(240, batch.AllocationBytes);
        Assert.Equal(252, written.Process.ByteCursor);
        Assert.Equal(new[] { 12, 20, 48, 76, 104, 132, 160, 188, 216, 244 }, written.Ordering.Proxies.Select(proxy => proxy.Packet.ByteOffset));
        Assert.Equal((byte)1, written.Process.ReadCommonPacket(247, 1)[0]);
        Assert.Throws<InvalidDataException>(() => NativeBattleCommonPacketArena.Append(source with { ByteCursor = 114688 - 236 }, NativeBattleLogicalOrdering.Clear(), batch));
    }

    [Fact]
    public void Shadow_matrix_flattens_rows_after_local_center_and_keeps_native_ground_and_seed_axes()
    {
        NativeBattleActionMatrix world = new([4096, 0, 0, 0, 0, -4096, 0, 4096, 0], 100, 200, 300, 4096);
        NativeBattleGraphicsModel model = new(0, 3) { GeometryWorldMatrix = world, Transform = new(default, default, 0, -7, 4096) };
        NativeBattleShadowSource source = new(Identity() with { Y = 3 }, new(4096, 0, 4096), Enumerable.Range(0, 10).Select(index => new NativeBattleVector(0, (short)index, 0)).ToImmutableArray());
        NativeBattleEffectLocalBounds bounds = new(new(0, -100, 0), new(20, 100, 40));
        NativeBattleShadowGeometry shape = NativeBattlePoseMatrices.ShadowGeometry5088A0(model, bounds, Identity(), source, true);
        Assert.Equal(new short[] { 4096, 0, 0, 0, 0, 0, 0, 4096, 0 }, shape.CameraMatrix.Rotation);
        Assert.Equal((110, -27, 303), (shape.CameraMatrix.X, shape.CameraMatrix.Y, shape.CameraMatrix.Z));
        Assert.Equal(new(0, 1, -20), shape.Vertices[1]);
        Assert.Equal(new(-7, 2, -14), shape.Vertices[2]);
        Assert.Equal(new(-10, 3, 0), shape.Vertices[3]);
        Assert.Equal(new(0, 9, -20), shape.Vertices[9]);
        NativeBattleShadowGeometry legacy = NativeBattlePoseMatrices.ShadowGeometry5088A0(model, bounds, Identity(), source, false);
        Assert.Equal((short)-5, legacy.Vertices[3].X);
        Assert.Equal((short)-10, legacy.Vertices[1].Z);
        Assert.Equal((short)0, source.Vertices[3].X);
        Assert.Throws<NotSupportedException>(() => NativeBattlePoseMatrices.ShadowGeometry5088A0(model with { GeometryWorldMatrix = null }, bounds, Identity(), source, true));
    }

    [Fact]
    public void Native_model_bounds_include_positive_bone_endpoints_and_the_root_margin()
    {
        byte[] skeleton = new byte[112];
        skeleton[0] = 2;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(18), 100);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(66), 2000);
        NativeBattlePoseSource source = new(skeleton.ToImmutableArray(), [1]);
        NativeBattleDecodedPose pose = new(new(1, -3, 2), [new(default, new(1024, 1024, 1024)), new(default, new(1024, 1024, 1024))], false);
        NativeBattleEffectLocalBounds bounds = NativeBattlePoseMatrices.ShadowBounds5086F0(source, pose);
        Assert.Equal(new(-484, -48, -468), bounds.Minimum);
        Assert.Equal(new(516, -48, 2132), bounds.Maximum);
        Assert.Throws<InvalidDataException>(() => NativeBattlePoseMatrices.ShadowBounds5086F0(source, pose with { Bones = [] }));
    }

    private static NativeBattleActionMatrix Identity() => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);

    private static NativeBattleModuleProjectedVertex[] Projected()
    {
        NativeBattleModuleProjectedVertex Point(short x, short y) => new(unchecked((ushort)x) | unchecked((uint)y << 16), 1003, 0);
        return [Point(0, 0), Point(-8, -8), Point(8, -8), Point(16, 0), Point(8, 8), Point(0, 16), Point(-8, 8), Point(-16, 0), Point(-8, -8), Point(-8, -8)];
    }
}
