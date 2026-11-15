using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Mods.Tests;

public sealed class ModelModMetadataTests
{
    [Theory]
    [InlineData("long-nose.glb")]
    [InlineData("arbitrary/subfolder/renamed.GLB")]
    [InlineData("models/field/wrong/9.geometry.glb")]
    public void InstalledExportLoadsByMetadataAfterMovingOrRenaming(string path)
    {
        ModelModInstallPlan plan = ModelModInstaller.Plan([new("field/sample/0", Model(2), true, [new("embedded", 3, Animation())])]);
        ModFile[] files = plan.Files.Select((output, index) => File(index == 0 ? path : "motions/wave.glb", output.Content)).ToArray();
        Assert.True(ModelMods.TryPrepare(new(1, [], files), out ModelMods mods, out var diagnostics));
        Assert.Empty(diagnostics);
        Assert.All(mods.Variants, variant => Assert.Equal("field/sample/0", variant.ModelId));
        EditableModel original = Model(1) with { Animations = Enumerable.Range(0, 4).Select(_ => Animation()).ToArray() };
        EditableModel resolved = mods.Resolve("field/sample/0", original);
        Assert.Equal(2, resolved.Meshes[0].Vertices[1].Position.X);
        Assert.Equal(3, mods.Variants.Single(variant => variant.ClipSlot is not null).ClipSlot);
        ModelAssetImportInventory inventory = ModelAssetExchange.InspectGlb(plan.Files[1].Content);
        Assert.False(Assert.Single(inventory.Objects).GeometrySelected);
        Assert.Equal("field/sample/0/clips/embedded/3", Assert.Single(inventory.Clips).Target);
    }

    [Fact]
    public void UnknownPathWithoutMetadataRejectsWholePublicationWithFileDiagnostic()
    {
        byte[] bytes = ModelExchange.ExportGlb(Model(2));
        ModFile legacy = File("models/field/sample/0.glb", bytes);
        ModFile unknown = File("field/sample/0.glb", bytes);
        Assert.False(ModelMods.TryPrepare(new(1, [], [legacy, unknown]), out ModelMods mods, out var diagnostics));
        Assert.Empty(mods.Variants);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == "Memoria.InvalidModel" && diagnostic.PackageId == unknown.PackageId && diagnostic.RelativePath == unknown.RelativePath && diagnostic.Message.Contains("metadata"));
    }

    [Fact]
    public void RecognizedLegacyPathStillLoadsWithWarning()
    {
        ModFile file = File("models/field/sample/0.geometry.glb", ModelExchange.ExportGlb(Model(2)));
        Assert.True(ModelMods.TryPrepare(new(1, [], [file]), out ModelMods mods, out var diagnostics));
        Assert.True(Assert.Single(mods.Variants).GeometryOnly);
        Assert.Equal("Memoria.LegacyModelPath", Assert.Single(diagnostics).Code);
        Assert.Equal(2, mods.Resolve("field/sample/0", Model(1)).Meshes[0].Vertices[1].Position.X);
    }

    [Fact]
    public void DuplicateEmbeddedDestinationRejectsPackage()
    {
        byte[] bytes = Assert.Single(ModelModInstaller.Plan([new("field/sample/0", Model(2), true, [])]).Files).Content;
        Assert.False(ModelMods.TryPrepare(new(1, [], [File("a.glb", bytes), File("b.glb", bytes)]), out ModelMods mods, out var diagnostics));
        Assert.Empty(mods.Variants);
        Assert.Equal("Memoria.InvalidModel", Assert.Single(diagnostics).Code);
    }

    [Fact]
    public void LinkedBrowserExportLoadsDirectlyFromArbitraryPackagePath()
    {
        const string target = "field/sample/0";
        ModelAssetSelection selection = new([new(target, "original", "", "rig", Model(2))], [new(target + "/clips/embedded/3", target, "rig", "wave", "original", "", Animation(), 4, 1f / 30)]) { Edition = "Classic" };
        byte[] bytes = ModelAssetExchange.ExportGlb(selection);
        Assert.True(ModelMods.TryPrepare(new(1, [], [File("my-export.glb", bytes)]), out ModelMods mods, out var diagnostics), string.Join("; ", diagnostics.Select(item => item.Message)));
        Assert.Empty(diagnostics);
        Assert.Equal(2, mods.Variants.Count);
        EditableModel original = Model(1) with { Animations = Enumerable.Range(0, 4).Select(_ => Animation()).ToArray() };
        EditableModel resolved = mods.Resolve(target, original);
        Assert.Equal(2, resolved.Meshes[0].Vertices[1].Position.X);
        Assert.Equal(0.1f, resolved.Animations[3].Duration);
    }

    private static ModFile File(string path, byte[] bytes) => new("Memoria/sample", ModFormat.Memoria, path, 1, () => bytes);

    private static EditableAnimation Animation() => new("animation_3", [new(0, "translation", [0, 0.1f], [Vector4.Zero, new(0.2f, 0, 0, 0)])]);

    private static EditableModel Model(float width)
    {
        EditableVertex Vertex(Vector3 position) => new(position, Vector3.UnitZ, Vector2.Zero, Vector4.Zero, Vector4.Zero, Vector4.One);
        EditableMesh mesh = new("triangle", [Vertex(Vector3.Zero), Vertex(new(width, 0, 0)), Vertex(Vector3.UnitY)], [0, 1, 2], -1, Matrix4x4.Identity);
        return new("sample", "field", [new("bone_0", -1, Vector3.Zero, Quaternion.Identity, Vector3.One) { NativeIndex = 0 }], [mesh], [], [], JsonSerializer.SerializeToElement(new { }));
    }
}
