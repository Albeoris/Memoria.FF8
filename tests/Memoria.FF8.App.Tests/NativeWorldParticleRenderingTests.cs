using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Events;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeWorldParticleRenderingTests
{
    [Fact]
    public void SavedWorldParticlesRenderAcrossRestoreWithoutChangingTheSource()
    {
        string path = TestResources.FixturePath("App.Tests.WorldParticleViewport.1");
        byte[] hash = SHA256.HashData(File.ReadAllBytes(path));
        GameSession session = new(PlayerStateLoader.Load(path, TestResources.GameDirectory));
        using WorldMapScene scene = new(session, TestResources.GameDirectory);
        Assert.True(scene.Apply(new(PlayerActionKind.Wait, Frames: 2)).Accepted);
        byte[] rendered = scene.Render(2560, 720);
        GameSession restoredSession = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using WorldMapScene restored = new(restoredSession, TestResources.GameDirectory);
        Assert.Equal(rendered, restored.Render(2560, 720));
        Assert.Equal(scene.Apply(new(PlayerActionKind.Wait)), restored.Apply(new(PlayerActionKind.Wait)));
        Assert.Equal(JsonSerializer.Serialize(session.State), JsonSerializer.Serialize(restoredSession.State));
        Assert.Equal(scene.Render(2560, 720, interpolation: 0.5f), restored.Render(2560, 720, interpolation: 0.5f));
        Assert.Equal(hash, SHA256.HashData(File.ReadAllBytes(path)));
    }

    [Theory]
    [InlineData(960, 672, (byte)13)]
    [InlineData(1280, 720, (byte)13)]
    [InlineData(2560, 720, (byte)13)]
    [InlineData(960, 672, (byte)14)]
    [InlineData(1280, 720, (byte)14)]
    [InlineData(2560, 720, (byte)14)]
    public void ParticleAnchorsUseTheSameViewportAsWorldModels(int width, int height, byte type)
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        NativeWorldForestEffect effect = assets.ForestTables.GetEffect(type);
        Recorder recorder = new(assets.GetForestTexture(type, 0));
        WorldTerrainRenderer renderer = new(assets, width, height) { TriangleRenderer = recorder };
        NativeWorldFootCameraState words = new(18381, -68272, -4000, 18381, -68272, -4256, 733, -112, -341, -7168);
        NativeWorldPathProjectionCertificate projection = NativeWorldPathProjectionCertificate.CreateFoot(words);
        Vector3 position = new(words.X, -words.NativeHeight, words.RawZ);
        WorldCamera camera = WorldCamera.Foot(words.Yaw, 4096) with { TargetPosition = new(words.TargetX, -words.TargetNativeHeight, words.TargetRawZ), ParticleProjection = projection };
        NativeWorldForestParticle particle = new(0, type, words.X + 1200, -4300, -words.RawZ + 600, 0, 0, 16, 0, effect.Lifetime, true) { Motion = new(0, 0, 0, 0) };
        NativeWorldForestState forest = NativeWorldForestState.Create(0, 0) with { Particles = [particle] };
        renderer.Render(position, 0, 0, 0, camera, avatarVisible: false, forest: forest);
        Assert.NotEmpty(recorder.Vertices);
        NativeWorldPathNodeProjection node = projection.ProjectNode(new(particle.X, particle.NativeY, particle.GraphicsZ))!;
        WorldCameraFrame frame = camera.FootFrame(position);
        Vector3 modelPoint = Vector3.Transform(projection.CanonicalModelAnchor(node), Matrix4x4.CreateLookAt(frame.Eye, frame.Eye + frame.Forward, frame.Up));
        float focal = height / (2 * MathF.Tan(camera.FieldOfView / 2));
        Vector2 expected = new(width / 2f + modelPoint.X * focal / -modelPoint.Z, height / 2f - modelPoint.Y * focal / -modelPoint.Z);
        Vector2 minimum = recorder.Vertices.Select(vertex => vertex.Position).Aggregate(Vector2.Min);
        Vector2 maximum = recorder.Vertices.Select(vertex => vertex.Position).Aggregate(Vector2.Max);
        Assert.InRange(Vector2.Distance(expected, (minimum + maximum) / 2), 0, 2);
    }

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
