using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Mods.Localization;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Mods.Tests;

public sealed class LocalizationDynamicFieldTests : IDisposable
{
    private const string Hash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    private const string Key = "$en_bgroom_6_0000";
    private readonly string root = Path.Combine(Path.GetTempPath(), "Memoria.Localization.Tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(-1)]
    [InlineData(255)]
    [InlineData(1)]
    public void DynamicChoiceUsesEvaluatedRangeAndPreservesNativeInitialAndCancel(int cancel)
    {
        LocalizationCatalog catalog = Prepare(Row([new(0, 2), new(1, 3)]));
        LocalizationFieldText translated = catalog.ResolveField(Key, 9, Hash, 0, 1, 1, cancel)!;
        Assert.NotNull(translated);
        Assert.Equal(2, translated.FirstChoice);
        Assert.Equal(3, translated.LastChoice);
        Assert.Equal(3, translated.InitialChoice);
        Assert.Equal(cancel == 1 ? 3 : cancel, translated.CancelChoice);
        Assert.Null(catalog.ResolveField(Key, 9, Hash, 0, 2, 1, cancel));
        Assert.Null(catalog.ResolveField(Key, 9, "wrong", 0, 1, 1, cancel));
    }

    [Fact]
    public void EmptyMapUsesNativeLineIdentityAndKnownOperandsAreNeverWildcards()
    {
        LocalizationCatalog catalog = Prepare(Row([]));
        LocalizationFieldText translated = catalog.ResolveField(Key, 9, Hash, 0, 1, 1, 255)!;
        Assert.NotNull(translated);
        Assert.Equal(0, translated.FirstChoice);
        Assert.Equal(1, translated.LastChoice);
        Assert.Equal(1, translated.InitialChoice);
        Assert.Equal(255, translated.CancelChoice);
        Assert.Null(catalog.ResolveField(Key, 9, Hash, 0, 2, 1, 255));
        Assert.Null(catalog.ResolveField(Key, 9, Hash, 0, 1, 2, 255));
    }

    [Fact]
    public void EditableTopLevelMapOverridesLegacyContextMapAndLegacyRemainsReadable()
    {
        LocalizationRecord row = Row([new(0, 2), new(1, 3)], legacy: true);
        Assert.Equal(2, Prepare(row).ResolveField(Key, 9, Hash, 0, 1, 1, 255)!.FirstChoice);
        row = row with { ExtensionData = new Dictionary<string, JsonElement>(row.ExtensionData) { ["Choice Line Map"] = JsonSerializer.SerializeToElement(Array.Empty<LocalizationChoiceLineMapEntry>()) } };
        Assert.Equal(0, Prepare(row).ResolveField(Key, 9, Hash, 0, 1, 1, 255)!.FirstChoice);
    }

    [Fact]
    public void NonContiguousOrIncompleteMapCannotChangeNativeOptionIdentity()
    {
        Assert.Null(Prepare(Row([new(0, 0), new(1, 2)])).ResolveField(Key, 9, Hash, 0, 1, 1, 255));
        Assert.Null(Prepare(Row([new(0, 2)])).ResolveField(Key, 9, Hash, 0, 1, 1, 255));
    }

    [Fact]
    public void DynamicSourceCopyKeepsNativeOptionLinesAndMissingMapMembersAreRejected()
    {
        LocalizationRecord row = Row([]);
        LocalizationFieldText translated = Prepare(row with { Target = row.Source }).ResolveField(Key, 9, Hash, 0, 1, 1, -1)!;
        Assert.Equal(row.Source, translated.Body);
        Assert.Equal(0, translated.FirstChoice);
        Assert.Equal(-1, translated.CancelChoice);
        row = row with { ExtensionData = new Dictionary<string, JsonElement>(row.ExtensionData) { ["Choice Line Map"] = JsonSerializer.SerializeToElement(new[] { new { SourceLine = 0 } }) } };
        Assert.Throws<InvalidDataException>(() => Prepare(row));
    }

    [Fact]
    public void InvalidMapAndLiteralDynamicChoicePrefixAreRejectedBeforePublication()
    {
        Assert.Throws<InvalidDataException>(() => Prepare(Row([new(0, 9)])));
        Assert.Throws<InvalidDataException>(() => Prepare(Row([new(0, 2), new(0, 3)])));
        Assert.Throws<InvalidDataException>(() => Prepare(Row([new(0, 2), new(1, 2)])));
        Assert.Throws<InvalidDataException>(() => Prepare(Row([]) with { Target = "{F0L1B1C255}Yes{Line}No" }));
    }

    private LocalizationCatalog Prepare(LocalizationRecord row)
    {
        string path = Path.Combine(root, Guid.NewGuid().ToString("N"));
        LocalizationProject project = new() { Manifest = new() { SourceLanguage = "en", TargetLanguage = "translated", MappingVersion = FieldCorrespondenceCatalog.Version, CompatibleVariants = ["Classic:en"] }, Entries = [row] };
        LocalizationProjectIO.WriteNew(project, path, "json", "single", TestContext.Current.CancellationToken);
        ModPackage package = new("Memoria/fixture", "fixture", ModFormat.Memoria, true, true, ModEffectiveStatus.Active, path) { HasLocalization = true, LocalizationOnly = true };
        return LocalizationRuntime.Prepare(new(1, [package], []), [package.Id], new("fixture", GameDataEdition.Classic, [], "en"), TestContext.Current.CancellationToken).Catalog;
    }

    private static LocalizationRecord Row(LocalizationChoiceLineMapEntry[] map, bool legacy = false)
    {
        FieldCompatibilityContext call = new(new(0, 0, 0, 0, 9, 36), "ASK", [0, 0, null, 1, null, null], 0, true, true, [], null);
        Dictionary<string, string> context = new() { ["JSM SHA256"] = Hash, ["Call Contexts"] = JsonSerializer.Serialize(new[] { call }) };
        Dictionary<string, JsonElement> metadata = [];
        if (legacy)
        {
            context["Choice Line Map"] = JsonSerializer.Serialize(map);
        }
        else
        {
            metadata["Choice Line Map"] = JsonSerializer.SerializeToElement(map);
        }
        metadata["sourceContext"] = JsonSerializer.SerializeToElement(context);
        return new() { Category = "Fields", File = "bgroom_6", Key = Key, Source = "Yes{Line}No", Target = "Heading{Line}Help{Line}Yes{Line}No", ExtensionData = metadata };
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}
