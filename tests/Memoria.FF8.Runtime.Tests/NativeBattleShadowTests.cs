using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShadowTests
{
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
        Assert.Empty(packets[0].VertexPayload4);
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
