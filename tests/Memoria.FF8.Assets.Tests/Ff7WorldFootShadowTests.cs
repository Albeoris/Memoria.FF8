using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Assets.Tests;

public sealed class Ff7WorldFootShadowTests
{
    [Fact]
    public void OrdinaryFootShadowKeepsNativePredicatesProjectionAtlasAndTransparentDepthContract()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original FFVII world resources for foot shadows.");
        Ff7Installation installation = Ff7Installation.Discover(root!);
        using Ff7WorldResources resources = Ff7WorldResources.Load(installation);
        Ff7WorldEnvironmentResources environment = Ff7WorldEnvironmentResources.Load(installation, resources);
        Ff7WorldFootShadowAdapter shadow = new(environment.Shadow, new(Ff7PcExecutableData.LoadWorldPresentation(installation, resources)));
        Ff7WorldVisual entity = new(0, Vector3.Zero, 0, 0, 0, 2) { GroundHeight = 0, Terrain = 0 };
        string frozen = JsonSerializer.Serialize(entity);
        Assert.True(Ff7WorldFootShadowAdapter.Eligible(entity, true, 0));
        Assert.False(Ff7WorldFootShadowAdapter.Eligible(entity, true, 1));
        Assert.True(Ff7WorldFootShadowAdapter.Eligible(entity, false, 1));
        Assert.False(Ff7WorldFootShadowAdapter.Eligible(entity with { Flags = 10 }, true, 0));
        Assert.False(Ff7WorldFootShadowAdapter.Eligible(entity with { Model = 3 }, true, 0));
        Assert.False(Ff7WorldFootShadowAdapter.Eligible(entity with { GroundHeight = null }, true, 0));
        Assert.False(Ff7WorldFootShadowAdapter.Eligible(entity with { NativeAnimation = 2 }, true, 0));
        Assert.False(Ff7WorldFootShadowAdapter.Eligible(entity with { Terrain = 1 }, true, 0));
        Ff7WorldView view = new(Matrix4x4.CreateTranslation(0, 0, 10000), Matrix4x4.Identity, 400, 640, 480, 240);
        Capture actual = new();
        Assert.Equal(2, shadow.Draw(actual, entity, entity.Position, 0, view, 0, true, 0));
        Assert.Equal(6, actual.Vertices.Count);
        Assert.All(actual.Vertices, vertex =>
        {
            Assert.Equal(1f / 9900, vertex.Depth, 7);
            Assert.Equal(1, vertex.ReciprocalW);
            Assert.Equal(new Vector4(64 / 255f, 64 / 255f, 64 / 255f, 128 / 255f), vertex.Color);
            Assert.Equal(MathF.Round(vertex.Position.X, MidpointRounding.ToEven), vertex.Position.X);
            Assert.Equal(MathF.Round(vertex.Position.Y, MidpointRounding.ToEven), vertex.Position.Y);
        });
        Assert.Contains(actual.Vertices, vertex => vertex.Uv == new Vector2(24f / environment.Shadow.Width, 0));
        Assert.Contains(actual.Vertices, vertex => vertex.Uv == new Vector2(39f / environment.Shadow.Width, 15f / environment.Shadow.Height));
        Assert.All(actual.Materials, material =>
        {
            Assert.Equal(TriangleBlend.Alpha, material.Blend);
            Assert.Equal(TriangleDepthComparison.GreaterEqual, material.DepthComparison);
            Assert.False(material.DepthWrite);
            Assert.Equal(TriangleDepthMapping.Positive, material.DepthMapping);
            Assert.Equal(1, material.DepthScale);
        });
        Capture repeated = new();
        shadow.Draw(repeated, entity, entity.Position, 0, view, 0, true, 0);
        Assert.Equal(actual.Vertices, repeated.Vertices);
        Assert.Equal(frozen, JsonSerializer.Serialize(entity));
        Capture shifted = new();
        shadow.Draw(shifted, entity, new(17, 0, 0), 0, view, 0, true, 0);
        Assert.Equal(325, shifted.Vertices[0].Position.X);
        Assert.Equal(MathF.Round(320 + 117f * 400 / 10100, MidpointRounding.ToEven), shifted.Vertices[0].Position.X);
        Assert.Equal(0, shadow.Draw(new Capture(), entity, entity.Position, 0, view with { NativeView = Matrix4x4.CreateTranslation(0, 0, 70000) }, 0, true, 0));
        Assert.Equal(0, shadow.Draw(new Capture(), entity, entity.Position, 0, view with { NativeView = Matrix4x4.Identity }, 0, true, 0));
        Ff7WorldView distant = view with { NativeView = Matrix4x4.CreateTranslation(0, -200, 30000) };
        Assert.Equal(381, Ff7WorldFootShadowAdapter.Curvature(Vector3.Zero, distant, 200, 5000));
        Assert.Equal(381, Ff7WorldFootShadowAdapter.Curvature(new(0, 9000, 0), distant, 200, 5000));
        Assert.Equal(381, Ff7WorldFootShadowAdapter.Curvature(Vector3.Zero, distant with { Width = 1280, Height = 960, Focal = 800, CenterY = 480 }, 200, 5000));
    }

    private sealed class Capture : ITriangleRenderer
    {
        public List<TriangleVertex> Vertices { get; } = [];
        public List<TriangleMaterial> Materials { get; } = [];

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Vertices.AddRange([a, b, c]);
            Materials.Add(material);
        }

        public void End()
        {
        }
    }
}
