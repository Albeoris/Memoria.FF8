using System.Buffers.Binary;
using Albeoris.Games.FF8.Imaging;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeFieldParticleResourcesTests
{
    [Fact]
    public void TextureUsesNativeNibbleOrderPaletteTransparencyAndByteWrappedCoordinates()
    {
        byte[] texture = new byte[516 + 128 * 2];
        BinaryPrimitives.WriteInt16LittleEndian(texture, 254);
        BinaryPrimitives.WriteUInt16LittleEndian(texture.AsSpan(6), 0x001F);
        BinaryPrimitives.WriteUInt16LittleEndian(texture.AsSpan(8), 0x83E0);
        texture[516] = 0x21;
        texture[516 + 127] = 0x10;
        texture[516 + 128] = 0x02;
        NativeFieldParticleResources resources = new(new byte[10016], texture);
        PixelImage sprite = resources.DecodeSprite(0, 254, 2, 2);
        Assert.Equal(new Rgba32(255, 0, 0), sprite[0, 0]);
        Assert.Equal(new Rgba32(0, 255, 0, 128), sprite[1, 0]);
        Assert.Equal(new Rgba32(0, 255, 0, 128), sprite[0, 1]);
        Assert.Equal(default, sprite[1, 1]);
        PixelImage wrapped = resources.DecodeSprite(255, 254, 2, 3);
        Assert.Equal(new Rgba32(255, 0, 0), wrapped[0, 0]);
        Assert.Equal(new Rgba32(255, 0, 0), wrapped[1, 0]);
        Assert.Equal(default, wrapped[0, 2]);
        Assert.Equal(default, wrapped[1, 2]);
    }

    [Fact]
    public void TruncatedDefinitionsAndInvalidTextureUploadExtentsRefuse()
    {
        byte[] texture = new byte[516];
        BinaryPrimitives.WriteInt16LittleEndian(texture, 256);
        Assert.Throws<InvalidDataException>(() => new NativeFieldParticleResources(new byte[10015], texture));
        BinaryPrimitives.WriteInt16LittleEndian(texture, 255);
        Assert.Throws<InvalidDataException>(() => new NativeFieldParticleResources(new byte[10016], texture));
        BinaryPrimitives.WriteInt16LittleEndian(texture, 254);
        Assert.Throws<InvalidDataException>(() => new NativeFieldParticleResources(new byte[10016], texture));
    }
}
