using Memoria.FF8.Assets;

namespace Memoria.FF8.Assets.Tests;

public sealed class ModelCatalogTests
{
    [Fact]
    public void InstalledCatalogDiscoversCategoriesAndExportsOneModelWithoutLoadingAScene()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "An optional installed game directory is required.");
        using GameAssets assets = new(directory);
        ModelCatalogEntry[] entries = assets.EnumerateModelCatalog().ToArray();
        Assert.Contains(entries, entry => entry.Category == "Field" && entry.LoadError is null);
        Assert.Contains(entries, entry => entry.Category == "Battle enemy");
        Assert.Contains(entries, entry => entry.Category == "Battle weapon");
        Assert.Contains(entries, entry => entry.Category == "Battle stage");
        Assert.Contains(entries, entry => entry.Category == "World character");
        Assert.Contains(entries, entry => entry.Category == "World static");
        Assert.Equal(entries.Length, entries.Select(entry => entry.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains(entries, entry => entry.Category == "Battle enemy" && entry.Name != Path.GetFileName(entry.ResourcePath));
        Assert.Contains(entries, entry => entry.SearchText.Contains("Squall", StringComparison.OrdinalIgnoreCase));
        string id = Environment.GetEnvironmentVariable("MEMORIA_FF8_MODEL_CATALOG_SMOKE_ID") ?? "world/0";
        ModelCatalogEntry selected = Assert.Single(entries, entry => entry.Id == id);
        EditableModel model = assets.LoadCatalogModel(selected);
        Assert.NotEmpty(model.Meshes);
        byte[] glb = ModelExchange.ExportGlb(model);
        ModelExchangeImportResult roundTrip = ModelExchange.ImportGlb(glb, model);
        Assert.False(roundTrip.RequiresBoneCountApproval);
        Assert.Equal(model.Bones.Count(bone => bone.IsJoint), roundTrip.Model.Bones.Count(bone => bone.IsJoint));
        Assert.Equal(model.Meshes.Sum(mesh => mesh.Indices.Count), roundTrip.Model.Meshes.Sum(mesh => mesh.Indices.Count));
    }
}
