using Memoria.FF8.Mods;

namespace Memoria.FF8.Mods.Tests;

public sealed class DemasterTextureFingerprintTests
{
    [Theory]
    [InlineData(1, 1, 3, 0, 0x2319C5BD0068FC81UL, 0x0495AB8CF8753FF9UL)]
    [InlineData(1, 4, 4, 0, 0xD78F678286117466UL, 0x09ED9F3147A016FAUL)]
    [InlineData(17, 1, 3, 0, 0x37020320943D4046UL, 0x2E192A5566D80DC7UL)]
    [InlineData(20, 4, 3, 0, 0x8E393BC26BFF68D5UL, 0x465BD3C40DA6CE34UL)]
    [InlineData(21, 4, 3, 0, 0x0613346E47F39229UL, 0x54337E19A12B02B0UL)]
    [InlineData(1023, 1, 4, 0, 0xE8B3C8815AA2164CUL, 0x6D0C29EF24BA2965UL)]
    [InlineData(256, 256, 3, 5, 0x2D22C26FA15E9774UL, 0x4B2DB01020C2F559UL)]
    [InlineData(256, 256, 3, 0, 0x4E959FE2136A006EUL, 0x9E902B4A80C9694CUL)]
    [InlineData(256, 256, 4, 5, 0x4E959FE2136A006EUL, 0x9E902B4A80C9694CUL)]
    [InlineData(256, 256, 4, 0, 0xB0EB04097701B0D5UL, 0x437FDE7DD93272C1UL)]
    public void SeededHighAndLowHalvesMatchIndependentReference(int width, int height, int bytesPerPixel, int nativeMode, ulong expectedHigh, ulong expectedLow)
    {
        Assert.True(DemasterTextureFingerprint.TryCalculate(Pixels(width * height * bytesPerPixel), width, height, bytesPerPixel, nativeMode, out DemasterTextureFingerprint fingerprint));
        Assert.Equal(new DemasterTextureFingerprint(expectedHigh, expectedLow), fingerprint);
    }

    [Theory]
    [InlineData(0, 1, 4)]
    [InlineData(1, 0, 4)]
    [InlineData(-1, 1, 4)]
    [InlineData(1, -1, 4)]
    [InlineData(1024, 1, 4)]
    [InlineData(1, 1024, 4)]
    [InlineData(int.MaxValue, int.MaxValue, 4)]
    [InlineData(1, 1, 0)]
    [InlineData(1, 1, 1)]
    [InlineData(1, 1, 2)]
    [InlineData(1, 1, 5)]
    public void UnsupportedUploadsDoNotProduceFingerprints(int width, int height, int bytesPerPixel)
    {
        Assert.False(DemasterTextureFingerprint.TryCalculate(new byte[4], width, height, bytesPerPixel, 0, out DemasterTextureFingerprint fingerprint));
        Assert.Equal(default, fingerprint);
    }

    [Fact]
    public void CompleteOriginalUploadIsRequiredEvenWhenOnlyItsTopRowsAreHashed()
    {
        Assert.False(DemasterTextureFingerprint.TryCalculate(default, 1, 1, 4, 0, out _));
        Assert.False(DemasterTextureFingerprint.TryCalculate(new byte[3], 1, 1, 4, 0, out _));
        Assert.False(DemasterTextureFingerprint.TryCalculate(new byte[256 * 192 * 4], 256, 256, 4, 5, out _));
    }

    [Fact]
    public void WorldMapCropAppliesOnlyToNativeModeFiveAndExactDimensions()
    {
        byte[] pixels = Pixels(256 * 256 * 4);
        Assert.True(DemasterTextureFingerprint.TryCalculate(pixels, 256, 256, 4, 5, out DemasterTextureFingerprint cropped));
        Assert.True(DemasterTextureFingerprint.TryCalculate(pixels, 256, 256, 4, 0, out DemasterTextureFingerprint full));
        Assert.NotEqual(full, cropped);
        pixels[256 * 192 * 4] ^= 255;
        Assert.True(DemasterTextureFingerprint.TryCalculate(pixels, 256, 256, 4, 5, out DemasterTextureFingerprint changedCrop));
        Assert.Equal(cropped, changedCrop);
        Assert.True(DemasterTextureFingerprint.TryCalculate(pixels, 256, 256, 4, 4, out DemasterTextureFingerprint changedFull));
        Assert.NotEqual(full, changedFull);
        Assert.True(DemasterTextureFingerprint.TryCalculate(pixels, 256, 255, 4, 5, out DemasterTextureFingerprint otherSize));
        Assert.True(DemasterTextureFingerprint.TryCalculate(pixels, 256, 255, 4, 0, out DemasterTextureFingerprint otherSizeStandard));
        Assert.Equal(otherSizeStandard, otherSize);
    }

    [Fact]
    public void HashUsesUploadByteOrderAndIgnoresOnlyBytesBeyondItsDeclaredExtent()
    {
        byte[] pixels = Pixels(8);
        Assert.True(DemasterTextureFingerprint.TryCalculate(pixels, 1, 1, 4, 0, out DemasterTextureFingerprint original));
        pixels[4] ^= 255;
        Assert.True(DemasterTextureFingerprint.TryCalculate(pixels, 1, 1, 4, 0, out DemasterTextureFingerprint extraBytes));
        Assert.Equal(original, extraBytes);
        (pixels[0], pixels[2]) = (pixels[2], pixels[0]);
        Assert.True(DemasterTextureFingerprint.TryCalculate(pixels, 1, 1, 4, 0, out DemasterTextureFingerprint swappedChannels));
        Assert.NotEqual(original, swappedChannels);
    }

    private static byte[] Pixels(int length)
    {
        byte[] pixels = new byte[length];
        for (int index = 0; index < pixels.Length; index++)
        {
            pixels[index] = (byte)(index * 37 + 11);
        }
        return pixels;
    }
}
