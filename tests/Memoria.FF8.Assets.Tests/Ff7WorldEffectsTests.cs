using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Assets.Tests;

public sealed class Ff7WorldEffectsTests
{
    [Fact]
    public void OriginalWorldSpriteTableRendersEveryPaletteAndMirroredAtlasWithoutChangingResources()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original FFVII resources for world sprites.");
        Ff7Installation installation = Ff7Installation.Discover(root!);
        using Ff7WorldResources resources = Ff7WorldResources.Load(installation);
        PcTexture texture = PcTexture.Read(resources.ReadRequired("dfx.tex"));
        Ff7WorldEffectTable table = Ff7PcExecutableData.LoadWorldEffects(installation);
        Assert.Equal(60, table.Sequences.Count);
        Assert.Equal(145, table.Sprites.Count);
        Assert.True(texture.Palettes.Count >= 26);
        Ff7WorldEffectsAdapter adapter = new(texture, table, Ff7PcExecutableData.LoadWorldPresentation(installation, resources));
        Ff7WorldView view = new(Matrix4x4.CreateTranslation(0, 0, 10000), Matrix4x4.Identity, 400, 320, 240, 160);
        foreach ((Ff7WorldEffectSprite sprite, int index) in table.Sprites.Select((sprite, index) => (sprite, index)))
        {
            if ((sprite.Duration & 127) == 0)
            {
                continue;
            }
            Ff7WorldEffectVisual effect = new(index + 1, Vector3.Zero, index, 0, false, sprite.Width, sprite.Height, new(0.5f, 0.5f, 0.5f, 128 / 255f));
            Capture capture = new();
            adapter.Draw(capture, view, Vector3.Zero, 0, 5000, [effect]);
            Assert.Equal(6, capture.Vertices.Count);
            Assert.All(capture.Vertices, vertex =>
            {
                Assert.Equal(1f / 10000, vertex.Depth, 7);
                Assert.Equal(1, vertex.ReciprocalW);
                Assert.Equal(effect.Color, vertex.Color);
                Assert.True(float.IsFinite(vertex.Position.X) && float.IsFinite(vertex.Position.Y));
            });
            Assert.Contains(capture.Vertices, vertex => vertex.Uv == new Vector2(sprite.Left / (float)texture.Width, sprite.Top / (float)texture.Height));
            Assert.Contains(capture.Vertices, vertex => vertex.Uv == new Vector2(sprite.Right / (float)texture.Width, sprite.Bottom / (float)texture.Height));
            Assert.All(capture.Materials, material =>
            {
                Assert.Equal(TriangleDepthComparison.GreaterEqual, material.DepthComparison);
                Assert.False(material.DepthWrite);
                Assert.Equal(TriangleDepthMapping.Positive, material.DepthMapping);
            });
            Capture repeated = new();
            adapter.Draw(repeated, view, Vector3.Zero, 0, 5000, [effect]);
            Assert.Equal(capture.Vertices, repeated.Vertices);
            Assert.Equal(sprite, table.Sprites[index]);
            Capture behind = new();
            adapter.Draw(behind, view with { NativeView = Matrix4x4.CreateTranslation(0, 0, -10000) }, Vector3.Zero, 0, 5000, [effect]);
            Assert.Empty(behind.Vertices);
        }
    }

    [Theory]
    [InlineData(0, 0, TriangleBlend.Alpha)]
    [InlineData(1, 0, TriangleBlend.AlphaAdd)]
    [InlineData(2, 1, TriangleBlend.Alpha)]
    [InlineData(2, 20, TriangleBlend.AlphaAdd)]
    [InlineData(3, 0, TriangleBlend.AlphaAdd)]
    public void NativeSpriteBlendKeepsSourceAlphaAndTheSpecialAdditivePalette(int flags, int palette, TriangleBlend expected)
    {
        Assert.Equal(expected, Ff7WorldEffectsAdapter.Blend(flags, palette));
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
