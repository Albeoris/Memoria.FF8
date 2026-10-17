using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleModuleFlatQuadTests
{
    [Fact]
    public void Flat_quad_preserves_all_four_precision_depths_and_applies_semitransparency_then_opaque_override()
    {
        byte[] face = new byte[12];
        BinaryPrimitives.WriteUInt32LittleEndian(face, 0x28102030);
        NativeBattleProjectedVertex[] vertices = Vertices();
        NativeBattleGeometryPrimitive primitive = NativeBattleModulePackets.EmitFlatQuad572500(face, vertices, 1, 0, 17, 2)!;
        Assert.Equal(0x2A102030u, primitive.Words[1]);
        Assert.Equal(0x05000000u, primitive.Words[0]);
        Assert.Equal(new[] { 1000, 2000, 3000, 4000 }, primitive.VertexPayload4.ToArray());
        Assert.Equal(17 + ((10000 >> 4) >> 2), primitive.Bucket);
        Assert.Equal(0x28102030u, NativeBattleModulePackets.EmitFlatQuad572500(face, vertices, 5, 0, 17, 2)!.Words[1]);
    }

    [Fact]
    public void Far_color_uses_original45DD60_sixteen_times_RGB_and_signed_half_fade_without_changing_opcode()
    {
        byte[] face = new byte[12];
        BinaryPrimitives.WriteUInt32LittleEndian(face, 0x28102030);
        NativeBattleGeometryPrimitive primitive = NativeBattleModulePackets.EmitFlatQuad572500(face, Vertices(), 0x40, 2048, 17, 2, 0x7090B0)!;
        Assert.Equal(0x28405870u, primitive.Words[1]);
        Assert.Equal(0x28081018u, NativeBattleModulePackets.EmitFlatQuad572500(face, Vertices(), 0x40, 2048, 17, 2)!.Words[1]);
    }

    [Fact]
    public void Original_backface_flag_and_first_three_near_checks_do_not_invent_a_fourth_vertex_near_rejection()
    {
        byte[] face = new byte[12];
        BinaryPrimitives.WriteUInt32LittleEndian(face, 0x28102030);
        NativeBattleProjectedVertex[] vertices = Vertices();
        (vertices[1], vertices[2]) = (vertices[2], vertices[1]);
        Assert.Null(NativeBattleModulePackets.EmitFlatQuad572500(face, vertices, 0, 0, 17, 2));
        Assert.NotNull(NativeBattleModulePackets.EmitFlatQuad572500(face, vertices, 0x10, 0, 17, 2));
        vertices[3] = vertices[3] with { DepthAndClip = 0x60000 | 4000 };
        Assert.NotNull(NativeBattleModulePackets.EmitFlatQuad572500(face, vertices, 0x10, 0, 17, 2));
        vertices[0] = vertices[0] with { DepthAndClip = 0x60000 | 1000 };
        Assert.Null(NativeBattleModulePackets.EmitFlatQuad572500(face, vertices, 0x10, 0, 17, 2));
    }

    private static NativeBattleProjectedVertex[] Vertices() => [new(Screen(0, 0), 1000), new(Screen(64, 0), 2000), new(Screen(0, 64), 3000), new(Screen(64, 64), 4000)];
    private static uint Screen(short x, short y) => unchecked((ushort)x) | unchecked((uint)y << 16);
}
