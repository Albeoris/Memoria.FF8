using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeWorldParticleRenderingTests
{
    [Theory]
    [InlineData((byte)4)]
    [InlineData((byte)5)]
    [InlineData((byte)17)]
    [InlineData((byte)18)]
    public void OriginalTexturedParticlesUseOpaqueAndStpPassesWithNativeProjection(byte type)
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        NativeWorldForestEffect effect = assets.ForestTables.GetEffect(type);
        Assert.NotEqual((byte)1, effect.DrawMode);
        PixelImage texture = assets.GetForestTexture(type, 0);
        Recorder recorder = new(texture);
        WorldTerrainRenderer renderer = new(assets, 320, 224) { TriangleRenderer = recorder };
        Vector3 position = new(18381, 4000, -68272);
        WorldCamera camera = WorldCamera.Foot(0, 4096) with { ParticleProjection = NativeWorldPathProjectionCertificate.CreateFoot(new NativeWorldFootCameraState(18381, -68272, -4000, 18381, -68272, -4256, 0, -112, -341, -7168)) };
        NativeWorldForestParticle particle = new(0, type, 18381, -4300, 68272, 0, 0, 4096, 0, effect.Lifetime, true) { Motion = type is 4 or 5 ? null : new(0, 0, 0, 0) };
        NativeWorldForestState forest = NativeWorldForestState.Create(0, 0) with { Particles = [particle] };
        renderer.Render(position, 0, 0, 0, camera, avatarVisible: false, forest: forest);
        Assert.Equal(4, recorder.Materials.Count);
        Assert.All(recorder.Materials.Take(2), material =>
        {
            Assert.Equal(TriangleBlend.Replace, material.Blend);
            Assert.True(material.DepthWrite);
            Assert.Equal(0.75f, material.MinimumAlpha);
        });
        Assert.All(recorder.Materials.Skip(2), material =>
        {
            Assert.False(material.DepthWrite);
            Assert.Equal(0.75f, material.MaximumAlpha);
            Assert.Equal(TriangleDepthComparison.GreaterEqual, material.DepthComparison);
        });
        Assert.All(recorder.Vertices, vertex => Assert.Equal(recorder.Vertices[0].Depth, vertex.Depth));
    }

    private sealed class Recorder(PixelImage texture) : ITriangleRenderer
    {
        public List<TriangleMaterial> Materials { get; } = [];
        public List<TriangleVertex> Vertices { get; } = [];
        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
        }
        public void End()
        {
        }
        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            if (!ReferenceEquals(material.Texture, texture))
            {
                return;
            }
            Materials.Add(material);
            Vertices.AddRange([a, b, c]);
        }
    }
}
