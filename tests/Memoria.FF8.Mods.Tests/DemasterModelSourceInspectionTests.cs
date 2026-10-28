using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class DemasterModelSourceInspectionTests
{
    [Fact]
    public void SelectedModelCapabilitiesDoNotEnableUnrelatedDonorResourceFamilies()
    {
        ResourceSourceDescriptor primary = new("primary", GameDataEdition.Classic, []);
        ResourceSourceDescriptor donor = new("models", GameDataEdition.Remastered, ["native-field-model-sheet", "native-battle-model-sheet", "native-battle-body-sheet", "native-field-background-page", "demaster-direct"]);
        string[] targets = ["textures/field.fs/field_hd/n001_0.png", "textures/battle.fs/hd_new/d0c000_0.png", "textures/field_bg/bgroom_1_0.png", "textures/arbitrary.png"];
        ModFile[] files = targets.Select((path, index) => new ModFile($"package-{index}", ModFormat.Demaster, path, 1, () => [])).ToArray();
        ModCatalogSnapshot catalog = new(1, [], files);
        DemasterInspection baseline = DemasterTextureResolver.Inspect(primary, catalog);
        Assert.Empty(baseline.UsablePackageIds);
        DemasterInspection selected = DemasterTextureResolver.Inspect(primary, catalog, donor);
        Assert.Contains("package-0", selected.UsablePackageIds);
        Assert.Contains("package-1", selected.UsablePackageIds);
        Assert.DoesNotContain("package-2", selected.UsablePackageIds);
        Assert.DoesNotContain("package-3", selected.UsablePackageIds);
        Assert.Equal(new[] { "package-2", "package-3" }, selected.Diagnostics.Select(item => item.PackageId));
        Assert.Equal(GameDataEdition.Classic, primary.Edition);
        Assert.Empty(primary.Layouts);
    }
}
