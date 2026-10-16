using System.Collections.Immutable;
using System.Buffers.Binary;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattlePrimitiveFrameRenderingTests
{
    [Fact]
    public void Empty_original_draw_environment_is_black_and_does_not_require_a_fabricated_camera()
    {
        BattleRenderer renderer = new(320, 216);
        byte[] pixels = renderer.RenderNativePrimitiveFrame(NativeBattlePrimitiveFrame.Clear(), null);
        Assert.All(Enumerable.Range(0, 320 * 216), index => Assert.Equal(new byte[] { 0, 0, 0, 255 }, pixels.AsSpan(index * 4, 4).ToArray()));
        Assert.False(renderer.TryProject(default, out _));
    }

    [Fact]
    public void Opaque_original_packets_use_less_equal_depth_independently_of_their_submission_order()
    {
        BattleRenderer renderer = new(320, 216);
        NativeBattleLogicalResolvedCommand near = Quad(0x28, 0x000064, 100, 1);
        NativeBattleLogicalResolvedCommand far = Quad(0x28, 0x640000, 200, 1);
        byte[] forward = renderer.RenderNativePrimitiveFrame(Frame([far, near]), null).ToArray();
        byte[] reverse = renderer.RenderNativePrimitiveFrame(Frame([near, far]), null).ToArray();
        Assert.Equal(forward, reverse);
        Assert.Equal(new byte[] { 100, 0, 0, 255 }, forward.AsSpan(4 * (20 * 320 + 20), 4).ToArray());
    }

    [Fact]
    public void Semi_quad_shared_diagonal_is_blended_once_and_does_not_write_depth()
    {
        BattleRenderer renderer = new(320, 216);
        NativeBattleLogicalResolvedCommand first = Quad(0x2A, 0xFFFFFF, 50, 2);
        NativeBattleLogicalResolvedCommand behind = Quad(0x2A, 0xFFFFFF, 100, 2);
        byte[] once = renderer.RenderNativePrimitiveFrame(Frame([first]), null).ToArray();
        byte[] twice = renderer.RenderNativePrimitiveFrame(Frame([first, behind]), null).ToArray();
        int diagonal = 4 * (20 * 320 + 20);
        Assert.Equal(127, once[diagonal]);
        Assert.InRange(twice[diagonal], (byte)190, (byte)191);
        Assert.Equal(once[diagonal], once[diagonal + 4]);
    }

    [Fact]
    public void Whole_pixel_and_eighth_pixel_proxy_coordinates_produce_the_same_original_polygon()
    {
        BattleRenderer renderer = new(320, 216);
        NativeBattleLogicalResolvedCommand whole = Quad(0x28, 0x00FF00, 100, 1);
        NativeBattleLogicalResolvedCommand eighth = whole with { Command = whole.Command with { Flags20 = 0, Words = [0x2800FF00, Point(80, 80), Point(240, 80), Point(80, 240), Point(240, 240)] } };
        Assert.Equal(renderer.RenderNativePrimitiveFrame(Frame([whole]), null).ToArray(), renderer.RenderNativePrimitiveFrame(Frame([eighth]), null));
    }

    [Fact]
    public void Old_snapshot_unknown_descriptor_or_depth_is_rejected_at_the_actual_polygon_read()
    {
        BattleRenderer renderer = new(320, 216);
        NativeBattleLogicalResolvedCommand quad = Quad(0x28, 0xFFFFFF, 100, 1);
        NativeBattlePrimitiveFrame frame = Frame([quad with { Command = quad.Command with { VertexPayload4 = [1000, 1000, 1000, 1000] } }]);
        Assert.Throws<NotSupportedException>(() => renderer.RenderNativePrimitiveFrame(frame with { LastDescriptorPerspective = null }, null));
        Assert.Throws<NotSupportedException>(() => renderer.RenderNativePrimitiveFrame(frame with { LastDepthState = null }, null));
        Assert.Throws<NotSupportedException>(() => renderer.RenderNativePrimitiveFrame(frame with { LastColorState = null }, null));
    }

    [Fact]
    public void Native_PC_semi_textures_are_admitted_by_the_command_pass_and_do_not_split_by_STP()
    {
        BattleRenderer renderer = new(320, 216);
        NativeBattleLogicalResolvedCommand background = Quad(0x28, 10, 100, 1);
        NativeBattleLogicalCommand textured = new(new("off_B6D070", 0, 20), 1, 0x2E, [0x2E808080, Point(10, 10), 0x40000000, Point(30, 10), 0x00200000, Point(10, 30), 0, Point(30, 30), 0], 1, [], null) { OrderingBucket = 50 };
        NativeBattleLogicalResolvedCommand effect = new(textured, new(0x20), 2);
        byte[] first = renderer.RenderNativePrimitiveFrame(Frame([background, effect]), Texture(3)).ToArray();
        byte[] second = renderer.RenderNativePrimitiveFrame(Frame([background, effect]), Texture(0x8003)).ToArray();
        Assert.Equal(first, second);
        Assert.Equal(34, first[4 * (20 * 320 + 20)]);
    }

    [Theory]
    [InlineData(0, 75)]
    [InlineData(1, 87)]
    public void Native_PC_ABR2_uses_its_alternate_palette_alpha_instead_of_subtracting_the_texel(byte shift, byte expectedRed)
    {
        BattleRenderer renderer = new(320, 216);
        NativeBattleLogicalResolvedCommand background = Quad(0x28, 100, 100, 1);
        NativeBattleLogicalCommand textured = new(new("off_B6D070", 0, 20), 1, 0x2E, [0x2E808080, Point(10, 10), 0x40000000, Point(30, 10), 0x00400000, Point(10, 30), 0, Point(30, 30), 0], 1, [], null) { OrderingBucket = 50 };
        NativeBattlePrimitiveFrame frame = Frame([background, new(textured, new(0x40), 2)]);
        frame = frame with { LastColorState = frame.LastColorState! with { AlternatePaletteShift = shift } };
        byte[] pixels = renderer.RenderNativePrimitiveFrame(frame, Texture(31));
        Assert.Equal(expectedRed, pixels[4 * (20 * 320 + 20)]);
        Assert.Throws<NotSupportedException>(() => renderer.RenderNativePrimitiveFrame(frame with { LastColorState = frame.LastColorState with { AlternatePaletteShift = null } }, Texture(31)));
    }

    [Theory]
    [InlineData(0, 62)]
    [InlineData(1, 124)]
    [InlineData(2, 97)]
    [InlineData(3, 102)]
    public void All_four_PC_material_modes_use_original_diffuse_palette_and_primary_blend_factors(int mode, byte expectedRed)
    {
        //Mode3 contributes24*64*127/(255*255), which is just below3 before the final integer framebuffer write.
        BattleRenderer renderer = new(320, 216);
        byte[] pixels = renderer.RenderNativePrimitiveFrame(Frame([Quad(0x28, 100, 100, 1), TexturedQuad(mode)]), Texture(3));
        Assert.Equal(expectedRed, pixels[4 * (20 * 320 + 20)]);
    }

    [Fact]
    public void Variable_TILE_uses_original_XY_WH_and_covers_each_pixel_of_its_rectangle_once()
    {
        BattleRenderer renderer = new(320, 216);
        NativeBattleLogicalCommand tile = new(new("off_B6D070", 0, 0), 1, 0x62, [0x62000064, Point(10, 20), Point(20, 30)], 1, [], null) { OrderingBucket = 17 };
        byte[] pixels = renderer.RenderNativePrimitiveFrame(Frame([new(tile, new(0), 2)]), null);
        Assert.Equal(49, pixels[4 * (20 * 320 + 10)]);
        Assert.Equal(49, pixels[4 * (49 * 320 + 29)]);
        Assert.Equal(0, pixels[4 * (19 * 320 + 10)]);
        Assert.Equal(0, pixels[4 * (20 * 320 + 30)]);
        Assert.Equal(600, Enumerable.Range(0, 320 * 216).Count(index => pixels[index * 4] != 0));
    }

    [Fact]
    public void Gouraud_triangle_interpolates_the_original_per_corner_PC_diffuse_colors()
    {
        BattleRenderer renderer = new(320, 216);
        NativeBattleLogicalCommand triangle = new(new("off_B6D070", 0, 0), 1, 0x30, [0x300000FF, Point(10, 10), 0x0000FF00, Point(30, 10), 0x00FF0000, Point(10, 30)], 1, [], null) { OrderingBucket = 100 };
        byte[] pixels = renderer.RenderNativePrimitiveFrame(Frame([new(triangle, new(0), 1)]), null);
        Assert.Equal(new byte[] { 114, 70, 70, 255 }, pixels.AsSpan(4 * (15 * 320 + 15), 4).ToArray());
    }

    [Fact]
    public void Material_sampling_rejects_unobserved_VRAM_or_inherited_ABR_at_the_actual_read()
    {
        BattleRenderer renderer = new(320, 216);
        Assert.Throws<NotSupportedException>(() => renderer.RenderNativePrimitiveFrame(Frame([TexturedQuad(1)]), null));
        NativeBattleLogicalResolvedCommand flat = Quad(0x2A, 0xFFFFFF, 100, 2);
        Assert.Throws<NotSupportedException>(() => renderer.RenderNativePrimitiveFrame(Frame([flat with { DrawMode = new(null) }]), null));
    }

    [Theory]
    [InlineData(0x03F8, 100)]
    [InlineData(0x03F9, 0)]
    public void Opaque_alternate_palette_respects_original_GREATEREQUAL112_alpha_test(ushort color, byte expectedRed)
    {
        BattleRenderer renderer = new(320, 216);
        NativeBattleLogicalResolvedCommand texture = TexturedQuad(2);
        texture = texture with { Pass = 1, Command = texture.Command with { Opcode = 0x2C, Words = texture.Command.Words.SetItem(0, 0x2C808080) } };
        byte[] pixels = renderer.RenderNativePrimitiveFrame(Frame([Quad(0x28, 100, 100, 1), texture]), Texture(color));
        Assert.Equal(expectedRed, pixels[4 * (20 * 320 + 20)]);
    }

    private static NativeBattlePrimitiveFrame Frame(ImmutableArray<NativeBattleLogicalResolvedCommand> commands) => new(NativeBattleLogicalOrdering.Clear(), commands, []) { LastDescriptorPerspective = false, LastDepthState = NativeBattlePrimitiveDepthState.FromColdProcess(), LastColorState = NativeBattlePrimitiveColorState.FromColdProcess() };

    private static NativeBattleLogicalResolvedCommand Quad(byte opcode, uint color, int bucket, byte pass)
    {
        NativeBattleLogicalCommand command = new(new("off_B6D070", 0, 0), 1, opcode, [(uint)opcode << 24 | color, Point(10, 10), Point(30, 10), Point(10, 30), Point(30, 30)], 1, [], null) { OrderingBucket = bucket };
        return new(command, new(0), pass);
    }

    private static uint Point(int x, int y) => (ushort)x | unchecked((uint)y << 16);

    private static NativeBattleLogicalResolvedCommand TexturedQuad(int mode)
    {
        ushort page = checked((ushort)(mode << 5));
        NativeBattleLogicalCommand command = new(new("off_B6D070", 0, 20), 1, 0x2E, [0x2E808080, Point(10, 10), 0x40000000, Point(30, 10), (uint)page << 16, Point(10, 30), 0, Point(30, 30), 0], 1, [], null) { OrderingBucket = 50 };
        return new(command, new(page), 2);
    }

    private static NativeBattleEffectTextureState Texture(ushort color)
    {
        byte[] palette = new byte[4];
        BinaryPrimitives.WriteUInt16LittleEndian(palette.AsSpan(2), color);
        return NativeBattleEffectTextureState.FromColdProcess() with { VramRegions = [new(0, ImmutableArray.Create((byte)0x11, (byte)0x11)), new(2 * 256 * 1024, palette.ToImmutableArray())] };
    }
}
