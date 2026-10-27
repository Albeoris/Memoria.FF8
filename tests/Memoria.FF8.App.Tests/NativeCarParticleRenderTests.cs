using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeCarParticleRenderTests
{
    [Theory]
    [InlineData((byte)2, (byte)8, (byte)170, (byte)170, (byte)170)]
    [InlineData((byte)3, (byte)4, (byte)120, (byte)100, (byte)100)]
    public void OriginalUntexturedCarParticlesRenderWithoutTextureLookupAndWrapWithTheWorld(byte type, byte lifetime, byte red, byte green, byte blue)
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        NativeWorldForestEffect effect = assets.ForestTables.GetEffect(type);
        Assert.Equal((byte)1, effect.DrawMode);
        Assert.Equal((red, green, blue), (effect.Red, effect.Green, effect.Blue));
        Assert.Equal((byte)0, effect.Width);
        Assert.Equal((byte)0, effect.Height);
        WorldTerrainRenderer renderer = new(assets, 320, 224);
        Vector3 position = new(18381, 4000, -68272);
        WorldCamera camera = WorldCamera.Foot(0, 4096);
        byte[] clear = renderer.Render(position, 0, 0, 0, camera, avatarVisible: false).ToArray();
        NativeWorldForestParticle particle = new(0, type, 18381, -4300, 68272, 0, 0, 4096, 1, lifetime, true) { Motion = new(0, 0, 0, 0) };
        NativeWorldForestState forest = NativeWorldForestState.Create(0, 0) with { Particles = [particle] };
        byte[] visible = renderer.Render(position, 0, 0, 0, camera, avatarVisible: false, forest: forest).ToArray();
        Assert.NotEqual(clear, visible);
        NativeWorldForestState wrapped = forest with { Particles = [particle with { X = particle.X + 262144, GraphicsZ = particle.GraphicsZ - 196608 }] };
        Assert.Equal(visible, renderer.Render(position, 0, 0, 0, camera, avatarVisible: false, forest: wrapped));
    }
}
