using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using FieldCharacterCatalog = Memoria.FF8.Assets.FieldCharacterCatalog;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7MappedColorOffsetTests
{
    [Fact]
    public void InstalledMappedAppearanceAdmitsUniformOffsetsWhileKeepingTopologyAndLightingGuards()
    {
        string? ff7 = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? ff8 = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ff7) || string.IsNullOrWhiteSpace(ff8), "Select the original source and replacement field resources.");
        using GameAssets assets = new(ff8!);
        Ff7Installation installation = Ff7Installation.Discover(ff7!);
        Ff7FieldResources field = Ff7FieldResources.Load(installation, "md1stin");
        PcFieldModelDescriptor descriptor = field.Models.Models[0];
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.CharacterArchive).Path);
        Ff7FieldModelResources original = Ff7FieldModelResources.Load(archive, descriptor);
        Ff7ModelAppearance appearance = Ff7ModelAppearance.CreateStandardFieldSquall(installation, assets);
        Ff7ActorVisual actor = new(0, 0, 0, 0, 0, 0, 0, 0) { PartyCharacter = 0 };
        Assert.True(appearance.TryCreate(actor, descriptor, original, out IReadOnlyList<Ff7AppearanceTriangle> baseline));
        Assert.NotEmpty(baseline);
        foreach ((short red, short green, short blue) in new (short, short, short)[] { (0, 0, 0), (-32, 64, -96) })
        {
            Ff7ModelRenderOverrides effects = Offset(red, green, blue);
            Assert.True(appearance.TryCreate(actor with { ModelEffects = effects }, descriptor, original, out IReadOnlyList<Ff7AppearanceTriangle> triangles));
            Assert.Equal(baseline.SelectMany(triangle => new[] { triangle.A, triangle.B, triangle.C }), triangles.SelectMany(triangle => new[] { triangle.A, triangle.B, triangle.C }));
            Assert.Equal(baseline.Select(triangle => triangle.Material.Blend), triangles.Select(triangle => triangle.Material.Blend));
            Assert.True(appearance.TryCreate(actor with { ModelEffects = effects with { PartVisible = _ => false } }, descriptor, original, out IReadOnlyList<Ff7AppearanceTriangle> hidden));
            Assert.Empty(hidden);
            Assert.Throws<NotSupportedException>(() => appearance.TryCreate(actor with { ModelEffects = effects with { RequiresOriginalTopology = true } }, descriptor, original, out _));
            Assert.Throws<NotSupportedException>(() => appearance.TryCreate(actor with { ModelEffects = effects with { SuppressNativeLighting = true } }, descriptor, original, out _));
        }

        ModelCatalogEntry entry = assets.GetFieldModelSource("bghall_1", 0);
        FieldCharacterCatalog catalog = FieldCharacterCatalog.Create([entry], assets.LoadFieldModelSource, TestContext.Current.CancellationToken);
        string clip = Path.GetFileNameWithoutExtension(descriptor.Animations[0]) + ".a";
        Ff7ModelAppearance withoutPartBinding = new(assets, catalog, entry.Id, descriptor.Hrc, Matrix4x4.Identity, new Dictionary<string, string> { [clip] = catalog.GetAnimation(entry.Id, 0).Id });
        Assert.True(withoutPartBinding.TryCreate(actor, descriptor, original, out _));
        Assert.Throws<NotSupportedException>(() => withoutPartBinding.TryCreate(actor with { ModelEffects = Offset(0, 0, 0) }, descriptor, original, out _));
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(-32, 64, -96)]
    public void UniformOffsetsShadeActualMappedArtistColorsAndPreserveAlpha(short red, short green, short blue)
    {
        (Ff7FieldResources field, Ff7FieldModelResources original) = Fixture();
        ArtistAppearance appearance = new();
        Ff7FieldRenderer renderer = new(field, new Dictionary<int, Ff7FieldModelResources> { [0] = original }, new(50, 32000), appearance);
        Ff7ActorVisual actor = new(0, 0, 0, 0, 0, 0, 0, 0);
        Capture baseline = Draw(renderer, actor);
        Capture shaded = Draw(renderer, actor with { ModelEffects = Offset(red, green, blue) });
        Assert.Equal(3, baseline.Vertices.Count);
        Assert.Equal(3, shaded.Vertices.Count);
        for (int index = 0; index < baseline.Vertices.Count; index++)
        {
            uint expected = Ff7NativeColorOffset.Shade(ArtistAppearance.Colors[index], red, green, blue);
            Vector4 color = new((byte)(expected >> 16) / 255f, (byte)(expected >> 8) / 255f, (byte)expected / 255f, (byte)(expected >> 24) / 255f);
            Assert.Equal(color, shaded.Vertices[index].Color);
            Assert.Equal(baseline.Vertices[index].Color.W, shaded.Vertices[index].Color.W);
            Assert.Equal(baseline.Vertices[index].Position, shaded.Vertices[index].Position);
        }
        Capture hidden = Draw(renderer, actor with { ModelEffects = Offset(red, green, blue) with { PartVisible = _ => false } });
        Assert.Empty(hidden.Vertices);
        Assert.Equal(ArtistAppearance.Colors, new uint[] { 0x738090A0, 0x2A102030, 0xE0F8C008 });
    }

    private static Ff7ModelRenderOverrides Offset(short red, short green, short blue)
    {
        byte[] payload = new byte[6];
        BinaryPrimitives.WriteInt16LittleEndian(payload, red);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(2), green);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(4), blue);
        Ff7NativeColorOffsetState state = Ff7NativeColorOffset.Advance(Ff7NativeColorOffset.Request(null, payload), true);
        PcFieldCamera camera = new(Vector3.Zero, Vector3.Zero, Vector3.Zero, 0, Vector3.Zero, 0, 0);
        return Ff7Scene.CreateModelEffects(null, camera, null, offset: state)!;
    }

    private static Capture Draw(Ff7FieldRenderer renderer, Ff7ActorVisual actor)
    {
        Capture capture = new();
        renderer.Render(capture, 320, 240, new(0, Vector2.Zero, [actor], new ushort[256]));
        return capture;
    }

    private static (Ff7FieldResources Field, Ff7FieldModelResources Original) Fixture()
    {
        PcFieldCamera camera = new(Vector3.UnitX * 4096, Vector3.UnitY * 4096, Vector3.UnitZ * 4096, 0, new(0, 0, 1000), 0, 100);
        PcFieldModelDescriptor descriptor = new("native", 0, "native.hrc", "128", [new(0, Vector3.Zero), new(0, Vector3.Zero), new(0, Vector3.Zero)], uint.MaxValue, ["pose.a"]);
        PcFieldTriggers triggers = new("appearance", 0, 0, -160, -120, 160, 120, [], [], [], []);
        Ff7FieldResources field = new("appearance", null!, null!, new(0, 0, [descriptor]), [camera], [], triggers, new(0, 0, 1, [[0x7FFF]]), new(320, 240, [0], [], []));
        HrcHierarchy hierarchy = HrcHierarchy.Read(Encoding.ASCII.GetBytes(":HEADER_BLOCK 2\n:SKELETON native\n:BONES 0\nbody\nroot\n0\n1 body\n"));
        PcPolygonModel polygon = new([Vector3.Zero, new(0, 10, 0), new(10, 0, 0)], [], [], [uint.MaxValue, uint.MaxValue, uint.MaxValue], [], [], [], [], ReadOnlyMemory<byte>.Empty);
        Ff7BoneGeometry part = new(0, "body.rsd", new("native", "body.p", [], new Dictionary<string, string>()), "body.p", polygon, []);
        byte[] clip = new byte[60];
        BinaryPrimitives.WriteUInt32LittleEndian(clip, 1);
        BinaryPrimitives.WriteUInt32LittleEndian(clip.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(clip.AsSpan(12), 0x020001);
        Ff7FieldModelResources original = new("native.hrc", hierarchy, [part], new Dictionary<string, PcFieldAnimation> { ["pose.a"] = PcFieldAnimation.Read(clip) });
        return (field, original);
    }

    private sealed class ArtistAppearance : IFf7ActorAppearance
    {
        internal static readonly uint[] Colors = [0x738090A0, 0x2A102030, 0xE0F8C008];
        public bool SupportsPartVisibility => true;

        public bool TryCreate(Ff7ActorVisual actor, PcFieldModelDescriptor descriptor, Ff7FieldModelResources original, out IReadOnlyList<Ff7AppearanceTriangle> triangles)
        {
            triangles = [];
            if (actor.ModelEffects?.PartVisible(0) == false)
            {
                return true;
            }
            Vector3[] points = [new(-10, 0, 10), new(10, 0, -10), new(0, 10, 20)];
            Ff7WorldVertex Vertex(int index)
            {
                uint color = Colors[index];
                return new(points[index], Vector2.Zero, new((byte)(color >> 16) / 255f, (byte)(color >> 8) / 255f, (byte)color / 255f, (byte)(color >> 24) / 255f));
            }
            triangles = [new(Vertex(0), Vertex(1), Vertex(2), new(null))];
            return true;
        }
    }

    private sealed class Capture : ITriangleRenderer
    {
        internal List<TriangleVertex> Vertices { get; } = [];
        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Vertices.AddRange([a, b, c]);
        }

        public void End()
        {
        }
    }
}
