using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Mods.Tests;

public sealed class ModelModsTests
{
    [Theory]
    [InlineData("field/sample/0", true)]
    [InlineData("battle/c0m001.dat", true)]
    [InlineData("battle/a0stg001.x", true)]
    [InlineData("world/5", true)]
    [InlineData("world/static/73", true)]
    [InlineData("field/sample/-1", false)]
    [InlineData("field/../0", false)]
    [InlineData("world/64", false)]
    [InlineData("world/static/256", false)]
    [InlineData("battle/model.exe", false)]
    public void ModelAddressesHaveStablePortableShape(string address, bool valid)
    {
        Assert.Equal(valid, ModelMods.IsModelId(address));
    }

    [Fact]
    public void MalformedReplacementRejectsPublicationWithPackageDiagnostic()
    {
        ModFile file = new("Memoria/sample", ModFormat.Memoria, "models/field/sample/0.glb", 1, () => [0, 1, 2]);
        Assert.False(ModelMods.TryPrepare(new(1, [], [file]), out ModelMods result, out var diagnostics));
        Assert.Empty(result.Variants);
        ModDiagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal("Memoria.InvalidModel", diagnostic.Code);
        Assert.Equal(file.PackageId, diagnostic.PackageId);
        Assert.Equal(file.RelativePath, diagnostic.RelativePath);
    }

    [Fact]
    public void VariantsRetainCatalogPrecedenceAndCanBeLoadedIndependently()
    {
        EditableModel original = Model("original", 1);
        ModFile first = File("Memoria/first", ModelExchange.ExportGlb(Model("first", 2)));
        ModFile second = File("Memoria/second", ModelExchange.ExportGlb(Model("second", 3)));
        Assert.True(ModelMods.TryPrepare(new(1, [], [first, second]), out ModelMods result, out var diagnostics));
        Assert.Empty(diagnostics);
        Assert.Equal(new[] { "Memoria/first", "Memoria/second" }, result.Variants.Select(variant => variant.PackageId));
        EditableModel selected = result.Load(result.Variants[1], original);
        Assert.Equal(3, selected.Meshes[0].Vertices[1].Position.X);
    }

    [Fact]
    public void UnrelatedModelsKeepOriginalObjectAndRequireNoConversion()
    {
        FieldModel original = new("original", [], [], [], [], []);
        Assert.Same(original, ModelMods.Empty.Apply("field/sample/0", original));
    }

    private static ModFile File(string package, byte[] bytes) => new(package, ModFormat.Memoria, "models/field/sample/0.glb", 1, () => bytes);

    [Fact]
    public void DisabledPackagesCanBeInspectedWithoutActivatingTheirModelFiles()
    {
        string temporary = Path.Combine(Path.GetTempPath(), "Memoria.ModelInspection", Guid.NewGuid().ToString("N"));
        try
        {
            string package = Path.Combine(temporary, "Mods", "Memoria", "Sample");
            string models = Path.Combine(package, "models", "field", "sample");
            Directory.CreateDirectory(models);
            System.IO.File.WriteAllText(Path.Combine(package, "mod.ini"), "enabled=0");
            System.IO.File.WriteAllBytes(Path.Combine(models, "0.glb"), ModelExchange.ExportGlb(Model("disabled", 2)));
            using ModRuntime runtime = new(temporary, watch: false);
            runtime.PreparePending(true);
            runtime.CommitPending();
            Assert.DoesNotContain(runtime.Catalog.Files, file => file.PackageId == "Memoria/Sample");
            ModFile file = Assert.Single(runtime.ReadInstalledFiles(ModFormat.Memoria), file => file.PackageId == "Memoria/Sample");
            Assert.Equal("models/field/sample/0.glb", file.RelativePath);
            Assert.NotEmpty(ModelExchange.ImportGlb(file.ReadBytes()).Model.Meshes);
            Assert.Equal(ModEffectiveStatus.Disabled, Assert.Single(runtime.Catalog.Packages, package => package.Id == file.PackageId).Status);
        }
        finally
        {
            if (Directory.Exists(temporary))
            {
                Directory.Delete(temporary, true);
            }
        }
    }

    private static EditableModel Model(string name, float width)
    {
        EditableVertex Vertex(Vector3 position) => new(position, Vector3.UnitZ, Vector2.Zero, Vector4.Zero, Vector4.Zero, Vector4.One);
        EditableMesh mesh = new("triangle", [Vertex(Vector3.Zero), Vertex(new(width, 0, 0)), Vertex(Vector3.UnitY)], [0, 1, 2], -1, Matrix4x4.Identity);
        return new(name, "field", [], [mesh], [], [], JsonSerializer.SerializeToElement(new { }));
    }
}
