using System.Buffers.Binary;
using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Assets.Tests;

public sealed class Ff7FieldBackgroundBlendTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(255)]
    [InlineData(65535)]
    public void BaseLayerIgnoresUnusedFxFields(int mode)
    {
        PcFieldTile tile = new(0, 0, 0, 0, 16, 0, 0, 0, 16, 16, 0, 0, 0, 0, 1, (ushort)mode, 0, 77, 0, 0, 0, 0, ReadOnlyMemory<byte>.Empty);
        PcFieldCamera camera = new(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, 0, new(0, 0, 1000), 0, 100);
        PcFieldBackground background = new(320, 240, [0], [[tile]], [new(0, 0, 1, new byte[256 * 256])]);
        Ff7FieldResources field = new("base-layer", null!, null!, null!, [camera], [], new("base-layer", 0, 0, -160, -120, 160, 120, [], [], [], []), new(0, 0, 1, [[0x7FFF]]), background);
        Assert.False(tile.UsesFxPage);
        Assert.Equal(0, tile.EffectiveTextureId);
        Assert.Equal(16, tile.EffectiveSourceX);
        Capture capture = Render(field);
        Assert.Equal(2, capture.Materials.Count);
        Assert.All(capture.Materials, material =>
        {
            Assert.Equal(TriangleBlend.Replace, material.Blend);
            Assert.True(material.DepthWrite);
        });
        Assert.True((tile with { Layer = 1 }).UsesFxPage);
        Assert.Equal(77, (tile with { Layer = 1 }).EffectiveTextureId);
    }

    [Fact]
    public void OriginalBaseLayerPreservesBlendWordAndRendersWithoutFxInterpretation()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original FFVII resources for the reached background contract.");
        Ff7FieldResources field = Ff7FieldResources.Load(Ff7Installation.Discover(root!), "bugin1a");
        PcFieldTile tile = field.Background.Layers[0][1];
        Assert.Equal(ushort.MaxValue, BinaryPrimitives.ReadUInt16LittleEndian(tile.Raw.Span[28..]));
        Assert.Equal(ushort.MaxValue, tile.BlendMode);
        Assert.Equal(1, tile.Blending);
        Assert.Equal(77, tile.TextureId2);
        Assert.False(tile.UsesFxPage);
        Capture selected = Render(field with { Background = field.Background with { Layers = [[tile]] } });
        Assert.All(selected.Materials, material =>
        {
            Assert.Equal(TriangleBlend.Replace, material.Blend);
            Assert.True(material.DepthWrite);
        });
        Assert.NotEmpty(Render(field).Materials);
    }

    private static Capture Render(Ff7FieldResources field)
    {
        Capture capture = new();
        Ff7FieldRenderer renderer = new(field, new Dictionary<int, Ff7FieldModelResources>(), new(50, 32000));
        renderer.Render(capture, 320, 240, new(0, Vector2.Zero, [], Enumerable.Repeat(ushort.MaxValue, 256).ToArray()));
        return capture;
    }

    private sealed class Capture : ITriangleRenderer
    {
        public List<TriangleMaterial> Materials { get; } = [];

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Materials.Add(material);
        }

        public void End()
        {
        }
    }
}
