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
    public void EditionProjectionUsesCanonicalTargetsAndEditionOnlyRowsAfterConversion()
    {
        const string common = "$en_sample_0000";
        const string classicOnly = "$en_sample_0001";
        const string remasteredCommon = "$en_sample_0002";
        const string remasteredOnly = "$en_sample_0003";
        string initial = Path.Combine(root, "shared-source");
        string converted = Path.Combine(root, "shared-workbook");
        LocalizationProject project = new()
        {
            Manifest = new() { SourceLanguage = "en", TargetLanguage = "ru", MappingVersion = FieldCorrespondenceCatalog.Version, CompatibleVariants = ["Classic:en", "Remastered:en"], EditionBindingPaths = new Dictionary<string, string> { ["Remastered:en"] = "Localization/EditionBindings/Remastered.en.json" } },
            Entries = [Row(common, "Общий"), Row(classicOnly, "Классика")],
            EditionBindings = new Dictionary<string, IReadOnlyList<LocalizationEditionBinding>> { ["Remastered:en"] = [new(Row(remasteredCommon, ""), common), new(Row(remasteredOnly, "Ремастер"))] }
        };
        LocalizationProjectIO.WriteNew(project, initial, "json", "split", TestContext.Current.CancellationToken);
        LocalizationProjectIO.Convert(initial, converted, "xlsx", "single", TestContext.Current.CancellationToken);
        ModPackage package = new("shared", "shared", ModFormat.Memoria, true, true, ModEffectiveStatus.Active, converted) { HasLocalization = true, LocalizationOnly = true };
        ModCatalogSnapshot snapshot = new(1, [package], []);
        LocalizationPreparation classic = LocalizationRuntime.Prepare(snapshot, [package.Id], Source, TestContext.Current.CancellationToken);
        LocalizationPreparation remastered = LocalizationRuntime.Prepare(snapshot, [package.Id], Source with { Edition = GameDataEdition.Remastered }, TestContext.Current.CancellationToken);

        Assert.Equal("Общий", classic.Catalog.Resolve(common, "original"));
        Assert.Equal("Классика", classic.Catalog.Resolve(classicOnly, "original"));
        Assert.Equal("original", classic.Catalog.Resolve(remasteredOnly, "original"));
        Assert.Equal("Общий", remastered.Catalog.Resolve(remasteredCommon, "original"));
        Assert.Equal("Ремастер", remastered.Catalog.Resolve(remasteredOnly, "original"));
        Assert.Equal("original", remastered.Catalog.Resolve(classicOnly, "original"));
        Assert.Equal(new[] { common, classicOnly }, LocalizationProjectIO.Read(converted).Entries.Select(row => row.Key));
    }

    [Fact]
    public void ExplicitEmptyShadowsAWinningTranslationAndKeepsItsContributor()
    {
        const string key = "$en_kernel_misctexts_text_0119";
        LocalizationRecord suffix = new() { Category = "Battle", File = "kernel_misctexts_text", Key = key, Source = "s", Target = "translated" };
        ModPackage first = Package("suffix", suffix);
        ModPackage second = Package("suppress", suffix with { Target = LocalizationValue.EmptyTarget });
        LocalizationPreparation result = LocalizationRuntime.Prepare(new(1, [first, second], []), [first.Id, second.Id], Source, TestContext.Current.CancellationToken);

        Assert.Equal(LocalizationValue.EmptyTarget, LocalizationProjectIO.Read(second.RootDirectory).Entries.Single().Target);
        Assert.Equal("", result.Catalog.Resolve(key, "s"));
        Assert.Equal("", result.Catalog.ResolveAliases([key], "s"));
        BoundLocalizationText bound = result.Catalog.ResolveText(key, "s");
        Assert.Equal("", bound.Text);
        Assert.Equal(second.Id, bound.Binding.PackageId);
    }

    [Fact]
    public void InvalidTargetShowsADiagnosticAndPreservesNativeControls()
    {
        const string key = "$en_sample_0000";
        string target = LocalizationValue.InvalidTarget + "{Line}{Var 2}";
        ModPackage package = Package("invalid", Row(key, target) with { Source = "Test{Line}{Var 2}", SourcePresent = true });
        LocalizationPreparation result = LocalizationRuntime.Prepare(new(1, [package], []), [package.Id], Source, TestContext.Current.CancellationToken);

        Assert.Equal(target, LocalizationProjectIO.Read(package.RootDirectory).Entries.Single().Target);
        Assert.Equal("[Служебная строка. Этот текст не предназначен для показа игроку.]{Line}{Var 2}", result.Catalog.Resolve(key, "native"));
        Assert.Equal(result.Catalog.Resolve(key, "native"), result.Catalog.ResolveText(key, "native").Text);
    }

    [Theory]
    [InlineData("The value is {Var 2}", "{Empty}")]
    [InlineData("source", "Text {Empty}")]
    public void EmptyTargetCannotRemoveRequiredArgumentsOrAppearWithinProse(string source, string target)
    {
        Assert.Throws<InvalidDataException>(() => LocalizationTextValidation.Validate(Row("$en_sample_0000", target) with { Source = source, SourcePresent = true }));
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
    public void DefaultOrderPlacesTheDeclaredBaseAboveItsPatchButKeepsAnExplicitOrder()
    {
        ModPackage parent = Package("z-base", Row("$en_sample_0000", "base"));
        ModPackage patch = Package("a-patch", Row("$en_sample_0000", "patch"));
        string manifestPath = Path.Combine(patch.RootDirectory, "Localization", "localization.manifest.json");
        var manifest = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(manifestPath))!;
        manifest["baseProjectId"] = LocalizationProjectIO.Read(parent.RootDirectory).Manifest.ProjectId;
        File.WriteAllText(manifestPath, manifest.ToJsonString());
        ModCatalogSnapshot candidate = new(1, [patch, parent], []);
        LocalizationPreparation suggested = LocalizationRuntime.Prepare(candidate, null, Source, TestContext.Current.CancellationToken);
        Assert.True(suggested.RequiresSelection);
        Assert.Equal(new[] { parent.Id, patch.Id }, suggested.RequestedOrder);
        Assert.Equal("patch", suggested.Catalog.Resolve("$en_sample_0000", "native"));
        LocalizationPreparation explicitOrder = LocalizationRuntime.Prepare(candidate, [patch.Id, parent.Id], Source, TestContext.Current.CancellationToken);
        Assert.Equal("base", explicitOrder.Catalog.Resolve("$en_sample_0000", "native"));
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

    [Fact]
    public void ExplicitEmptyKeepsTheFieldMessageBinding()
    {
        const string key = "$en_bgroom_6_0000";
        ModPackage package = Package("field-empty", FieldRow(key, LocalizationValue.EmptyTarget, choice: false));
        LocalizationPreparation prepared = LocalizationRuntime.Prepare(new(1, [package], []), [package.Id], Source, TestContext.Current.CancellationToken);

        LocalizationFieldText? translated = prepared.Catalog.ResolveField(key, 9, Hash, null, null, null, null);
        Assert.NotNull(translated);
        Assert.Equal("", translated.Body);
    }

    [Fact]
    public void OneTranslationKeepsDistinctOriginalChoiceRangesWhenItsLinesStayInPlace()
    {
        const string key = "$en_bgroom_6_0000";
        FieldCompatibilityContext first = new(new(0, 0, 0, 0, 9, 36), "ASK", [0, 0, 0, 1, 0, 1], 0, true, false, [], null);
        FieldCompatibilityContext second = new(new(0, 0, 0, 0, 12, 48), "ASK", [0, 0, 1, 2, 0, 2], 0, true, false, [], null);
        LocalizationRecord row = FieldRow(key, "Один{Line}Два{Line}Три") with
        {
            Source = "One{Line}Two{Line}Three",
            SourcePresent = true,
            ExtensionData = new Dictionary<string, JsonElement> { ["sourceContext"] = JsonSerializer.SerializeToElement(new Dictionary<string, string> { ["JSM SHA256"] = Hash, ["Call Contexts"] = JsonSerializer.Serialize(new[] { first, second }) }) }
        };
        ModPackage package = Package("shared-choice-shape", row);
        LocalizationPreparation prepared = LocalizationRuntime.Prepare(new(1, [package], []), [package.Id], Source, TestContext.Current.CancellationToken);

        Assert.Equal(row.Target, prepared.Catalog.ResolveField(key, 9, Hash, 0, 1, 0, 1)?.Body);
        LocalizationFieldText? translated = prepared.Catalog.ResolveField(key, 12, Hash, 1, 2, 0, 2);
        Assert.NotNull(translated);
        Assert.Equal(row.Target, translated.Body);
        Assert.Null(translated.InitialChoice);
        Assert.Null(prepared.Catalog.ResolveField(key, 12, Hash, 1, 2, 1, 2));
    }

    [Theory]
    [InlineData("Да{Line}Нет")]
    [InlineData("{F0L2B0C255}Да{Line}Нет{Line}Ещё")]
    [InlineData("{F2L3B3C255}Заголовок{Line}Подсказка{Line}Да{Line}Нет")]
    [InlineData("{F2L3B2C255}Да{Line}Нет")]
    [InlineData("{Empty}")]
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
        LocalizationPreparation a = LocalizationRuntime.Prepare(new(1, [first], []), [first.Id], Source, TestContext.Current.CancellationToken);
        LocalizationManifest manifest = LocalizationProjectIO.Read(first.RootDirectory).Manifest;
        string payload = Path.Combine(first.RootDirectory, manifest.Payloads.Single().Path);
        var document = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(payload))!;
        string sourceColumn = manifest.SchemaVersion == 1 ? manifest.SourceColumn : manifest.ReferenceLanguages.Single(pair => pair.Value == manifest.SourceLanguage).Key;
        document[original.Category]![original.File]![original.Key]![sourceColumn] = "changed reference";
        File.WriteAllText(payload, document.ToJsonString());
        LocalizationPreparation b = LocalizationRuntime.Prepare(new(2, [first], []), [first.Id], Source, TestContext.Current.CancellationToken);
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

    [Fact]
    public void TextOnlyPublicationPreservesAnExistingExclusiveResourceScope()
    {
        using InMemoryResourceSource source = new(Source);
        ResourceKey hidden = new("main/section/hidden.bin");
        source.Set(hidden, [1, 2, 3], "bin");
        ResourceService resources = new(source);
        EncodedResource archive = new(new("archive/main.pack"), "pack", 1, "synthetic", [4, 5, 6], new("main/section"));
        resources.Prepare(null, encodedResources: [archive]).Commit();
        Assert.Throws<FileNotFoundException>(() => resources.Get(hidden));
        LocalizationCatalog translated = new(new Dictionary<string, LocalizationValue> { ["$en_sample_0000"] = new("Translated", "package", "project") });
        resources.PrepareLocalization(translated).Commit();
        Assert.Equal("Translated", resources.Localize("$en_sample_0000", "native"));
        Assert.Throws<FileNotFoundException>(() => resources.Get(hidden));
        Assert.Equal(new byte[] { 4, 5, 6 }, resources.Read(new("archive/main.pack")));
        resources.PrepareLocalization(LocalizationCatalog.Empty).Commit();
        Assert.Throws<FileNotFoundException>(() => resources.Get(hidden));
    }

    private ModPackage Package(string name, params LocalizationRecord[] rows)
    {
        string path = Path.Combine(root, name);
        LocalizationProject project = new() { Manifest = new() { SourceLanguage = "en", TargetLanguage = "ru (fixture)", MappingVersion = FieldCorrespondenceCatalog.Version, CompatibleVariants = ["Classic:en"] }, Entries = rows };
        LocalizationProjectIO.WriteNew(project, path, "json", "single", TestContext.Current.CancellationToken);
        return new("Memoria/" + name, name, ModFormat.Memoria, true, true, ModEffectiveStatus.Active, path) { HasLocalization = true, LocalizationOnly = true };
    }

    private static LocalizationRecord Row(string key, string target) => new() { Category = "Menus", File = "sample", Key = key, Source = "source", Target = target };

    private static LocalizationRecord FieldRow(string key, string target, bool choice = true)
    {
        FieldCompatibilityContext call = new(new(0, 0, 0, 0, 9, 36), choice ? "ASK" : "MES", choice ? [0, 0, 0, 1, 0, 255] : [0, 0], 0, true, false, [], null);
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
