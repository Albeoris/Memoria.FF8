using System.IO.Compression;
using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Tests;

namespace Memoria.FF8.Mods.Tests;

public sealed class ModelModCrossEditionTests
{
    [Theory]
    [InlineData("MEMORIA_FF8_GAME_DIR", "Mods.Tests.FieldModelPackage.1", false)]
    [InlineData("MEMORIA_FF8_REMASTERED_DIR", "Mods.Tests.FieldModelPackage.1", false)]
    [InlineData("MEMORIA_FF8_GAME_DIR", "Mods.Tests.FieldModelPackage.2", true)]
    [InlineData("MEMORIA_FF8_REMASTERED_DIR", "Mods.Tests.FieldModelPackage.2", true)]
    public void InstalledFieldPackageAppliesAllGeometryAndClips(string installationVariable, string fixtureId, bool canonicalPackage)
    {
        string? directory = Environment.GetEnvironmentVariable(installationVariable);
        Assert.SkipUnless(Directory.Exists(directory), $"Set {installationVariable} to run the optional installed package check.");
        using ZipArchive archive = ZipFile.OpenRead(TestResources.FixturePath(fixtureId));
        string settingsPath = Assert.Single(archive.Entries, entry => entry.FullName.EndsWith("/mod.ini", StringComparison.OrdinalIgnoreCase) || entry.FullName.Equals("mod.ini", StringComparison.OrdinalIgnoreCase)).FullName;
        string packageRoot = settingsPath[..^"mod.ini".Length];
        bool canonicalLayout = archive.Entries.Any(entry => entry.FullName.StartsWith(packageRoot + "models/", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(canonicalPackage, canonicalLayout);
        ModFile[] files = archive.Entries.Where(entry => entry.FullName.EndsWith(".glb", StringComparison.OrdinalIgnoreCase)).Select(entry =>
        {
            using Stream input = entry.Open();
            using MemoryStream buffer = new();
            input.CopyTo(buffer);
            byte[] bytes = buffer.ToArray();
            if (canonicalPackage)
            {
                ModelExchangeImportResult imported = ModelExchange.ImportGlb(bytes);
                Assert.NotNull(imported.Model.ImportTarget);
                Assert.Empty(imported.Warnings);
                Assert.StartsWith(packageRoot, entry.FullName);
            }
            string relativePath = canonicalLayout ? entry.FullName[packageRoot.Length..] : entry.FullName;
            return new ModFile("sample", ModFormat.Memoria, relativePath, 1, () => bytes);
        }).ToArray();
        Assert.NotEmpty(files);
        Assert.True(ModelMods.TryPrepare(new(1, [], files), out ModelMods mods, out var diagnostics), string.Join("; ", diagnostics));
        Assert.Equal(files.Length, mods.Variants.Count);
        using GameAssets assets = new(directory!);
        foreach (string modelId in mods.Variants.Select(variant => variant.ModelId).Distinct())
        {
            string[] address = modelId.Split('/');
            Assert.Equal("field", address[0]);
            FieldModel original = assets.LoadFieldModelSource(assets.GetFieldModelSource(address[1], int.Parse(address[2], System.Globalization.CultureInfo.InvariantCulture)));
            FieldModel result = mods.Apply(modelId, original);
            Assert.Same(original.Bones, result.Bones);
            Assert.Equal(original.Animations.Count, result.Animations.Count);
            Assert.NotNull(result.EditableOverride);
            foreach (ModelModVariant variant in mods.Variants.Where(variant => variant.ModelId == modelId && variant.ClipSlot is not null))
            {
                int slot = variant.ClipSlot!.Value;
                Assert.NotEmpty(result.Animations[slot].Frames);
                foreach (int frame in new[] { 0, result.Animations[slot].Frames.Count - 1 }.Distinct())
                {
                    Assert.All(result.Pose(slot, frame), position => Assert.True(float.IsFinite(position.X) && float.IsFinite(position.Y) && float.IsFinite(position.Z)));
                }
            }
        }
    }

    [Theory]
    [InlineData("bghoke_2", 1)]
    [InlineData("bghoke_3", 0)]
    [InlineData("bgroad_6", 0)]
    public void InstalledFieldRigsRetainSelectiveReplacementsAcrossEditions(string field, int modelIndex)
    {
        string? classicDirectory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? remasteredDirectory = Environment.GetEnvironmentVariable("MEMORIA_FF8_REMASTERED_DIR");
        Assert.SkipUnless(Directory.Exists(classicDirectory) && Directory.Exists(remasteredDirectory), "Set MEMORIA_FF8_GAME_DIR and MEMORIA_FF8_REMASTERED_DIR to run the optional cross-edition resource check.");
        using GameAssets classic = new(classicDirectory!);
        using GameAssets remastered = new(remasteredDirectory!);
        FieldModel classicModel = classic.LoadFieldModelSource(classic.GetFieldModelSource(field, modelIndex));
        FieldModel remasteredModel = remastered.LoadFieldModelSource(remastered.GetFieldModelSource(field, modelIndex));
        string target = $"field/{field}/{modelIndex}";
        Check(target, classicModel, remasteredModel, classic.Resources.Source.Edition.ToString());
        Check(target, remasteredModel, classicModel, remastered.Resources.Source.Edition.ToString());
    }

    private static void Check(string modelId, FieldModel source, FieldModel target, string sourceEdition)
    {
        EditableModel editable = ModelExchange.FromField(source);
        EditableAnimation changed = editable.Animations[0] with
        {
            Channels = editable.Animations[0].Channels.Select(channel => channel.Path == "translation" && editable.Bones[channel.Bone].Parent < 0 ? channel with { Values = channel.Values.Select(value => value + new Vector4(3, 0, 0, 0)).ToArray() } : channel).ToArray()
        };
        EditableAnimation[] expectedAnimations = editable.Animations.ToArray();
        expectedAnimations[0] = changed;
        EditableModel expected = editable with { Animations = expectedAnimations };
        ModelModInstallPlan plan = ModelModInstaller.Plan([new(modelId, editable, true, [new("embedded", 0, changed)])]);
        ModFile[] files = plan.Files.Select(file => new ModFile("sample", ModFormat.Memoria, file.RelativePath, 1, () => file.Content)).ToArray();
        CheckReplacement(files, modelId, source, target, expected);

        ModelAssetObject exportedObject = new(modelId, "original", "", modelId, editable);
        ModelAssetClip exportedClip = new(ModelAssetDiscoveryBindings.ClipTarget(modelId, "embedded", 0), modelId, modelId, "embedded/0", "original", "", changed, source.Animations[0].Frames.Count, 1f / editable.NativeFramesPerSecond);
        byte[] linkedExport = ModelAssetExchange.ExportGlb(new ModelAssetSelection([exportedObject], [exportedClip]) { Edition = sourceEdition });
        CheckReplacement([new("sample", ModFormat.Memoria, "exports/edited-character.glb", 1, () => linkedExport)], modelId, source, target, expected);
    }

    private static void CheckReplacement(ModFile[] files, string modelId, FieldModel source, FieldModel target, EditableModel expected)
    {
        Assert.True(ModelMods.TryPrepare(new(1, [], files), out ModelMods mods, out var diagnostics), string.Join("; ", diagnostics));
        FieldModel result = mods.Apply(modelId, target);
        Assert.Same(target.Bones, result.Bones);
        Assert.Equal(expected.Meshes.Count, result.EditableOverride!.Meshes.Count);
        for (int slot = 1; slot < target.Animations.Count; slot++)
        {
            Assert.Same(target.Animations[slot], result.Animations[slot]);
        }
        foreach (int frame in new[] { 0, source.Animations[0].Frames.Count / 2, source.Animations[0].Frames.Count - 1 }.Distinct())
        {
            Vector3[] expectedPositions = ModelExchange.Pose(expected, 0, frame / expected.NativeFramesPerSecond).SelectMany(mesh => mesh).Select(position => Vector3.Transform(position, Matrix4x4.CreateRotationX(MathF.PI / 2))).ToArray();
            Vector3[] actualPositions = result.Pose(0, frame);
            Assert.Equal(expectedPositions.Length, actualPositions.Length);
            Assert.All(expectedPositions.Zip(actualPositions), pair => Assert.InRange(Vector3.Distance(pair.First, pair.Second), 0, 0.01f));
            Assert.InRange(Vector3.Distance(source.Animations[0].Frames[frame].Translation + new Vector3(3, 0, 0), result.Animations[0].Frames[frame].Translation), 0, 0.001f);
        }
    }
}
