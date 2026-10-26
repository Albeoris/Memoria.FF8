using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Mods.Localization;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Mods.Tests;

public sealed class LocalizationRuntimeTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Memoria.Localization.Tests", Guid.NewGuid().ToString("N"));
    private const string Hash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    private static ResourceSourceDescriptor Source => new("fixture", GameDataEdition.Classic, [], "en");

    [Fact]
    public void HighPriorityNonemptyTargetsWinAndBlankPatchFallsThrough()
    {
        ModPackage first = Package("base", Row("$en_sample_0000", "Перевод"));
        ModPackage second = Package("patch", Row("$en_sample_0000", ""), Row("$en_sample_0001", "訳"));
        ModCatalogSnapshot candidate = new(1, [first, second], []);
        LocalizationPreparation result = LocalizationRuntime.Prepare(candidate, [first.Id, second.Id], Source, TestContext.Current.CancellationToken);
        Assert.False(result.RequiresSelection);
        Assert.Equal("Перевод", result.Catalog.Resolve("$en_sample_0000", "native"));
        Assert.Equal("訳", result.Catalog.Resolve("$en_sample_0001", "native"));
        Assert.Equal("native", result.Catalog.Resolve("missing", "native"));
        LocalizationPreparation removed = LocalizationRuntime.Prepare(new(2, [second], []), [second.Id], Source, TestContext.Current.CancellationToken);
        Assert.Equal("native", removed.Catalog.Resolve("$en_sample_0000", "native"));
    }

    [Fact]
    public void NewMultiplePackagesNeedAnExplicitSelection()
    {
        ModPackage first = Package("base", Row("$en_sample_0000", "base"));
        ModPackage patch = Package("patch", Row("$en_sample_0000", "patch"));
        LocalizationPreparation pending = LocalizationRuntime.Prepare(new(1, [first, patch], []), null, Source, TestContext.Current.CancellationToken);
        Assert.True(pending.RequiresSelection);
        LocalizationPreparation accepted = LocalizationRuntime.Prepare(new(1, [first, patch], []), [patch.Id, first.Id], Source, TestContext.Current.CancellationToken);
        Assert.False(accepted.RequiresSelection);
        Assert.Equal("base", accepted.Catalog.Resolve("$en_sample_0000", "native"));
    }

    [Fact]
    public void FieldBindingsRequireTheNativeScriptAndKeepNativeChoiceOperandsIndependent()
    {
        ModPackage package = Package("field", FieldRow("$en_bgroom_6_0000", "{X40Y50F2L3B2C255}Заголовок{Line}Подсказка{Line}Да{Line}Нет"));
        LocalizationPreparation prepared = LocalizationRuntime.Prepare(new(1, [package], []), [package.Id], Source, TestContext.Current.CancellationToken);
        LocalizationFieldText? translated = prepared.Catalog.ResolveField("$en_bgroom_6_0000", 9, Hash, 0, 1, 0, 255);
        Assert.NotNull(translated);
        Assert.Equal(2, translated.FirstChoice);
        Assert.Equal(3, translated.LastChoice);
        Assert.Equal(255, translated.CancelChoice);
        Assert.Null(prepared.Catalog.ResolveField("$en_bgroom_6_0000", 9, "wrong-script", 0, 1, 0, 255));
        Assert.Null(prepared.Catalog.ResolveField("$en_bgroom_6_0000", 10, Hash, 0, 1, 0, 255));
        Assert.Null(prepared.Catalog.ResolveField("$en_bgroom_6_0000", 9, Hash, 0, 2, 0, 255));
    }

    [Theory]
    [InlineData("Да{Line}Нет")]
    [InlineData("{F0L2B0C255}Да{Line}Нет{Line}Ещё")]
    [InlineData("{F2L3B3C255}Заголовок{Line}Подсказка{Line}Да{Line}Нет")]
    [InlineData("{F2L3B2C255}Да{Line}Нет")]
    public void IncompatibleChoiceEditsAreRejectedBeforePublication(string target)
    {
        ModPackage package = Package("bad-choice", FieldRow("$en_bgroom_6_0000", target));
        Assert.Throws<InvalidDataException>(() => LocalizationRuntime.Prepare(new(1, [package], []), [package.Id], Source, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ABaseInTheHigherLayerTakesPriorityOverALowerContextVariant()
    {
        LocalizationValue Value(string text) => new(text, "fixture", "project") { Field = new(text, Hash, null, null, null, null, null, null, null, null, [new(9, 0, false, null, null, null, null)]) };
        Dictionary<string, LocalizationValue> lower = new() { ["$en_bgroom_6_0000_ctx_01234567"] = Value("lower variant") with { Field = Value("lower variant").Field! with { BaseKey = "$en_bgroom_6_0000" } } };
        Dictionary<string, LocalizationValue> higher = new() { ["$en_bgroom_6_0000"] = Value("higher base") };
        LocalizationCatalog catalog = new(lower.Concat(higher), [lower, higher]);
        Assert.Equal("higher base", catalog.ResolveField("$en_bgroom_6_0000", 9, Hash, null, null, null, null)!.Body);
        LocalizationCatalog reversed = new(lower.Concat(higher), [higher, lower]);
        Assert.Equal("lower variant", reversed.ResolveField("$en_bgroom_6_0000", 9, Hash, null, null, null, null)!.Body);
        Assert.NotEqual(catalog.Fingerprint, reversed.Fingerprint);
    }

    [Fact]
    public void SourceOnlyEditsDoNotChangeEffectiveTextFingerprint()
    {
        LocalizationRecord original = Row("$en_sample_0000", "Перевод");
        ModPackage first = Package("first", original);
        ModPackage second = Package("second", original with { Source = "changed reference" });
        LocalizationPreparation a = LocalizationRuntime.Prepare(new(1, [first], []), [first.Id], Source, TestContext.Current.CancellationToken);
        LocalizationPreparation b = LocalizationRuntime.Prepare(new(1, [second], []), [second.Id], Source, TestContext.Current.CancellationToken);
        Assert.Equal(a.Catalog.Fingerprint, b.Catalog.Fingerprint);
    }

    [Fact]
    public void ResourcePublicationChangesLocalizationAtomicallyAndCanRemoveAllOverrides()
    {
        using InMemoryResourceSource source = new(Source);
        ResourceService resources = new(source);
        List<ResourceChangeSet> changes = [];
        using IDisposable subscription = resources.Subscribe(changes.Add);
        LocalizationCatalog translated = new(new Dictionary<string, LocalizationValue> { ["$en_sample_0000"] = new("訳", "package", "project") });
        ResourceService.PreparedResources pending = resources.Prepare(null, localization: translated);
        Assert.Equal("native", resources.Localize("$en_sample_0000", "native"));
        pending.Commit();
        Assert.Equal("訳", resources.Localize("$en_sample_0000", "native"));
        Assert.Contains(new ResourceKey("localization/catalog"), changes.Single().Texts);
        resources.Prepare(null, localization: LocalizationCatalog.Empty).Commit();
        Assert.Equal("native", resources.Localize("$en_sample_0000", "native"));
    }

    private ModPackage Package(string name, params LocalizationRecord[] rows)
    {
        string path = Path.Combine(root, name);
        LocalizationProject project = new() { Manifest = new() { SourceLanguage = "en", TargetLanguage = "ru (fixture)", MappingVersion = FieldCorrespondenceCatalog.Version, CompatibleVariants = ["Classic:en"] }, Entries = rows };
        LocalizationProjectIO.WriteNew(project, path, "json", "single", TestContext.Current.CancellationToken);
        return new("Memoria/" + name, name, ModFormat.Memoria, true, true, ModEffectiveStatus.Active, path) { HasLocalization = true, LocalizationOnly = true };
    }

    private static LocalizationRecord Row(string key, string target) => new() { Category = "Menus", File = "sample", Key = key, Source = "source", Target = target };

    private static LocalizationRecord FieldRow(string key, string target)
    {
        FieldCompatibilityContext call = new(new(0, 0, 0, 0, 9, 36), "ASK", [0, 0, 0, 1, 0, 255], 0, true, false, [], null);
        return new() { Category = "Fields", File = "bgroom_6", Key = key, Source = "Да{Line}Нет", Target = target, ExtensionData = new Dictionary<string, JsonElement> { ["sourceContext"] = JsonSerializer.SerializeToElement(new Dictionary<string, string> { ["JSM SHA256"] = Hash, ["Call Contexts"] = JsonSerializer.Serialize(new[] { call }) }) } };
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}
