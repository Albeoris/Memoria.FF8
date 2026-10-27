using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Mods.Localization;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Mods.Tests;

public sealed class LocalizationReferenceRuntimeTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Memoria.ReferenceRuntime.Tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("json")]
    [InlineData("xlsx")]
    public void OnlyBaseTargetIsPublishedAndHiddenBaseControlsRemainAuthoritative(string format)
    {
        LocalizationProject project = new()
        {
            Manifest = new() { SchemaVersion = 2, TargetLanguage = "ru", MappingVersion = FieldCorrespondenceCatalog.Version, ReferenceLanguages = new Dictionary<string, string> { ["Reference [jp]"] = "jp", ["Reference [en]"] = "en" } },
            Entries = [new() { Category = "Magic", File = "sample", Key = "$en_sample_0000", Source = "Base {Var 1}", Target = "Перевод {Var 1}", References = new Dictionary<string, string?> { ["Reference [jp]"] = "日本語 {Var 2}", ["Reference [en]"] = "Base {Var 1}" }, ReferenceKeys = new Dictionary<string, IReadOnlyList<string>> { ["Reference [jp]"] = new[] { "$jp_sample_0007" }, ["Reference [en]"] = new[] { "$en_sample_0000" } } }]
        };
        LocalizationProjectIO.WriteNew(project, root, format, "single", TestContext.Current.CancellationToken);
        ModPackage package = new("Memoria/Reference", "Reference", ModFormat.Memoria, true, true, ModEffectiveStatus.Active, root) { HasLocalization = true, LocalizationOnly = true };
        ModCatalogSnapshot candidate = new(1, [package], []);
        ResourceSourceDescriptor source = new("fixture", GameDataEdition.Classic, [], "en");
        LocalizationPreparation prepared = LocalizationRuntime.Prepare(candidate, [package.Id], source, TestContext.Current.CancellationToken);
        Assert.Equal("Перевод {Var 1}", prepared.Catalog.Resolve("$en_sample_0000", "native"));
        Assert.Equal("native", prepared.Catalog.Resolve("$jp_sample_0007", "native"));
        Assert.Single(prepared.Catalog.Values);
        Assert.Throws<InvalidDataException>(() => LocalizationRuntime.Prepare(candidate, [package.Id], source with { Language = "jp" }, TestContext.Current.CancellationToken));

        LocalizationProject loaded = LocalizationProjectIO.Read(root);
        LocalizationRecord row = Assert.Single(loaded.Entries);
        Assert.Equal("Base {Var 1}", row.Source);
        Assert.Throws<InvalidDataException>(() => LocalizationTextValidation.Validate(row with { Target = "日本語 {Var 2}" }));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}
