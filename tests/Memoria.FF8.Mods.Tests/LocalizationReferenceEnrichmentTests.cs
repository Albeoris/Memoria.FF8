using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Mods.Localization;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Mods.Tests;

public sealed class LocalizationReferenceEnrichmentTests
{
    [Fact]
    public void JapaneseCommonKeysPreserveInstalledTargetAndOtherReferences()
    {
        LocalizationRecord matched = new()
        {
            Category = "Magic",
            File = "kernel_magicspells_name",
            Key = "$en_kernel_magicspells_name_0000",
            SourcePresent = false,
            Target = "Русский",
            TargetPresent = true,
            References = new Dictionary<string, string?>() { ["Reference [en]"] = "English" },
            ReferenceKeys = new Dictionary<string, IReadOnlyList<string>>() { ["Reference [en]"] = ["$en_kernel_magicspells_name_0000"] },
            ExtensionData = new Dictionary<string, JsonElement>() { ["Edition Targets"] = JsonSerializer.SerializeToElement(new Dictionary<string, string> { ["Classic:en"] = "Вариант" }) }
        };
        LocalizationRecord missing = matched with { Key = "$en_kernel_magicspells_name_0001", References = new Dictionary<string, string?>(), ReferenceKeys = new Dictionary<string, IReadOnlyList<string>>() };
        LocalizationSourceCatalog japanese = Catalog(Native(matched, "$jp_kernel_magicspells_name_0000", "jp", "あい"));
        LocalizationSourceCatalog english = Catalog(Native(matched, matched.Key, "en", "English"), Native(missing, missing.Key, "en", "Other"));

        LocalizationReferenceEnrichment result = LocalizationProjectBuilder.EnrichReference(Project(matched, missing), "ja", GameDataEdition.Remastered, japanese, english, TestContext.Current.CancellationToken);
        Assert.Equal("en", result.Project.Manifest.FieldScriptBaseLanguage);
        Assert.Equal("jp", result.Language);
        Assert.Equal(1, result.Matched);
        Assert.Equal("native-key-missing", Assert.Single(result.Skips).Reason);
        LocalizationRecord first = result.Project.Entries[0];
        Assert.Equal("あい", first.References["Reference [jp]"]);
        Assert.Equal(["$jp_kernel_magicspells_name_0000"], first.ReferenceKeys["Reference [jp]"]);
        Assert.Equal("English", first.References["Reference [en]"]);
        Assert.Equal("Русский", first.Target);
        Assert.True(first.TargetPresent);
        Assert.Same(matched.ExtensionData, first.ExtensionData);
        Assert.True(result.Project.Manifest.PlayerExport);
        Assert.False(result.Project.Entries[1].References.ContainsKey("Reference [jp]"));

        var repeated = LocalizationProjectBuilder.EnrichReference(result.Project, "jp", GameDataEdition.Remastered, japanese, english, TestContext.Current.CancellationToken);
        Assert.Equal(0, repeated.Changed);
        Assert.Same(first, repeated.Project.Entries[0]);
    }

    [Fact]
    public void ClassicFieldRequiresVerifiedBankAndUsesNativeReading()
    {
        string key = FieldCorrespondenceCatalog.GetKeys("en").First();
        int ordinalSeparator = key.LastIndexOf('_');
        LocalizationRecord row = new() { Category = "Fields", File = key[4..ordinalSeparator], Key = key, SourcePresent = false, Target = "Target", TargetPresent = true };
        string bank = new('a', 64);
        var bound = row with { ExtensionData = new Dictionary<string, JsonElement>() { ["sourceContext"] = JsonSerializer.SerializeToElement(new Dictionary<string, string> { ["Source MSD SHA256"] = bank }) } };
        LocalizationSourceCatalog source = Catalog(Native(row, key, "en", "Classic", new() { ["Source MSD SHA256"] = bank }));

        var result = LocalizationProjectBuilder.EnrichReference(Project(bound), "en", GameDataEdition.Classic, source, cancellation: TestContext.Current.CancellationToken);
        Assert.Equal("Classic", Assert.Single(result.Project.Entries).References["Reference [en]"]);
        Assert.Equal([key], Assert.Single(result.Project.Entries).ReferenceKeys["Reference [en]"]);
        Assert.Equal("Classic", result.Project.Manifest.ExtensionData[LocalizationProjectIO.ReferenceEditionsMember].GetProperty("Reference [en]").GetString());

        var unverified = LocalizationProjectBuilder.EnrichReference(Project(row), "en", GameDataEdition.Classic, source, cancellation: TestContext.Current.CancellationToken);
        Assert.Equal("english-edition-key-unverified", Assert.Single(unverified.Skips).Reason);
        Assert.Empty(Assert.Single(unverified.Project.Entries).References);
    }

    [Fact]
    public void CrossLanguageFieldNeverFallsBackToEnglishOrIgnoresMissingSegments()
    {
        string key = FieldCorrespondenceCatalog.GetKeys("en").First(candidate => FieldCorrespondenceCatalog.GetEquivalents(candidate, "jp").Count > 1);
        int ordinalSeparator = key.LastIndexOf('_');
        LocalizationRecord row = new() { Category = "Fields", File = key[4..ordinalSeparator], Key = key, SourcePresent = false, Target = "Target", TargetPresent = true };
        IReadOnlyList<string> japaneseKeys = FieldCorrespondenceCatalog.GetEquivalents(key, "jp");
        LocalizationSourceCatalog english = Catalog(Native(row, key, "en", "English"));
        LocalizationSourceCatalog incomplete = Catalog(Native(row, japaneseKeys[0], "jp", "あい"));
        var missing = LocalizationProjectBuilder.EnrichReference(Project(row), "jp", GameDataEdition.Remastered, incomplete, english, TestContext.Current.CancellationToken);
        Assert.Equal("native-key-missing", Assert.Single(missing.Skips).Reason);
        Assert.Empty(Assert.Single(missing.Project.Entries).References);
        Assert.Equal(japaneseKeys, Assert.Single(missing.Project.Entries).ReferenceKeys["Reference [jp]"]);

        LocalizationSourceCatalog complete = Catalog(japaneseKeys.Select((candidate, index) => Native(row, candidate, "jp", "あい" + index)).ToArray());
        var matched = LocalizationProjectBuilder.EnrichReference(Project(row), "jp", GameDataEdition.Remastered, complete, english, TestContext.Current.CancellationToken);
        Assert.Equal(string.Join("{Line}", complete.Entries.Select(entry => entry.Text)), Assert.Single(matched.Project.Entries).References["Reference [jp]"]);
    }

    [Fact]
    public void JapanesePresentationVariantsUseOnlyProvedBaseReadingsWhenTheirLayoutDiffers()
    {
        string key = FieldCorrespondenceCatalog.GetKeys("en").First(candidate => FieldCorrespondenceCatalog.TryGetEquivalentKey(candidate, "jp", out _));
        string japaneseKey = Assert.Single(FieldCorrespondenceCatalog.GetEquivalents(key, "jp"));
        string unmatchedSuffix = "_ctx_00aabbcc11223344";
        string matchingSuffix = "_ctx_00112233aabbccdd";
        int ordinalSeparator = key.LastIndexOf('_');
        LocalizationRecord row = new() { Category = "Fields", File = key[4..ordinalSeparator], Key = key + unmatchedSuffix, SourcePresent = false, Target = "Target", TargetPresent = true };
        LocalizationRecord secondLayout = row with { Key = key + matchingSuffix };
        LocalizationSourceCatalog english = Catalog(Native(row, row.Key, "en", "{X10Y40}English"), Native(secondLayout, secondLayout.Key, "en", "{X40Y90}English"));
        LocalizationSourceCatalog japanese = Catalog(
            Native(row, japaneseKey, "jp", "{X20Y40}あい{End}"),
            Native(row, japaneseKey + matchingSuffix, "jp", "{X40Y90}あい{End}", new() { ["Base Key"] = japaneseKey }));

        var result = LocalizationProjectBuilder.EnrichReference(Project(row, secondLayout), "jp", GameDataEdition.Remastered, japanese, english, TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Matched);
        Assert.Equal(1, result.TextOnly);
        Assert.Equal(1, Assert.Single(result.Coverage).TextOnly);
        Assert.Empty(result.Skips);
        Assert.Equal("あい{End}", result.Project.Entries[0].References["Reference [jp]"]);
        Assert.Equal(new[] { japaneseKey }, result.Project.Entries[0].ReferenceKeys["Reference [jp]"]);
        Assert.Equal("{X40Y90}あい{End}", result.Project.Entries[1].References["Reference [jp]"]);
        Assert.Equal(new[] { japaneseKey + matchingSuffix }, result.Project.Entries[1].ReferenceKeys["Reference [jp]"]);

        LocalizationSourceCatalog unrelated = Catalog(Native(row, japaneseKey + matchingSuffix, "jp", "{X40Y90}ちがう{End}", new() { ["Base Key"] = japaneseKey }));
        var missing = LocalizationProjectBuilder.EnrichReference(Project(row), "jp", GameDataEdition.Remastered, unrelated, english, TestContext.Current.CancellationToken);
        Assert.Equal("presentation-context-unverified", Assert.Single(missing.Skips).Reason);
        Assert.Empty(Assert.Single(missing.Project.Entries).References);
    }

    [Fact]
    public void ExpandedClassicTermsProveTheReadingOnlyWhenRawSourceAndCurrentReadingMatch()
    {
        const string label = "Reference [en]";
        string key = FieldCorrespondenceCatalog.GetKeys("en").First();
        int ordinalSeparator = key.LastIndexOf('_');
        const string nativeText = "{TermEN Galbadia}.";
        string bank = new('a', 64);
        LocalizationRecord row = new()
        {
            Category = "Fields",
            File = key[4..ordinalSeparator],
            Key = key,
            SourcePresent = false,
            Target = "Target",
            ExtensionData = new Dictionary<string, JsonElement>
            {
                ["Source Text SHA256"] = JsonSerializer.SerializeToElement(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(nativeText)))),
                ["sourceContext"] = JsonSerializer.SerializeToElement(new Dictionary<string, string> { ["Source MSD SHA256"] = bank })
            }
        };
        LocalizationSourceCatalog classic = Catalog(
            Native(row, key, "en", nativeText, new() { ["Source MSD SHA256"] = bank }),
            new("Menus", "namedic_menu", "$en_namedic_menu_0000", "Galbadia", "en", new Dictionary<string, string?>()));

        var result = LocalizationProjectBuilder.EnrichReference(Project(row), "en", GameDataEdition.Classic, classic, cancellation: TestContext.Current.CancellationToken);
        LocalizationRecord written = Assert.Single(result.Project.Entries);
        string reading = Assert.IsType<string>(written.References[label]);
        Assert.Equal("Galbadia.", reading);
        Assert.True(LocalizationReferenceProvenance.MatchesSource(result.Project.Manifest, written, label, reading));
        Assert.False(LocalizationReferenceProvenance.MatchesSource(result.Project.Manifest, written, label, "Edited reading."));
        Assert.False(LocalizationReferenceProvenance.MatchesSource(result.Project.Manifest, written, label, "Galbadia.{End}"));

        LocalizationRecord changedSource = written with
        {
            ExtensionData = new Dictionary<string, JsonElement>(written.ExtensionData, StringComparer.Ordinal)
            {
                ["Source Text SHA256"] = JsonSerializer.SerializeToElement(new string('b', 64))
            }
        };
        Assert.False(LocalizationReferenceProvenance.MatchesSource(result.Project.Manifest, changedSource, label, reading));
        var refreshed = LocalizationProjectBuilder.EnrichReference(result.Project with { Entries = [changedSource] }, "en", GameDataEdition.Classic, classic, cancellation: TestContext.Current.CancellationToken);
        Assert.Equal(reading, Assert.Single(refreshed.Project.Entries).References[label]);
        Assert.False(refreshed.Project.Manifest.ExtensionData[LocalizationReferenceProvenance.ManifestMember].TryGetProperty(label, out _));
    }

    private static LocalizationProject Project(params LocalizationRecord[] rows) => new()
    {
        Manifest = new()
        {
            SchemaVersion = 2,
            SourceEdition = "Remastered",
            CompatibleVariants = ["Remastered:en", "Classic:en"],
            FieldScriptBaseLanguage = "en",
            SourceLanguage = "en",
            TargetLanguage = "ru",
            MappingVersion = FieldCorrespondenceCatalog.Version,
            PlayerExport = true,
            ReferenceLanguages = new Dictionary<string, string> { ["Reference [en]"] = "en" }
        },
        Entries = rows
    };

    private static LocalizationSourceCatalog Catalog(params LocalizationSourceEntry[] entries) => new(entries, [], []);

    private static LocalizationSourceEntry Native(LocalizationRecord row, string key, string language, string text, Dictionary<string, string?>? metadata = null) =>
        new(row.Category, row.File, key, text, language, metadata ?? []);
}
