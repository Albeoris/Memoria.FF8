using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeSoftwareTexturedSpanDispatchTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void Four_bit_opaque_tables_use_both_nibbles_and_an_inclusive_endpoint(byte variant)
    {
        byte[] image = new byte[4096];
        image[1024] = 0x21;
        Put(image, 2050, 0x9234);
        Put(image, 2052, 0x4567);
        Put(image, 4, 0xBEEF);
        NativeSoftwareVramSpan.WriteTextured(image, 0, 0, 2, 0, 1024, 0, 0, 1024, 2048, 4, variant, 0);
        Assert.Equal((ushort)0x9234, Word(image, 0));
        Assert.Equal((ushort)0x4567, Word(image, 2));
        Assert.Equal((ushort)0xBEEF, Word(image, 4));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void Direct_table_literally_loads_a_byte_then_writes_a_word(byte variant)
    {
        byte[] image = new byte[4096];
        image[1024] = 0xE3;
        image[1025] = 0x7C;
        NativeSoftwareVramSpan.WriteTextured(image, 0, 0, 1, 0, 1024, 0, 0, 1024, 0, 4, variant, 2);
        Assert.Equal((ushort)0xE3, Word(image, 0));
        Assert.Equal((ushort)0x7C, Word(image, 2));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Addition_saturates_each_five_bit_channel_and_excludes_the_endpoint(int depth)
    {
        byte[] image = Image(depth, 0x7FFF);
        Put(image, 0, 0x7FFF);
        Put(image, 2, 0xAAAA);
        NativeSoftwareVramSpan.WriteTextured(image, 0, 0, 1, 0, 0, 0, 0, 1024, 2048, 4, 1, depth);
        Assert.Equal((ushort)0x7FFF, Word(image, 0));
        Assert.Equal((ushort)0xAAAA, Word(image, 2));
    }

    [Theory]
    [InlineData(0, 0x4210, 0x7FFF, 0)]
    [InlineData(1, 0x7FFF, 0x4210, 0x3DEF)]
    [InlineData(2, 0x7FFF, 0x007F, 0x7F80)]
    public void Subtraction_clamps_channels_and_includes_the_endpoint(int depth, ushort previous, ushort source, ushort expected)
    {
        byte[] image = Image(depth, source);
        Put(image, 0, previous);
        NativeSoftwareVramSpan.WriteTextured(image, 0, 0, 0, 0, 0, 0, 0, 1024, 2048, 4, 2, depth);
        Assert.Equal(expected, Word(image, 0));
    }

    [Fact]
    public void Dispatch_without_blend_flag_ignores_the_unused_variant()
    {
        byte[] image = Image(0, 0x1234);
        NativeSoftwareVramSpan.WriteTextured(image, 0, 0, 0, 0, 0, 0, 0, 1024, 2048, 0, 255, 0);
        Assert.Equal((ushort)0x1234, Word(image, 0));
        Assert.Throws<NotSupportedException>(() => NativeSoftwareVramSpan.WriteTextured(image, 0, 0, 0, 0, 0, 0, 0, 1024, 2048, 4, 4, 0));
    }

    private static byte[] Image(int depth, ushort color)
    {
        byte[] image = new byte[4096];
        image[1024] = depth == 2 ? (byte)color : (byte)1;
        Put(image, 2050, color);
        return image;
    }

    private static ushort Word(byte[] image, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(offset, 2));
    private static void Put(byte[] image, int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(offset, 2), value);
}
