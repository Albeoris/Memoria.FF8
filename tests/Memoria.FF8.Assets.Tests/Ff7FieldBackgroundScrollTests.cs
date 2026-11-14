using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Assets.Tests;

public sealed class Ff7FieldBackgroundScrollTests
{
    [Fact]
    public void IndependentSignedPhasesKeepCameraFactorsMaterialsAndClipping()
    {
        Ff7FieldResources field = AuthoredField([0, 0, 256, 256]);
        Ff7FieldRenderer renderer = new(field, new Dictionary<int, Ff7FieldModelResources>(), new(50, 32000));
        Ff7FieldRenderFrame frame = new(0, new(8, -4), [], new ushort[256]);
        Capture before = Draw(renderer, frame, 640, 480);
        Capture after = Draw(renderer, frame with { Layer2ScrollX16 = 16, Layer2ScrollY16 = -16, Layer3ScrollX16 = -1, Layer3ScrollY16 = 15 }, 640, 480);
        Assert.Equal(new Vector2(-2, 2), after.Vertices[0].Position - before.Vertices[0].Position);
        Assert.Equal(new Vector2(2, 0), after.Vertices[6].Position - before.Vertices[6].Position);
        Assert.Equal(before.Materials, after.Materials);
        Assert.Equal(before.Vertices.Select(vertex => vertex with { Position = Vector2.Zero, TextureScreenRectangle = Vector4.Zero }), after.Vertices.Select(vertex => vertex with { Position = Vector2.Zero, TextureScreenRectangle = Vector4.Zero }));
        Capture camera = Draw(renderer, frame with { CameraOffsetPixels = new(12, -6) }, 640, 480);
        Assert.Equal(before.Vertices.Take(6), camera.Vertices.Take(6));
        Assert.Equal(new Vector2(8, -4), camera.Vertices[6].Position - before.Vertices[6].Position);
        Capture shake = Draw(renderer, frame with { CameraOffsetPixels = new(10, -7), BackgroundCameraShakePixels = new(2, -3) }, 640, 480);
        Assert.Equal(new Vector2(4, -6), shake.Vertices[0].Position - before.Vertices[0].Position);
        Assert.Equal(new Vector2(4, -6), shake.Vertices[6].Position - before.Vertices[6].Position);
    }

    [Fact]
    public void NativeSingleDimensionWrapAndColdRepeatedDrawPreserveThePhaseOwner()
    {
        Ff7FieldResources field = AuthoredField([256, 256, 256, 256], 200, -200);
        Ff7FieldRenderer renderer = new(field, new Dictionary<int, Ff7FieldModelResources>(), new(50, 32000));
        Ff7FieldRenderFrame frame = new(0, Vector2.Zero, [], new ushort[256]) { Layer2ScrollX16 = 16, Layer2ScrollY16 = -16, Layer3ScrollX16 = -16, Layer3ScrollY16 = 16 };
        string saved = JsonSerializer.Serialize(frame);
        Capture first = Draw(renderer, frame);
        Assert.Equal(new Vector2(160 + 200 - 1024 - 1, 120 - 200 + 1024 + 1), first.Vertices[0].Position);
        Assert.Equal(new Vector2(160 + 200 - 1024 + 1, 120 - 200 + 1024 - 1), first.Vertices[6].Position);
        Capture second = Draw(renderer, frame);
        Capture cold = Draw(renderer, JsonSerializer.Deserialize<Ff7FieldRenderFrame>(saved)!);
        Assert.Equal(first.Vertices, second.Vertices);
        Assert.Equal(first.Vertices, cold.Vertices);
        Assert.Equal(saved, JsonSerializer.Serialize(frame));
    }

    [Fact]
    public void OriginalBeachOneTickWaterPhaseMovesOnePixelWithOriginalCameraFactors()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original FFVII resources for background scroll validation.");
        Ff7Installation installation = Ff7Installation.Discover(root!);
        Ff7FieldResources original = Ff7FieldResources.Load(installation, "ujunon2");
        Assert.Equal(new short[] { 0, 0, 256, 256 }, original.Triggers.LayerCameraFactors);
        Assert.Equal(new short[] { 1024, 1024, 1024, 1024 }, original.Triggers.LayerDimensions);
        Assert.Equal(new byte[] { 0x2D, 0, 3, 16, 0, 0xF0, 0xFF }, original.Scripts.Data.Span.Slice(0x050F, 7).ToArray());
        PcFieldTile tile = original.Background.Layers.SelectMany(layer => layer).First(tile => tile.Layer == 3 && tile.DestinationX is not (-192 or -191 or 159 or 160 or 161) && tile.DestinationY is not (-145 or -144 or -143 or 111 or 112 or 113));
        Ff7FieldResources selected = original with { Background = original.Background with { Layers = [[tile]] } };
        Ff7FieldRenderer renderer = new(selected, new Dictionary<int, Ff7FieldModelResources>(), new(50, 32000));
        Ff7FieldRenderFrame frame = new(0, Vector2.Zero, [], Enumerable.Repeat(ushort.MaxValue, 256).ToArray()) { Layer3Depth = 4094 };
        Capture before = Draw(renderer, frame);
        Capture after = Draw(renderer, frame with { Layer3ScrollX16 = 16, Layer3ScrollY16 = -16 });
        Assert.NotEmpty(before.Vertices);
        Assert.Equal(before.Materials, after.Materials);
        Assert.Equal(before.Vertices.Select(vertex => vertex with { Position = vertex.Position + new Vector2(-1, 1), TextureScreenRectangle = vertex.TextureScreenRectangle + new Vector4(-1, 1, 0, 0) }), after.Vertices);
        Assert.Equal(before.Vertices, Draw(renderer, frame).Vertices);
    }

    private static Ff7FieldResources AuthoredField(short[] factors, short x = 0, short y = 0)
    {
        PcFieldCamera camera = new(Vector3.UnitX * 4096, Vector3.UnitY * 4096, Vector3.UnitZ * 4096, 0, new(0, 0, 1000), 0, 100);
        PcFieldTriggers triggers = new("background-scroll", 0, 0, -160, -120, 160, 120, [1024, 1024, 1024, 1024], [], [], []) { LayerCameraFactors = factors };
        PcFieldTile tile = new(2, 0, x, y, 0, 0, 0, 0, 32, 32, 0, 0, 0, 0, 1, 0, 0, 0, 0, 2500000, 0, 0, ReadOnlyMemory<byte>.Empty);
        PcFieldBackground background = new(320, 240, [0], [[tile], [tile with { Layer = 3 }]], [new(0, 0, 1, new byte[256 * 256])]);
        return new("background-scroll", null!, null!, null!, [camera], [], triggers, new(0, 0, 1, [[0x7FFF]]), background);
    }

    private static Capture Draw(Ff7FieldRenderer renderer, Ff7FieldRenderFrame frame, int width = 320, int height = 240)
    {
        Capture result = new();
        renderer.Render(result, width, height, frame);
        return result;
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
