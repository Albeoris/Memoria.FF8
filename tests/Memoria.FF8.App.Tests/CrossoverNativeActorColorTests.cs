using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverNativeActorColorTests
{
    [Theory]
    [InlineData(0, TriangleBlend.Alpha, 127, 32, 64, 96)]
    [InlineData(1, TriangleBlend.Add, 255, 32, 64, 96)]
    [InlineData(2, TriangleBlend.Alpha, 70, 0, 0, 0)]
    [InlineData(3, TriangleBlend.AlphaAdd, 255, 8, 16, 24)]
    public void NativeSemiTransparentModelUsesOriginalPcDiffuseAndMaterialContract(int abr, TriangleBlend blend, int alpha, int red, int green, int blue)
    {
        CrossoverNativeActorColor color = new(0x02302010);
        var diffuse = color.Diffuse(abr);
        Assert.Equal(new Vector3(red, green, blue), diffuse.Rgb);
        Assert.Equal(alpha, diffuse.Alpha);
        PixelImage texture = new(1, 1);
        TriangleMaterial material = color.ApplyMaterial(new(texture, Sampler: TriangleSampler.PointClamp), abr);
        Assert.Same(texture, material.Texture);
        Assert.Equal(blend, material.Blend);
        Assert.False(material.DepthWrite);
        Assert.True(material.TextureSelectsAlpha);
        Assert.True(material.PreserveTargetAlpha);
        Assert.True(material.CentroidTextureSampling);
        Assert.Equal(abr == 0 ? 122f / 255 : 1f / 255, material.MinimumAlpha);
    }

    [Theory]
    [InlineData(0, TriangleBlend.Alpha)]
    [InlineData(1, TriangleBlend.Add)]
    [InlineData(2, TriangleBlend.Alpha)]
    [InlineData(3, TriangleBlend.AlphaAdd)]
    [InlineData(4, TriangleBlend.Alpha)]
    public void ForeignSemiTransparentModelUsesActualHostBlendAndRetainsPixelAndVertexAlpha(int mode, TriangleBlend blend)
    {
        CrossoverNativeActorColor color = new(0x02302010);
        PixelImage texture = new(1, 1);
        TriangleMaterial original = new(texture, MinimumAlpha: 0.25f, Sampler: TriangleSampler.LinearWrap) { PreserveTargetAlpha = false, TextureSelectsAlpha = false };
        TriangleMaterial projected = color.ApplyHostMaterial(original, mode);
        Assert.Equal(original with { Blend = blend, DepthWrite = false }, projected);
        Vector4 vertex = new(0.5f, 0.25f, 1, 0.5f);
        Vector4 mapped = color.ApplyHost(vertex, mode);
        Assert.Equal(vertex.W * (mode is 0 or 4 ? 127f / 255 : mode == 2 ? 70f / 255 : 1), mapped.W);
        Assert.Same(texture, projected.Texture);
    }

    [Fact]
    public void ForeignOpaqueColorRetainsActualMaterialAndNeutralRgb()
    {
        CrossoverNativeActorColor color = new(0x00808080);
        TriangleMaterial material = new(null, TriangleBlend.Subtract, TriangleDepthComparison.GreaterEqual, true, 0.2f, TriangleSampler.LinearWrap) { TextureSelectsAlpha = false };
        Assert.Equal(material, color.ApplyHostMaterial(material, 4));
        Assert.Equal(Vector4.One, color.ApplyHost(Vector4.One, 4));
    }
}
