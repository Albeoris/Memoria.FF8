using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7WaterMappedAppearanceTests
{
    [Fact]
    public void InstalledSquallBindsWaterColorsToCommittedTargetVerticesAcrossSmoothedDrawPoses()
    {
        string? ff7 = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? ff8 = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ff7) || string.IsNullOrWhiteSpace(ff8), "Select the original source and replacement field resources.");
        using GameAssets assets = new(ff8!);
        Ff7Installation installation = Ff7Installation.Discover(ff7!);
        Ff7FieldResources field = Ff7FieldResources.Load(installation, "ujunon2");
        PcFieldModelDescriptor descriptor = field.Models.Models[0];
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.CharacterArchive).Path);
        Ff7FieldModelResources original = Ff7FieldModelResources.Load(archive, descriptor);
        Ff7ModelAppearance appearance = Ff7ModelAppearance.CreateStandardFieldSquall(installation, assets);
        Ff7ActorVisual pose = new(5, 0, 123 * 4096, 456 * 4096, -100 * 4096, 73, 0, 0) { PartyCharacter = 0 };
        Assert.True(appearance.TrySampleVertices(pose, descriptor, original, out IReadOnlyList<Vector3> sampled));
        Assert.NotEmpty(sampled);
        Assert.True(appearance.TryCreate(pose, descriptor, original, out IReadOnlyList<Ff7AppearanceTriangle> triangles));
        Assert.All(triangles.SelectMany(triangle => new[] { triangle.A, triangle.B, triangle.C }), vertex => Assert.Equal(sampled[vertex.AppearanceVertexIndex], vertex.Position));
        double threshold = (sampled.Min(vertex => vertex.Z) + sampled.Max(vertex => vertex.Z)) / 2d;
        Ff7FieldWaterColorRegion region = new(true, threshold, false);
        Ff7FieldWaterParameters parameters = new(-32, -64, -32, 0, 224, 3);
        Ff7NativeWaterEffectState water = new()
        {
            RequestedPayload = [1],
            Initialized = true,
            OwnsColors = true,
            DisplayedSample = new(parameters, original.Geometry.Select((part, index) => new Ff7FieldWaterPartMask(index, new bool[part.Polygon.Vertices.Count])).ToArray(), 0, 0)
        };
        Ff7ModelRenderOverrides effects = Ff7Scene.CreateWaterAppearanceEffects(water, appearance, pose, descriptor, original, region);
        Assert.NotNull(effects.AppearanceVertexColor);
        Assert.Contains(sampled, region.Contains);
        Assert.Contains(sampled, vertex => !region.Contains(vertex));
        for (int vertex = 0; vertex < sampled.Count; vertex++)
        {
            Assert.Equal(region.Contains(sampled[vertex]) ? 0x734070A0U : 0x738090A0U, effects.AppearanceVertexColor(vertex, 0x738090A0));
        }
        Assert.Throws<InvalidDataException>(() => effects.AppearanceVertexColor(sampled.Count, uint.MaxValue));
        Ff7ActorVisual interpolated = pose with { Z4096 = pose.Z4096 + 1000 * 4096, ModelEffects = effects };
        Assert.True(appearance.TryCreate(interpolated, descriptor, original, out IReadOnlyList<Ff7AppearanceTriangle> moved));
        Assert.All(moved.SelectMany(triangle => new[] { triangle.A, triangle.B, triangle.C }), vertex =>
        {
            Assert.True(Vector3.Distance(sampled[vertex.AppearanceVertexIndex] + new Vector3(0, 0, 1000), vertex.Position) < 0.001f);
            Assert.Equal(region.Contains(sampled[vertex.AppearanceVertexIndex]) ? 0x734070A0U : 0x738090A0U, effects.AppearanceVertexColor(vertex.AppearanceVertexIndex, 0x738090A0));
        });
    }
}
