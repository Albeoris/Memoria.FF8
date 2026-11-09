using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Assets;
using Albeoris.Games.FF8.Imaging;

namespace Memoria.FF8.Assets.Tests;

public sealed class FieldCharacterCatalogTests
{
    [Fact]
    public void SharedGeometryAndClipsKeepEveryNativeFieldBinding()
    {
        ModelCatalogEntry first = Entry("first");
        ModelCatalogEntry second = Entry("second");
        FieldModel model = Model();
        FieldCharacterCatalog catalog = FieldCharacterCatalog.Create([first, second], _ => model, TestContext.Current.CancellationToken);
        Assert.Single(catalog.Models);
        Assert.Single(catalog.Animations);
        Assert.Equal(2, catalog.Sources.Count);
        Assert.Equal(2, catalog.Animations[0].Bindings.Count);
        Assert.Equal(catalog.GetAnimation(first.Id, 0).Id, catalog.GetAnimation(second.Id, 0).Id);
        Assert.Equal(1f / 30, catalog.GetAnimation(first.Id, 0).SecondsPerFrame);
        FieldCharacterAnimationBank bank = catalog.CreateAnimationBank(first.Id);
        Assert.Single(bank.Animations);
        Assert.Equal(0, bank.ResolveSlot(second.Id, 0));
    }

    [Fact]
    public void EqualBoneCountDoesNotEstablishRigCompatibilityAndMaterialVariantsRemainDistinct()
    {
        FieldModel first = Model();
        FieldModel otherRig = first with { Bones = [new ModelBone(-1, 2)] };
        FieldModel costume = first with { Textures = [new PixelImage(1, 1)] };
        FieldCharacterCatalog catalog = FieldCharacterCatalog.Create([Entry("a"), Entry("b"), Entry("c")], entry => entry.Id switch { "a" => first, "b" => otherRig, _ => costume }, TestContext.Current.CancellationToken);
        Assert.Equal(3, catalog.Models.Count);
        Assert.Equal(2, catalog.Animations.Count);
        Assert.Single(catalog.CompatibleAnimations("a"));
        Assert.NotEqual(catalog.GetSource("a").RigId, catalog.GetSource("b").RigId);
        Assert.Equal(catalog.GetSource("a").GeometryId, catalog.GetSource("c").GeometryId);
        Assert.NotEqual(catalog.GetSource("a").MaterialId, catalog.GetSource("c").MaterialId);
    }

    [Fact]
    public void MalformedSourceIsDiagnosticAndCancellationIsNotSwallowed()
    {
        FieldCharacterCatalog catalog = FieldCharacterCatalog.Create([Entry("bad"), Entry("good")], entry => entry.Id == "bad" ? throw new InvalidDataException("bad original") : Model(), TestContext.Current.CancellationToken);
        Assert.Single(catalog.Sources);
        Assert.Equal("bad", Assert.Single(catalog.Diagnostics).SourceId);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => FieldCharacterCatalog.Create([Entry("a")], _ => Model(), cancellation.Token));
    }

    [Fact]
    public void InstalledFieldCatalogGroupsAllOriginalSourcesWithoutTextureResidency()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? Environment.GetEnvironmentVariable("FF8_GAME_DIRECTORY");
        Assert.SkipUnless(Directory.Exists(game), "Optional installed game resources are required.");
        using GameAssets assets = new(TestResources.GameDirectory);
        FieldCharacterCatalog catalog = assets.DiscoverFieldCharacters(cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotEmpty(catalog.Models);
        Assert.True(catalog.Sources.Count > catalog.Models.Count);
        Assert.Equal(0, assets.Resources.ResidentTextureCount);
        FieldCharacterSource corridor = catalog.Sources.First(source => source.Entry.ResourcePath == "field/bghall_1/chara.one" && source.Entry.Name.StartsWith("d000", StringComparison.OrdinalIgnoreCase));
        Assert.True(corridor.Clips.Count >= 3);
        Assert.NotEqual(catalog.GetAnimation(corridor.Entry.Id, 1).Id, catalog.GetAnimation(corridor.Entry.Id, 2).Id);
        Assert.Equal("field/main_chr/d000.mch", corridor.Entry.ModelLibraryResource);
        FieldCharacterAnimationBank bank = catalog.CreateAnimationBank(corridor.Entry.Id);
        Assert.Equal(1, bank.ResolveSlot(corridor.Entry.Id, 1));
        Assert.Equal(2, bank.ResolveSlot(corridor.Entry.Id, 2));
        ModelAnimation walked = assets.LoadFieldCharacterAnimation(catalog, corridor.Entry.Id, catalog.GetAnimation(corridor.Entry.Id, 1).Id);
        Assert.Equal(28, walked.Frames.Count);
        string? report = Environment.GetEnvironmentVariable("MEMORIA_FIELD_CHARACTER_CATALOG_REPORT");
        if (!string.IsNullOrWhiteSpace(report))
        {
            FieldModel native = assets.LoadFieldModelSource(corridor.Entry);
            Vector3[] posed = native.Pose(0, 0);
            var bounds = native.Vertices.Select((vertex, index) => (vertex.Bone, Position: posed[index])).GroupBy(item => item.Bone).Select(group => new { Bone = group.Key, Min = new[] { group.Min(item => item.Position.X), group.Min(item => item.Position.Y), group.Min(item => item.Position.Z) }, Max = new[] { group.Max(item => item.Position.X), group.Max(item => item.Position.Y), group.Max(item => item.Position.Z) } });
            File.WriteAllText(report, JsonSerializer.Serialize(new { Sources = catalog.Sources.Count, Models = catalog.Models.Count, Rigs = catalog.Models.Select(model => model.RigId).Distinct().Count(), Animations = catalog.Animations.Count, Diagnostics = catalog.Diagnostics, MainCharacters = catalog.Sources.Where(source => source.Entry.Name.StartsWith("d0", StringComparison.OrdinalIgnoreCase)).Select(source => new { source.Entry, source.ModelId, source.GeometryId, source.RigId, source.Clips }), Corridor = corridor, Compatible = catalog.CompatibleAnimations(corridor.Entry.Id), Rig = native.Bones, native.ControlBones, BoneBounds = bounds }, new JsonSerializerOptions { WriteIndented = false }));
        }
    }

    private static ModelCatalogEntry Entry(string id) => new(id, id, "Field", "field/" + id + "/chara.one", id);

    private static FieldModel Model() => new("original", [new ModelBone(-1, 1)], [], [], [], [new ModelAnimation([new AnimationFrame(Vector3.Zero, [Vector3.Zero])])]);
}
