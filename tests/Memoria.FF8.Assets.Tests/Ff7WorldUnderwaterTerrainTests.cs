using System.Numerics;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Assets.Tests;

public sealed class Ff7WorldUnderwaterTerrainTests
{
    [Fact]
    public void UnderwaterModelsUseBlueAmbientAndNativeColorWeightWithoutChangingOpacity()
    {
        Assert.Equal(new Vector4(32 / 255f, 32 / 255f, 48 / 255f, 0.25f), Ff7WorldLighting.Character(Vector3.Zero, new(1, 1, 1, 0.25f), underwater: true));
        Assert.Equal(255, Ff7WorldLighting.UnderwaterModelWeight(16000));
        Assert.Equal(254, Ff7WorldLighting.UnderwaterModelWeight(16032));
        Assert.Equal(0, Ff7WorldLighting.UnderwaterModelWeight(24160));
        Assert.Equal(255, Ff7WorldLighting.UnderwaterModelWeight(-100));
        Assert.Equal(new Vector4(50 / 255f, 25 / 255f, 100 / 255f, 0.25f), Ff7WorldLighting.BlendModelColor(new(100 / 255f, 50 / 255f, 200 / 255f, 0.25f), 128));
        Assert.Equal(new Vector4(0, 0, 0, 0.25f), Ff7WorldLighting.BlendModelColor(new(1, 1, 1, 0.25f), 0));
    }

    [Fact]
    public void NativePaletteRetainsRedGreenAndDelayedBlueChannels()
    {
        Assert.Equal(new Vector3(187, 187, 255), Ff7WorldUnderwaterTerrain.Palette(0));
        Assert.Equal(new Vector3(179, 183, 255), Ff7WorldUnderwaterTerrain.Palette(4));
        Assert.Equal(new Vector3(0, 59, 255), Ff7WorldUnderwaterTerrain.Palette(128));
        Assert.Equal(new Vector3(0, 0, 1), Ff7WorldUnderwaterTerrain.Palette(255));
    }

    [Fact]
    public void NativeEqualDepthSortPreservesTheThreeOriginalCornerPaletteOwners()
    {
        Vector3 depth = new(16000);
        Assert.Equal(new Vector4(21 / 255f, 22 / 255f, 32 / 255f, 1), Ff7WorldUnderwaterTerrain.Color(Vector3.Zero, depth, 0));
        Assert.Equal(new Vector4(22 / 255f, 23 / 255f, 32 / 255f, 1), Ff7WorldUnderwaterTerrain.Color(Vector3.Zero, depth, 1));
        Assert.Equal(new Vector4(23 / 255f, 23 / 255f, 32 / 255f, 1), Ff7WorldUnderwaterTerrain.Color(Vector3.Zero, depth, 2));
    }

    [Theory]
    [InlineData(16001, 19, 22, 32)]
    [InlineData(16511, 19, 22, 32)]
    [InlineData(16512, 15, 20, 32)]
    [InlineData(24192, 0, 0, 2)]
    public void NativeMaximumDepthSelectsQuantizedFogBandsAndTerminalBlue(int depth, int red, int green, int blue)
    {
        Vector4 expected = new(red / 255f, green / 255f, blue / 255f, 1);
        for (int corner = 0; corner < 3; corner++)
        {
            Assert.Equal(expected, Ff7WorldUnderwaterTerrain.Color(Vector3.Zero, new(10000, depth, 10001), corner));
        }
    }
}
