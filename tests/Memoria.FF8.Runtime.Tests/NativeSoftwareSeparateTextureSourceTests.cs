using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeSoftwareSeparateTextureSourceTests
{
    [Theory]
    [InlineData(0, 0x1234, 0x4567)]
    [InlineData(1, 0x1234, 0x4567)]
    [InlineData(2, 0x00E3, 0x007C)]
    public void Textured_span_reads_only_the_selected_texture_image(int depth, ushort first, ushort second)
    {
        byte[] textureImage = new byte[32];
        textureImage[8] = depth switch { 0 => 0x21, 1 => 1, _ => 0xE3 };
        textureImage[9] = depth switch { 1 => 2, 2 => 0x7C, _ => 0 };
        Put(textureImage, 18, 0x1234);
        Put(textureImage, 20, 0x4567);
        byte[] before = textureImage.ToArray();
        byte[] framebuffer = new byte[32];
        framebuffer[8] = 3;
        framebuffer[9] = 4;
        Put(framebuffer, 18, 0x5555);
        Put(framebuffer, 20, 0x6666);
        Put(framebuffer, 22, 0x7777);
        Put(framebuffer, 24, 0x7FFF);
        Put(framebuffer, 4, 0xBEEF);

        NativeSoftwareVramSpan.WriteTextured(framebuffer, textureImage, 0, 0, depth == 2 ? 1 : 2, 0, 1024, 0, 0, 8, 16, 0, 0, depth);

        Assert.Equal(first, Word(framebuffer, 0));
        Assert.Equal(second, Word(framebuffer, 2));
        Assert.Equal((ushort)0xBEEF, Word(framebuffer, 4));
        Assert.Equal(before, textureImage);
    }

    [Theory]
    [InlineData(1, 0x0424, 0xBEEF)]
    [InlineData(2, 0x0402, 0x3ECE)]
    public void Blended_span_reads_existing_framebuffer_words(int variant, ushort expected, ushort end)
    {
        byte[] textureImage = new byte[10];
        textureImage[4] = 1;
        Put(textureImage, 8, 0x0021);
        Put(textureImage, 0, 0x7FFF);
        byte[] before = textureImage.ToArray();
        byte[] framebuffer = new byte[4];
        Put(framebuffer, 0, 0x0403);
        Put(framebuffer, 2, 0xBEEF);

        NativeSoftwareVramSpan.WriteTextured(framebuffer, textureImage, 0, 0, 1, 0, 0, 0, 0, 4, 6, 4, (byte)variant, 1);

        Assert.Equal(expected, Word(framebuffer, 0));
        Assert.Equal(end, Word(framebuffer, 2));
        Assert.Equal(before, textureImage);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    public void Bounds_follow_each_actual_buffer_for_texel_clut_and_destination(int depth, int right)
    {
        byte[] textureImage = [1, 0, 0, 0, 0x34, 0x12];
        byte[] framebuffer = new byte[4];
        NativeSoftwareVramSpan.WriteTextured(framebuffer, textureImage, 2, 0, right, 0, 0, 0, 0, 0, 2, 0, 0, depth);
        Assert.Equal((ushort)0x1234, Word(framebuffer, 2));

        Assert.Throws<NotSupportedException>(() => NativeSoftwareVramSpan.WriteTextured(new byte[64], textureImage, 2, 0, right, 0, 0, 0, 0, 6, 2, 0, 0, depth));
        Assert.Throws<NotSupportedException>(() => NativeSoftwareVramSpan.WriteTextured(new byte[64], textureImage, 2, 0, right, 0, 0, 0, 0, 0, 3, 0, 0, depth));
        Assert.Throws<NotSupportedException>(() => NativeSoftwareVramSpan.WriteTextured(new byte[3], textureImage, 2, 0, right, 0, 0, 0, 0, 0, 2, 0, 0, depth));
    }

    [Fact]
    public void DrawPolygon_samples_the_supplied_texture_image_through_the_full_triangle_path()
    {
        NativeBattleSoftwarePrimitiveState state = NativeBattleSoftwarePrimitiveState.FromInitializedRenderer();
        NativeSoftwareDrawingEnvironmentState environment = NativeSoftwareDrawingEnvironment.Recalculate45B6F0(state.Environment with { TopLeftX = 0, TopLeftY = 0, RightX = 7, BottomY = 7, OffsetX = 0, OffsetY = 0 });
        state = state with { Environment = environment };
        uint[] handlers = new uint[256];
        handlers[0x24] = 0x4669F0;
        NativeSoftwarePrimitiveTable table = new(handlers.ToImmutableArray());
        NativeBattleLogicalCommand command = new(new("test", 0, 0), 1, 0x24, [0x240000FF, 0x00040000, 0x01800000, 0x00040004, 0x00800000, 0x00060000, 0], 1, [], null);
        byte[] framebuffer = new byte[16384];
        byte[] textureImage = new byte[12294];
        framebuffer[0] = 2;
        Put(framebuffer, 12292, 0x7777);
        textureImage[0] = 1;
        Put(textureImage, 12290, 0x801F);
        byte[] before = textureImage.ToArray();

        NativeBattleSoftwarePrimitiveState drawn = state.DrawPolygon(framebuffer, textureImage, command, table);

        Assert.NotNull(drawn.Raster.PublishedClippedDescriptor);
        Assert.Equal((ushort)0x801F, Word(framebuffer, 4 * 2048));
        Assert.Equal((ushort)0x7777, Word(framebuffer, 12292));
        Assert.Equal(before, textureImage);
    }

    private static ushort Word(byte[] image, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(offset, 2));
    private static void Put(byte[] image, int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(offset, 2), value);
}
