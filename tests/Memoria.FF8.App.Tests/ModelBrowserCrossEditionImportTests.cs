using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Mods;

namespace Memoria.FF8.App.Tests;

public sealed class ModelBrowserCrossEditionImportTests
{
    [Theory]
    [InlineData("Classic")]
    [InlineData("Remastered")]
    public void ExportProviderEditionDoesNotPreventImportToCompatibleTarget(string providerEdition)
    {
        EditableModel original = ModelExchange.FromField(Field());
        EditableModel edited = original with
        {
            Meshes = original.Meshes.Select(mesh => mesh with { Vertices = mesh.Vertices.Select(vertex => vertex with { Position = vertex.Position * 2 }).ToArray() }).ToArray()
        };
        ModelAssetImportInventory inventory = RecreateGame.InspectBrowserModelImport(Export(edited, providerEdition));
        Assert.Equal(providerEdition, inventory.Edition);
        ModelAssetObject item = Assert.Single(inventory.Objects);
        ModelAssetClip clip = Assert.Single(inventory.Clips);
        EditableModel result = ModelMods.Preview(original, new(item.Target, item.Model, true, [new("embedded", 0, clip.Animation)]));
        Assert.Same(original.Bones, result.Bones);
        Vector3[] expected = ModelExchange.Pose(edited, 0, 0).SelectMany(mesh => mesh).ToArray();
        Vector3[] actual = ModelExchange.Pose(result, 0, 0).SelectMany(mesh => mesh).ToArray();
        Assert.Equal(expected.Length, actual.Length);
        Assert.All(expected.Zip(actual), pair => Assert.InRange(Vector3.Distance(pair.First, pair.Second), 0, 0.0001f));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CrossEditionImportStillRejectsIncompatibleTargetRig(bool changedHierarchy)
    {
        EditableModel source = ModelExchange.FromField(Field());
        ModelAssetObject item = Assert.Single(RecreateGame.InspectBrowserModelImport(Export(source, "Classic")).Objects);
        EditableModel target = source with
        {
            Bones = changedHierarchy ? [source.Bones[0], source.Bones[1] with { Parent = -1 }] : [source.Bones[0]]
        };
        Assert.Throws<NotSupportedException>(() => ModelMods.Preview(target, new(item.Target, item.Model, true, [])));
    }

    private static byte[] Export(EditableModel model, string edition)
    {
        const string target = "field/sample/0";
        ModelAssetObject item = new(target, "original", "", target, model);
        ModelAssetClip clip = new(target + "/clips/embedded/0", target, target, "embedded/0", "original", "", model.Animations[0], 1, 1f / model.NativeFramesPerSecond);
        return ModelAssetExchange.ExportGlb(new ModelAssetSelection([item], [clip]) { Edition = edition });
    }

    private static FieldModel Field()
    {
        ModelAnimation animation = new([new(Vector3.Zero, [Vector3.Zero, Vector3.Zero])]);
        return new("sample", [new(-1, 4), new(0, 2)], [new(Vector3.Zero, 0), new(Vector3.UnitX, 1), new(Vector3.UnitY, 1)], [new([0, 1, 2], [Vector2.Zero, Vector2.Zero, Vector2.Zero], 0, false)], [new PixelImage(1, 1)], [animation]);
    }
}
