using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDoomtrainSkyPacketTests
{
    [Theory]
    [InlineData(2, 0x24030201u)]
    [InlineData(3, 0x2C030201u)]
    public void Uniform_flat_sky_command_overrides_source_RGB_and_opcode_before_near_rejection(int group, uint expected)
    {
        byte[] face = Face(group);
        BinaryPrimitives.WriteUInt32LittleEndian(face, 0xDEADBEEF);
        NativeBattleModuleProjectedVertex[] points = Vertices();
        points[0] = points[0] with { Flags = 0x60000 };
        NativeBattleGfGeometryAttempt result = Emit(face, points.Take((group & 1) == 0 ? 3 : 4).ToArray(), group, new(0x030201, 0xF0A040, true, true));
        Assert.Null(result.Primitive);
        Assert.Equal(expected, BinaryPrimitives.ReadUInt32LittleEndian(result.Data.AsSpan().Slice(4)));
        Assert.All(result.WriteMask.AsSpan().Slice(4, 4).ToArray(), value => Assert.Equal(NativeBattleGfPacketByteState.Known, value));
    }

    [Theory]
    [InlineData(6)]
    [InlineData(7)]
    public void Rejected_Gouraud_sky_face_preserves_the_actual_packet_color_bytes_instead_of_publishing_context_scratch(int group)
    {
        NativeBattleModuleProjectedVertex[] points = Vertices();
        points[0] = points[0] with { Flags = 0x60000 };
        IReadOnlyList<NativeBattleModuleProjectedVertex> corners = points.Take(group == 6 ? 3 : 4).ToArray();
        NativeBattleGfGeometryAttempt sky = Emit(Face(group), corners, group, new(null, 0x40A0F0, true, true));
        NativeBattleGfGeometryAttempt ordinary = Emit(Face(group), corners, group, null);
        Assert.Null(sky.Primitive);
        Assert.All(sky.WriteMask.AsSpan().Slice(4, 4).ToArray(), value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
        Assert.All(ordinary.WriteMask.AsSpan().Slice(4, 4).ToArray(), value => Assert.Equal(NativeBattleGfPacketByteState.Known, value));
        Assert.Equal(ordinary.Data.AsSpan().Slice(12, 4).ToArray(), sky.Data.AsSpan().Slice(12, 4).ToArray());
    }

    [Theory]
    [InlineData(6)]
    [InlineData(7)]
    public void Gouraud_sky_modulation_wraps_each_byte_before_the_existing_depth_cue_and_preserves_command_code(int group)
    {
        NativeBattleGfGeometryAttempt result = Emit(Face(group), Vertices().Take(group == 6 ? 3 : 4).ToArray(), group, new(null, 0x40A0F0, true, true), 2048);
        Assert.NotNull(result.Primitive);
        Assert.Equal((group == 6 ? 0x34000000u : 0x3C000000u) | 0x206434u, result.Primitive.Words[1]);
        Assert.Equal(4126 + ((group == 6 ? 255 : 256) >> 6), result.Primitive.Bucket);
    }

    [Fact]
    public void Zero_area_sky_quad_rejects_before_reading_fourth_vertex_while_the_existing_default_path_is_unchanged()
    {
        NativeBattleModuleProjectedVertex[] points = [Point(100, 100), Point(200, 100), Point(300, 100)];
        NativeBattleGfGeometryPackets.ProjectedCorners corners = new([0, 1, 2, 3], index => index == 3 ? throw new InvalidOperationException("Fourth vertex was consumed.") : points[index]);
        NativeBattleGfGeometryAttempt sky = Emit(Face(3), corners, 3, new(0x808080, null, true, true));
        Assert.Null(sky.Primitive);
        Assert.All(sky.WriteMask.AsSpan().Slice(32, 4).ToArray(), value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
        Assert.Throws<InvalidOperationException>(() => Emit(Face(3), corners, 3, null));
    }

    private static NativeBattleGfGeometryAttempt Emit(byte[] face, IReadOnlyList<NativeBattleModuleProjectedVertex> points, int group, NativeBattleMeshPacketVariant? variant, int fade = 0)
    {
        return NativeBattleGfGeometryPackets.EmitAttempt(face, points, group, new(0xC0, 0, fade, null, null, 0, 0, 4126, 6), NativeBattleGteColorInterpolation.CodeConstants45F270, variant: variant);
    }

    private static byte[] Face(int group)
    {
        int[] lengths = [12, 12, 20, 24, 20, 24, 28, 36];
        byte[] face = new byte[lengths[group]];
        uint command = group switch { 2 => 0x2480A0C0, 3 => 0x2C80A0C0, 6 => 0x3480A0C0, 7 => 0x3C80A0C0, _ => throw new ArgumentOutOfRangeException(nameof(group)) };
        BinaryPrimitives.WriteUInt32LittleEndian(face, command);
        if ((group & 4) != 0)
        {
            int offset = (group & 1) == 0 ? 20 : 24;
            for (int corner = 1; corner < ((group & 1) == 0 ? 3 : 4); corner++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(offset + 4 * (corner - 1)), 0x00A0C0E0);
            }
        }
        return face;
    }

    private static NativeBattleModuleProjectedVertex[] Vertices() => [Point(100, 100), Point(200, 100), Point(100, 200), Point(200, 200)];
    private static NativeBattleModuleProjectedVertex Point(ushort x, ushort y) => new(x | (uint)y << 16, 1024, 0);
}
