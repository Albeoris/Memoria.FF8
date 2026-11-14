using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Assets.Tests;

public sealed class Ff7FieldBackgroundDepthTests
{
    [Fact]
    public void NativeBackgroundProjectionRetainsNearFarGuardsAndNormalizedFallback()
    {
        Ff7ProjectionDepth projection = new(512, 32000);
        Assert.Equal(0.0001f, projection.ProjectBackground(0));
        Assert.Equal(0.001f, projection.ProjectBackground(1));
        Assert.Equal(0.128f, projection.ProjectBackground(128));
        Assert.Equal(projection.Project(516), projection.ProjectBackground(129));
        Assert.Equal(projection.Project(16376), projection.ProjectBackground(4094));
        Assert.Equal(0.9999f, projection.ProjectBackground(4095));
        Assert.Equal(0.9999f, projection.ProjectBackground(ushort.MaxValue));
        Assert.Equal(100 / 4096f, new Ff7ProjectionDepth(50, 100).ProjectBackground(100));
        Assert.Throws<InvalidDataException>(() => new Ff7ProjectionDepth(0, 32000).ProjectBackground(0));
    }

    [Fact]
    public void LayerTwoAndThreeConsumeIndependentDepthOwnersWhileEarlierLayersKeepTheirDepths()
    {
        PcFieldCamera camera = new(Vector3.UnitX * 4096, Vector3.UnitY * 4096, Vector3.UnitZ * 4096, 0, new(0, 0, 1000), 0, 100);
        PcFieldTriggers triggers = new("background-depth", 0, 0, -160, -120, 160, 120, [], [], [], []);
        PcFieldTile tile = new(0, 0, 0, 0, 0, 0, 0, 0, 16, 16, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2500000, 0, 0, ReadOnlyMemory<byte>.Empty);
        PcFieldBackground background = new(320, 240, [0], Enumerable.Range(0, 4).Select(layer => (IReadOnlyList<PcFieldTile>)[tile with { Layer = layer, Blending = (byte)(layer > 1 ? 1 : 0) }]).ToArray(), [new(0, 0, 1, new byte[256 * 256])]);
        Ff7FieldResources field = new("background-depth", null!, null!, null!, [camera], [], triggers, new(0, 0, 1, [[0x7FFF]]), background);
        Ff7ProjectionDepth projection = new(50, 32000);
        Ff7FieldRenderer renderer = new(field, new Dictionary<int, Ff7FieldModelResources>(), projection);
        Ff7FieldRenderFrame defaults = new(0, Vector2.Zero, [], new ushort[256]);
        Capture before = new();
        renderer.Render(before, 320, 240, defaults);
        Assert.Equal(new[] { 0.9997f, 0.25f, 0.9998f, 0.001f }, before.Vertices.Chunk(6).Select(vertices => vertices[0].Depth));
        Ff7FieldRenderFrame changed = defaults with { Layer2Depth = 100, Layer3Depth = 4094 };
        Capture after = new();
        renderer.Render(after, 320, 240, changed);
        Assert.Equal(new[] { 0.9997f, 0.25f, projection.Project(400), projection.Project(16376) }, after.Vertices.Chunk(6).Select(vertices => vertices[0].Depth));
        Assert.Equal(before.Materials, after.Materials);
        Assert.Equal(before.Vertices.Select(vertex => vertex with { Depth = 0 }), after.Vertices.Select(vertex => vertex with { Depth = 0 }));
        Ff7FieldRenderFrame far = defaults with { Layer2Depth = ushort.MaxValue, Layer3Depth = ushort.MaxValue };
        Capture farCapture = new();
        renderer.Render(farCapture, 320, 240, far);
        Assert.Equal(0.9998f, farCapture.Vertices[12].Depth);
        Assert.Equal(0.9999f, farCapture.Vertices[18].Depth);
        string saved = JsonSerializer.Serialize(changed);
        Capture cold = new();
        renderer.Render(cold, 320, 240, JsonSerializer.Deserialize<Ff7FieldRenderFrame>(saved)!);
        Assert.Equal(after.Vertices, cold.Vertices);
        Assert.Equal(after.Materials, cold.Materials);
    }

    [Fact]
    public void OriginalBeachWaterLayerUsesScriptDepthWithoutChangingItsGeometryPaletteOrBlend()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original FFVII resources for background depth validation.");
        Ff7Installation installation = Ff7Installation.Discover(root!);
        Ff7FieldResources original = Ff7FieldResources.Load(installation, "ujunon2");
        Assert.Equal(new byte[] { 0x2C, 0, 3, 0xFE, 0x0F }, original.Scripts.Data.Span.Slice(0x0509, 5).ToArray());
        PcFieldTile tile = original.Background.Layers.SelectMany(layer => layer).First(tile => tile.Layer == 3);
        Ff7FieldResources selected = original with { Background = original.Background with { Layers = [[tile]] } };
        Ff7PcExecutableData native = Ff7PcExecutableData.Load(installation);
        Ff7ProjectionDepth projection = new(native.NativePresentation.NearPlane(429), native.FieldFarPlane);
        Ff7FieldRenderer renderer = new(selected, new Dictionary<int, Ff7FieldModelResources>(), projection);
        Ff7FieldRenderFrame frame = new(0, Vector2.Zero, [], Enumerable.Repeat(ushort.MaxValue, 256).ToArray());
        Capture before = new();
        renderer.Render(before, 320, 240, frame);
        Ff7FieldRenderFrame changed = frame with { Layer3Depth = 4094 };
        string saved = JsonSerializer.Serialize(changed);
        Capture after = new();
        renderer.Render(after, 320, 240, changed);
        Capture cold = new();
        renderer.Render(cold, 320, 240, JsonSerializer.Deserialize<Ff7FieldRenderFrame>(saved)!);
        Assert.NotEmpty(after.Vertices);
        Assert.All(before.Vertices, vertex => Assert.Equal(projection.ProjectBackground(1), vertex.Depth));
        float expected = (float)(32000d / (32000 - projection.NearPlane) - 32000d * projection.NearPlane / ((32000 - projection.NearPlane) * 16376d));
        Assert.All(after.Vertices, vertex => Assert.Equal(expected, vertex.Depth, 6));
        Assert.Equal(before.Vertices.Select(vertex => vertex with { Depth = 0 }), after.Vertices.Select(vertex => vertex with { Depth = 0 }));
        Assert.Equal(before.Materials, after.Materials);
        Assert.Equal(after.Vertices, cold.Vertices);
        Assert.Equal(after.Materials, cold.Materials);
        Assert.Equal(tile, Assert.Single(selected.Background.Layers[0]));
        Assert.Equal(saved, JsonSerializer.Serialize(changed));
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
