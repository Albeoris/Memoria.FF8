using System.Buffers.Binary;
using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Assets.Tests;

public sealed class Ff7NativeTransparencyMaterialTests
{
    [Fact]
    public void NativeTransparencyForcesOnlyAnEnabledBlendUpdateAndRetainsOtherMaterialState()
    {
        PcRenderState enabled = new(0x400 | 0x4 | 0x800 | 0x8000, 0x400 | 0x4 | 0x800 | 0x8000 | 0x10000, 2, 0, 0, 0, 73, 2, 0, 64, ReadOnlyMemory<byte>.Empty);
        Ff7ModelMaterialState material = new();
        material.Apply(enabled, true);
        Assert.Equal(TriangleBlend.Alpha, material.Blend);
        Assert.True(material.DepthTest);
        Assert.False(material.DepthWrite);
        Assert.Equal(TriangleSampler.LinearWrap, material.Sampler);
        Assert.Equal(73 / 255f, material.AlphaReference);
        material.Apply(enabled with { Features = 0, Options = 0 }, false);
        Assert.Equal(TriangleBlend.Alpha, material.Blend);
        material.Apply(enabled with { Features = 0x400, Options = 0 }, true);
        Assert.Equal(TriangleBlend.Replace, material.Blend);
        material.Apply(enabled with { Features = 0, Options = 0x400 }, true);
        Assert.Equal(TriangleBlend.Replace, material.Blend);
        material.Apply(enabled with { Features = 0x400 }, false);
        Assert.Equal(TriangleBlend.Subtract, material.Blend);
        Assert.False(material.DepthWrite);
        Assert.Equal(TriangleSampler.LinearWrap, material.Sampler);
    }

    [Fact]
    public void NormalAndTransparentModelsUsePhysicalSlotOrderAroundWaterAndBlendedBackground()
    {
        PcFieldCamera camera = new(Vector3.Zero, Vector3.Zero, Vector3.Zero, 0, new(0, 0, 1000), 0, 100);
        PcFieldModelDescriptor descriptor = new("actor", 0, "actor.hrc", "512", Enumerable.Repeat(new PcFieldLight(0, Vector3.Zero), 3).ToArray(), 0, ["pose"]);
        PcFieldTile tile = new(0, 0, 0, 0, 0, 0, 0, 0, 16, 16, 0, 0, 0, 0, 0, 0, 0, 0, 16, 0, 0, 0, ReadOnlyMemory<byte>.Empty);
        PcFieldBackground background = new(320, 240, [0], [[tile], [tile with { Layer = 1, Blending = 1 }]], [new(0, 0, 1, new byte[256 * 256])]);
        PcTexture splash = new(1, 1, 4, [], new byte[] { 255, 255, 255, 255 }, new byte[0xEC]);
        Ff7FieldResources field = new("transparency-order", null!, null!, new(0, 512, [descriptor]), [camera], [], null!, new(0, 0, 1, [[0x7FFF]]), background) { WaterSplashTextures = [splash] };
        Ff7FieldModelResources model = new("actor.hrc", null!, [], new Dictionary<string, PcFieldAnimation>());
        Ff7FieldRenderer renderer = new(field, new Dictionary<int, Ff7FieldModelResources> { [0] = model }, new(50, 32000), new Appearance());
        Ff7ActorVisual Actor(int entity, int slot, byte mode) => new(entity, 0, 0, 0, 0, 0, 0, 0) { PhysicalSlot = slot, NativeTransparencyMode = mode };
        Ff7FieldRenderFrame frame = new(0, Vector2.Zero, [Actor(70, 7, 255), Actor(80, 8, 0), Actor(20, 2, 3), Actor(10, 1, 0)], new ushort[256]) { WaterSplashes = [new(0, new(40, 40), 0.5f, 4)] };
        Capture capture = new();
        renderer.Render(capture, 320, 240, frame);
        Assert.Equal(10, capture.Materials.Count);
        Assert.Equal(TriangleBlend.Replace, capture.Materials[0].Blend);
        Assert.Equal(10 / 255f, capture.Vertices[2].Color.X);
        Assert.Equal(80 / 255f, capture.Vertices[3].Color.X);
        Assert.Equal(128 / 255f, capture.Vertices[4].Color.W);
        Assert.Equal(128 / 255f, capture.Vertices[5].Color.W);
        Assert.Equal(TriangleBlend.HalfAdd, capture.Materials[6].Blend);
        Assert.Equal(TriangleBlend.HalfAdd, capture.Materials[7].Blend);
        Assert.Equal(20 / 255f, capture.Vertices[8].Color.X);
        Assert.Equal(70 / 255f, capture.Vertices[9].Color.X);
        Assert.All(capture.Vertices.TakeLast(2), vertex => Assert.Equal(192 / 255f, vertex.Color.W));
        Assert.All(capture.Materials.TakeLast(2), material => Assert.Equal(TriangleBlend.Alpha, material.Blend));
    }

    [Fact]
    public void OriginalModelRetainsTextureDepthFilteringAndLitRgbWhileReplacingFinalAlpha()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original FFVII resources for the transparency material check.");
        Ff7Installation installation = Ff7Installation.Discover(root!);
        Ff7FieldResources field = Ff7FieldResources.Load(installation, "gldst");
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.CharacterArchive).Path);
        Ff7FieldModelResources original = Ff7FieldModelResources.Load(archive, field.Models.Models[0]);
        Ff7FieldModelResources model = original with
        {
            Geometry = original.Geometry.Select(part => part with
            {
                Polygon = part.Polygon with
                {
                    VertexColors = part.Polygon.VertexColors.Select(color => color & 0x00FFFFFF | 0x80000000).ToArray(),
                    RenderStates = part.Polygon.RenderStates.Select(raw =>
                    {
                        byte[] data = raw.ToArray();
                        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(92), 64);
                        return (ReadOnlyMemory<byte>)data;
                    }).ToArray()
                }
            }).ToArray()
        };
        field = field with { Cameras = [new(Vector3.Zero, Vector3.Zero, Vector3.Zero, 0, new(0, 0, 10000), 0, 256)] };
        Ff7FieldRenderer renderer = new(field, new Dictionary<int, Ff7FieldModelResources> { [0] = model }, new(50, 32000));
        Ff7ActorVisual actor = new(0, 0, 0, 0, 0, 0, 0, 0);
        Capture opaque = new();
        Capture transparent = new();
        renderer.Render(opaque, 320, 240, new(0, Vector2.Zero, [actor], new ushort[256], MovieActive: true));
        renderer.Render(transparent, 320, 240, new(0, Vector2.Zero, [actor with { NativeTransparencyMode = 219 }], new ushort[256], MovieActive: true));
        Assert.NotEmpty(opaque.Materials);
        Assert.Equal(opaque.Vertices.Count, transparent.Vertices.Count);
        Assert.Contains(opaque.Materials, material => material.Texture is not null);
        Assert.All(opaque.Materials.Where(material => material.Texture is not null), material =>
        {
            Assert.True(material.CentroidTextureSampling);
            Assert.Contains(material.Sampler, new[] { TriangleSampler.PointWrap, TriangleSampler.LinearWrap });
        });
        for (int index = 0; index < opaque.Vertices.Count; index++)
        {
            TriangleVertex before = opaque.Vertices[index];
            TriangleVertex after = transparent.Vertices[index];
            Assert.Equal(before.Position, after.Position);
            Assert.Equal(before.Uv, after.Uv);
            Assert.Equal(new Vector3(before.Color.X, before.Color.Y, before.Color.Z), new(after.Color.X, after.Color.Y, after.Color.Z));
            Assert.Equal(192 / 255f, after.Color.W);
            Assert.True(before.Color.W < 192 / 255f);
            TriangleMaterial material = opaque.Materials[index];
            Assert.Equal(material with { Blend = transparent.Materials[index].Blend }, transparent.Materials[index]);
        }
    }

    private sealed class Appearance : IFf7ActorAppearance
    {
        public bool TryCreate(Ff7ActorVisual actor, PcFieldModelDescriptor descriptor, Ff7FieldModelResources original, out IReadOnlyList<Ff7AppearanceTriangle> triangles)
        {
            Vector4 color = new(actor.EntityId / 255f, 0.25f, 0.5f, 0.125f);
            triangles = [new(new(Vector3.Zero, Vector2.Zero, color), new(Vector3.UnitX, Vector2.Zero, color), new(Vector3.UnitY, Vector2.Zero, color), new(null, TriangleBlend.Add))];
            return true;
        }
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
            Vertices.Add(a);
            Materials.Add(material);
        }

        public void End()
        {
        }
    }
}
