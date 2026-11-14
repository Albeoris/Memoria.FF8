using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7WaterActivePresentationTests
{
    [Fact]
    public void NativeHeightMasksAndProjectedSegmentSplashesRetainTheirSeparateFirstSampleOwners()
    {
        (Ff7FieldModelResources model, PcFieldModelDescriptor descriptor, PcFieldAnimation animation) = AuthoredModel();
        PcFieldCamera camera = Camera();
        Ff7FieldWaterParameters parameters = new(-32, -64, -32, 0, 224, 3);
        Ff7FieldWaterGeometry capture = Sample(parameters);
        Assert.Equal(new[] { true, false, false }, Assert.Single(capture.Parts).ModifiedVertices);
        Ff7FieldWaterSplashCandidate splash = Assert.Single(capture.Splashes);
        Assert.Equal(0, splash.BoneIndex);
        Assert.Equal(172, splash.X);
        Assert.Equal(120, splash.Y);
        Assert.Equal(12, splash.HalfSize);
        double scale = 128f / 4096 * 30.43090057373047f;
        double endZ = 900 - (200 * scale + 1);
        double fraction = (-170.09999084472656 + 100) / (-200 * scale - 1);
        double firstDepth = 32000d / 31950 - 1600000d / (31950 * 900);
        double endDepth = 32000d / 31950 - 1600000d / (31950 * endZ);
        float referenceDepth = (float)(1 - (firstDepth + (endDepth - firstDepth) * fraction));
        Assert.InRange(MathF.Abs(referenceDepth - splash.Depth), 0, 0.0000002f);
        Assert.Empty(Ff7FieldWaterGeometryMath.Capture(model, descriptor, animation, 0, new(100, 0, -100), 0, camera, Vector2.Zero, 50, 32000, parameters, false).Splashes);
        Assert.All(Sample(parameters with { Flags = 4 }).Parts, part => Assert.All(part.ModifiedVertices, modified => Assert.True(modified)));
        Ff7FieldWaterGeometry restoredColors = Sample(parameters with { EnvironmentProfile = 0 });
        Assert.All(restoredColors.Parts, part => Assert.All(part.ModifiedVertices, modified => Assert.False(modified)));
        Assert.Empty(restoredColors.Splashes);
        Ff7FieldWaterGeometry zeroLevel = Sample(parameters with { Level = 0 });
        Assert.All(zeroLevel.Parts, part => Assert.All(part.ModifiedVertices, modified => Assert.False(modified)));
        Assert.Empty(zeroLevel.Splashes);

        Ff7FieldWaterGeometry Sample(Ff7FieldWaterParameters selected)
        {
            return Ff7FieldWaterGeometryMath.Capture(model, descriptor, animation, 0, new(100, 0, -100), 0, camera, Vector2.Zero, 50, 32000, selected, true);
        }
    }

    [Fact]
    public void OriginalDolphinCachedMasksUseDirectRgbOffsetsAndStayStableAcrossDrawsAndColdSnapshots()
    {
        (Ff7FieldResources field, Ff7FieldModelResources model) = OriginalDolphin();
        PcFieldModelDescriptor descriptor = field.Models.Models[8];
        PcFieldAnimation animation = model.Animations[Path.ChangeExtension(descriptor.Animations[0], ".a")];
        Ff7FieldWaterParameters parameters = new(-32, -64, -32, 0, 224, 3);
        Ff7FieldWaterGeometry geometry = Ff7FieldWaterGeometryMath.Capture(model, descriptor, animation, 0, new(0, 0, 5000), 0, Camera(), Vector2.Zero, 50, 32000, parameters, false);
        Assert.All(geometry.Parts, part => Assert.All(part.ModifiedVertices, modified => Assert.True(modified)));
        Assert.Empty(geometry.Splashes);
        Ff7NativeWaterEffectState initialized = Ff7NativeWaterEffect.Advance(Ff7NativeWaterEffect.Request(null, field.Scripts.Data.Span.Slice(0x0C25, 27), 0), true);
        Ff7NativeWaterEffectState requested = Ff7NativeWaterEffect.Request(initialized, [1], 0);
        Ff7NativeWaterEffectSample sample = new(parameters, geometry.Parts.ToArray(), requested.CycleIndex, requested.SubcycleIndex);
        Ff7NativeWaterEffectState applied = Ff7NativeWaterEffect.Advance(requested, true, sample) with { Active = false };
        Ff7ModelRenderOverrides effects = Ff7Scene.CreateWaterModelEffects(applied);
        Assert.False(effects.SuppressNativeLighting);
        Assert.True(effects.RequiresOriginalTopology);
        Assert.Equal(0xFF8090C0U, effects.SourceVertexColor!(0, 0, 0xFFC0B0C0));
        Ff7NativeWaterEffectState saturating = applied with { DisplayedSample = sample with { Parameters = parameters with { Red = 300, Green = -300, Blue = 300 } } };
        Assert.Equal(0x73FF00FFU, Ff7Scene.CreateWaterModelEffects(saturating).SourceVertexColor!(0, 0, 0x73402010));
        Ff7NativeWaterEffectState overflowing = applied with { DisplayedSample = sample with { Parameters = parameters with { Red = short.MaxValue, Green = 300, Blue = -300 } } };
        Assert.Equal(0x7300FF00U, Ff7Scene.CreateWaterModelEffects(overflowing).SourceVertexColor!(0, 0, 0x73FFFFFF));
        Assert.Throws<InvalidDataException>(() => effects.SourceVertexColor!(0, geometry.Parts[0].ModifiedVertices.Length, uint.MaxValue));
        Ff7ActorVisual actor = new(13, 8, 0, 0, 5000 * 4096, 0, 0, 0);
        Ff7FieldRenderer renderer = new(field, new Dictionary<int, Ff7FieldModelResources> { [8] = model }, new(50, 32000));
        Ff7FieldRenderFrame frame = new(0, Vector2.Zero, [actor], new ushort[256], CameraOverride: Camera());
        Capture before = Draw(renderer, frame);
        Capture after = Draw(renderer, frame with { Actors = [actor with { ModelEffects = effects }] });
        Assert.NotEmpty(after.Vertices);
        Assert.Equal(before.Materials, after.Materials);
        Assert.Equal(before.Vertices.Select(vertex => vertex with { Color = Vector4.Zero }), after.Vertices.Select(vertex => vertex with { Color = Vector4.Zero }));
        Assert.Contains(before.Vertices.Zip(after.Vertices), pair => pair.First.Color != pair.Second.Color);
        string saved = JsonSerializer.Serialize(applied);
        Ff7NativeWaterEffectState cold = JsonSerializer.Deserialize<Ff7NativeWaterEffectState>(saved)!;
        Ff7ModelRenderOverrides coldEffects = Ff7Scene.CreateWaterModelEffects(cold);
        Capture repeated = Draw(renderer, frame with { Actors = [actor with { ModelEffects = effects }] });
        Capture restored = Draw(renderer, frame with { Actors = [actor with { ModelEffects = coldEffects }] });
        Assert.Equal(after.Vertices, repeated.Vertices);
        Assert.Equal(after.Vertices, restored.Vertices);
        Assert.Equal(saved, JsonSerializer.Serialize(applied));
        Assert.Equal(saved, JsonSerializer.Serialize(cold));
    }

    [Fact]
    public void OriginalSplashResourcesDrawImmutableNativeTextureQueuesWithCachedGeometry()
    {
        (Ff7FieldResources field, Ff7FieldModelResources model) = OriginalDolphin();
        Assert.Equal(4, field.WaterSplashTextures.Count);
        Assert.All(field.WaterSplashTextures, texture =>
        {
            Assert.Equal(32, texture.Width);
            Assert.Equal(32, texture.Height);
        });
        Ff7FieldRenderer renderer = new(field, new Dictionary<int, Ff7FieldModelResources>(), new(50, 32000));
        Ff7FieldRenderFrame frame = new(0, Vector2.Zero, [], new ushort[256])
        {
            WaterSplashes = [new(3, new(160, 120), 0.05f, 12), new(0, new(40, 60), 0.06f, 12), new(0, new(80, 60), 0.07f, 12)]
        };
        Capture first = Draw(renderer, frame);
        Assert.Equal(18, first.Vertices.Count);
        Assert.Equal(new Vector2(28, 48), first.Vertices[0].Position);
        Assert.Equal(new Vector2(68, 48), first.Vertices[6].Position);
        Assert.Equal(new Vector2(148, 108), first.Vertices[12].Position);
        Assert.All(first.Vertices, vertex => Assert.Equal(new Vector4(1, 1, 1, 128 / 255f), vertex.Color));
        Assert.All(first.Materials, material => Assert.Equal(TriangleBlend.Alpha, material.Blend));
        // Saves store scalar queue coordinates; Vector2 belongs only to the transient render frame.
        Ff7NativeWaterSplash[] queue = frame.WaterSplashes.Select(splash => new Ff7NativeWaterSplash(0, 0, splash.TextureIndex, splash.Position.X, splash.Position.Y, splash.Depth, splash.HalfSize)).ToArray();
        string saved = JsonSerializer.Serialize(queue);
        Ff7NativeWaterSplash[] coldQueue = JsonSerializer.Deserialize<Ff7NativeWaterSplash[]>(saved)!;
        Capture repeated = Draw(renderer, frame);
        Capture cold = Draw(renderer, frame with { WaterSplashes = coldQueue.Select(splash => new Ff7FieldWaterSplashVisual(splash.TextureIndex, new(splash.X, splash.Y), splash.Depth, splash.HalfSize)).ToArray() });
        Assert.Equal(first.Vertices, repeated.Vertices);
        Assert.Equal(first.Vertices, cold.Vertices);
        Assert.Equal(first.Materials, repeated.Materials);
        Assert.Equal(first.Materials, cold.Materials);
        Assert.Equal(saved, JsonSerializer.Serialize(queue));
        Assert.Equal(saved, JsonSerializer.Serialize(coldQueue));
        Ff7FieldModelResources authoredMaterial = model with
        {
            Geometry = model.Geometry.Select(part => part with
            {
                Polygon = part.Polygon with
                {
                    RenderStates = part.Polygon.RenderStates.Select(memory =>
                    {
                        byte[] state = memory.ToArray();
                        uint options = BinaryPrimitives.ReadUInt32LittleEndian(state.AsSpan(8));
                        uint features = BinaryPrimitives.ReadUInt32LittleEndian(state.AsSpan(12));
                        BinaryPrimitives.WriteUInt32LittleEndian(state.AsSpan(8), (options & ~0x18000U) | 0x804U);
                        BinaryPrimitives.WriteUInt32LittleEndian(state.AsSpan(12), features | 0x18804U);
                        BinaryPrimitives.WriteUInt32LittleEndian(state.AsSpan(64), 128);
                        return (ReadOnlyMemory<byte>)state;
                    }).ToArray()
                }
            }).ToArray()
        };
        Ff7FieldRenderer inheritedRenderer = new(field, new Dictionary<int, Ff7FieldModelResources> { [8] = authoredMaterial }, new(50, 32000));
        Capture inherited = Draw(inheritedRenderer, frame with { Actors = [new(13, 8, 0, 0, 0, 0, 0, 0)], CameraOverride = Camera() });
        Assert.True(inherited.Vertices.Count > 18);
        Assert.All(inherited.Materials.TakeLast(6), material =>
        {
            Assert.Equal(TriangleDepthComparison.Always, material.DepthComparison);
            Assert.False(material.DepthWrite);
            Assert.Equal(128 / 255f, material.MinimumAlpha);
            Assert.Equal(TriangleSampler.PointWrap, material.Sampler);
        });
    }

    private static (Ff7FieldResources Field, Ff7FieldModelResources Model) OriginalDolphin()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original FFVII resources for the reached active water presentation.");
        Ff7Installation installation = Ff7Installation.Discover(root!);
        Ff7FieldResources field = Ff7FieldResources.Load(installation, "ujunon2");
        Assert.Equal(new byte[] { 0x28, 4, 8, 1 }, field.Scripts.Data.Span.Slice(0x0C43, 4).ToArray());
        using LgpArchive characters = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.CharacterArchive).Path);
        Ff7FieldModelResources model = Ff7FieldModelResources.Load(characters, field.Models.Models[8]);
        return (field with { Background = field.Background with { Layers = [] } }, model);
    }

    private static (Ff7FieldModelResources Model, PcFieldModelDescriptor Descriptor, PcFieldAnimation Animation) AuthoredModel()
    {
        HrcHierarchy hierarchy = HrcHierarchy.Read(Encoding.ASCII.GetBytes(":HEADER_BLOCK 2\n:SKELETON water\n:BONES 1\nbody\nroot\n200\n1 body\n"));
        PcPolygonModel polygon = new([Vector3.Zero, new(0, 0, -200), new(0, 0, -300)], [], [], [uint.MaxValue, uint.MaxValue, uint.MaxValue], [], [], [], [new(0, 0, 0, 0, 3, 0, 0, 0, ReadOnlyMemory<byte>.Empty)], ReadOnlyMemory<byte>.Empty);
        Ff7BoneGeometry part = new(0, "body.rsd", new("water", "body.p", [], new Dictionary<string, string>()), "body.p", polygon, []);
        byte[] bytes = new byte[72];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), 0x020001);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(60), 90);
        PcFieldAnimation animation = PcFieldAnimation.Read(bytes);
        Ff7FieldModelResources model = new("water.hrc", hierarchy, [part], new Dictionary<string, PcFieldAnimation> { ["water.a"] = animation });
        PcFieldModelDescriptor descriptor = new("water", 0, "water.hrc", "128", [new(0, Vector3.Zero), new(0, Vector3.Zero), new(0, Vector3.Zero)], uint.MaxValue, ["water.a"]);
        return (model, descriptor, animation);
    }

    private static PcFieldCamera Camera()
    {
        return new(Vector3.UnitX * 4096, Vector3.UnitY * 4096, Vector3.UnitZ * 4096, 0, new(0, 0, 1000), 0, 100);
    }

    private static Capture Draw(Ff7FieldRenderer renderer, Ff7FieldRenderFrame frame)
    {
        Capture capture = new();
        renderer.Render(capture, 320, 240, frame);
        return capture;
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
