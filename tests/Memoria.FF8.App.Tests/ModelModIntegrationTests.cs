using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Mods;

namespace Memoria.FF8.App.Tests;

public sealed class ModelModIntegrationTests
{
    private static readonly Rgba32 ReplacementColor = new(231, 37, 113, 255);

    [Fact]
    public void InstalledModelReplacementsReachRuntimeLoadersAndDisablingRestoresOriginals()
    {
        string gameDirectory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(gameDirectory), "An optional installed game directory is required.");
        string root = Path.Combine(Path.GetTempPath(), "ff8-model-integration-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            VerifyRuntimeLoaders(gameDirectory, root);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void VerifyRuntimeLoaders(string gameDirectory, string root)
    {
        using GameAssets originals = new(gameDirectory);
        ModelCatalogEntry fieldEntry = originals.EnumerateModelCatalog().First(entry => entry.Id == "field/bghoke_1/0");
        int fieldId = fieldEntry.FieldId!.Value;
        FieldModel originalField = originals.LoadField(fieldId).Models[0];
        using BattleAssets originalBattleAssets = new(gameDirectory);
        BattleModel originalBattle = originalBattleAssets.LoadEnemy(0);
        BattleStage originalStage = originalBattleAssets.LoadStage(0);
        using WorldAssets originalWorldAssets = new(gameDirectory);
        FieldModel originalWorld = originalWorldAssets.GetFootModel(0);
        WorldStaticModel originalStatic = originalWorldAssets.ReadScriptedStaticModel(73);
        Dictionary<string, EditableModel> edits = new()
        {
            [fieldEntry.Id] = Edit(ModelExchange.FromField(originalField)),
            ["battle/c0m000.dat"] = Edit(ModelExchange.FromBattle(originalBattle)),
            ["battle/a0stg000.x"] = Edit(ModelExchange.FromStage(originalStage)),
            ["world/0"] = Edit(ModelExchange.FromField(originalWorld)),
            ["world/static/73"] = Edit(ModelExchange.FromWorld(originalStatic))
        };
        string modsDirectory = Path.Combine(root, "Mods");
        const string packageName = "000-model-integration";
        string package = Path.Combine(modsDirectory, "Memoria", packageName);
        Directory.CreateDirectory(package);
        File.WriteAllText(Path.Combine(package, "mod.ini"), "[Mod]\nEnabled=1\n");
        foreach ((string id, EditableModel model) in edits)
        {
            string path = Path.Combine(package, "models", id.Replace('/', Path.DirectorySeparatorChar) + ".glb");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, ModelExchange.ExportGlb(model));
        }

        using GameResourceHost host = new(gameDirectory, root, watch: false, modsDirectory: modsDirectory);
        string packageId = "Memoria/" + packageName;
        Assert.Contains(host.Mods.Catalog.Packages, item => item.Id == packageId && item.Status == ModEffectiveStatus.Active);
        foreach (string id in edits.Keys)
        {
            Assert.Equal(packageId, host.ModelMods.Variants.First(variant => variant.ModelId == id).PackageId);
        }
        using GameAssets fields = new(gameDirectory, resources: host.Resources, modelOverrides: host);
        using BattleAssets battle = new(gameDirectory, host.Resources, host);
        using WorldAssets world = new(gameDirectory, host.Resources, host);
        FieldModel fieldReplacement = fields.LoadField(fieldId).Models[0];
        BattleModel battleReplacement = battle.LoadEnemy(0);
        FieldModel worldReplacement = world.GetFootModel(0);
        WorldStaticModel staticReplacement = world.ReadScriptedStaticModel(73);
        BattleStage stageReplacement = battle.LoadStage(0);

        Assert.NotNull(fieldReplacement.EditableOverride);
        Assert.NotNull(battleReplacement.EditableOverride);
        Assert.NotNull(worldReplacement.EditableOverride);
        AssertPose(edits[fieldEntry.Id], fieldReplacement.Pose(0, 0));
        AssertPose(edits["battle/c0m000.dat"], battleReplacement.Pose(0, 0).SelectMany(mesh => mesh).ToArray());
        AssertPose(edits["world/0"], worldReplacement.Pose(0, 0));
        AssertPose(edits["world/static/73"], staticReplacement.NativeVertices, animation: -1);
        AssertPose(edits["battle/a0stg000.x"], stageReplacement.Meshes.SelectMany(mesh => mesh.Vertices.Select(vertex => vertex.Position)).ToArray(), animation: -1);
        Assert.All(new[] { fieldReplacement.Textures, battleReplacement.Textures, worldReplacement.Textures, staticReplacement.Textures, stageReplacement.Textures }, textures =>
        {
            Assert.NotEmpty(textures);
            Assert.All(textures, texture => Assert.Equal(ReplacementColor, texture[0, 0]));
        });
        Assert.Equal(originalField.ControlBones, fieldReplacement.ControlBones);
        Assert.Equal(originalField.Animations.Count, fieldReplacement.Animations.Count);
        Assert.Equal(originalBattle.Animations.Count, battleReplacement.Animations.Count);
        Assert.Null(originalField.EditableOverride);
        Assert.Null(originalBattle.EditableOverride);
        Assert.Null(originalWorld.EditableOverride);

        Assert.True(host.Mods.SetEnabled(packageId, false).Success);
        Assert.True(host.Pump(true));
        FieldModel restoredField = fields.LoadField(fieldId).Models[0];
        BattleModel restoredBattle = battle.LoadEnemy(0);
        FieldModel restoredWorld = world.GetFootModel(0);
        WorldStaticModel restoredStatic = world.ReadScriptedStaticModel(73);
        BattleStage restoredStage = battle.LoadStage(0);
        Assert.Null(restoredField.EditableOverride);
        Assert.Null(restoredBattle.EditableOverride);
        Assert.Null(restoredWorld.EditableOverride);
        Assert.Equal(originalField.Vertices.Select(vertex => vertex.Position), restoredField.Vertices.Select(vertex => vertex.Position));
        Assert.Equal(originalBattle.Meshes.SelectMany(mesh => mesh.Vertices.Select(vertex => vertex.Position)), restoredBattle.Meshes.SelectMany(mesh => mesh.Vertices.Select(vertex => vertex.Position)));
        Assert.Equal(originalWorld.Vertices.Select(vertex => vertex.Position), restoredWorld.Vertices.Select(vertex => vertex.Position));
        Assert.Equal(originalStatic.NativeVertices, restoredStatic.NativeVertices);
        Assert.Equal(originalStage.Meshes.SelectMany(mesh => mesh.Vertices.Select(vertex => vertex.Position)), restoredStage.Meshes.SelectMany(mesh => mesh.Vertices.Select(vertex => vertex.Position)));
    }

    private static EditableModel Edit(EditableModel model)
    {
        Vector3 offset = new(173, 29, -43);
        return model with
        {
            Meshes = model.Meshes.Select(mesh => mesh with { Vertices = mesh.Vertices.Select(vertex => vertex with { Position = vertex.Position + offset }).ToArray(), Texture = mesh.Texture < 0 ? 0 : mesh.Texture }).ToArray(),
            Textures = model.Textures.Count == 0 ? [new PixelImage(1, 1, [ReplacementColor])] : model.Textures.Select(texture => new PixelImage(texture.Width, texture.Height, Enumerable.Repeat(ReplacementColor, texture.Width * texture.Height).ToArray())).ToArray()
        };
    }

    private static void AssertPose(EditableModel expected, IReadOnlyList<Vector3> actual, int animation = 0)
    {
        Vector3[] vertices = ModelExchange.Pose(expected, animation, 0).SelectMany(mesh => mesh).Select(position => Vector3.Transform(position, Matrix4x4.CreateRotationX(MathF.PI / 2))).ToArray();
        Assert.Equal(vertices.Length, actual.Count);
        foreach (int index in new[] { 0, vertices.Length / 2, vertices.Length - 1 }.Distinct())
        {
            Assert.True(Vector3.Distance(vertices[index], actual[index]) < 0.05f, $"Runtime vertex {index}: expected {vertices[index]}, actual {actual[index]}.");
        }
    }
}
