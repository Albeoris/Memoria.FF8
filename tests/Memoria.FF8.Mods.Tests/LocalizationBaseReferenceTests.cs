using System.Globalization;
using System.Text.Json;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Mods.Localization;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Mods.Tests;

public sealed class LocalizationBaseReferenceTests
{
    [Fact]
    public void OnlyBaseRowsOwnTargetsAndEveryMappedPartRemainsAvailable()
    {
        LocalizationSourceEntry baseline = Entry("en", "Fields", "bcport1a", 50, "base");
        LocalizationProject project = Create(new Dictionary<string, LocalizationSourceCatalog>
        {
            ["en"] = Catalog(baseline, Entry("en", "Fields", "bcport1a", 9999, "unused base")),
            ["jp"] = Catalog(Entry("jp", "Fields", "bcport1a", 50, "Japanese"), Entry("jp", "Fields", "bcport1a", 9998, "foreign only")),
            ["it"] = Catalog(Entry("it", "Fields", "bcport1a", 49, "first"), Entry("it", "Fields", "bcport1a", 51, "second"))
        }, ["it", "en", "jp"]);
        Assert.Equal(2, project.Entries.Count);
        Assert.All(project.Entries, row => Assert.StartsWith("$en_", row.Key));
        Assert.Equal(2, project.Manifest.SchemaVersion);
        Assert.Equal("en", project.Manifest.SourceLanguage);
        Assert.Equal(new[] { "jp", "it", "en" }, project.Manifest.ReferenceLanguages.Values);
        LocalizationRecord row = project.Entries.Single(row => row.Key == baseline.Key);
        Assert.Equal("base", row.Source);
        Assert.False(row.ExtensionData.ContainsKey("referenceContextAlternatives"));
        Assert.False(row.ExtensionData["equivalentContexts"].TryGetProperty("jp", out _));
        Assert.Equal("Japanese", row.References["Reference [jp]"]);
        Assert.Equal("first{Line}second", row.References["Reference [it]"]);
        Assert.Equal(new[] { "$it_bcport1a_0049", "$it_bcport1a_0051" }, row.ReferenceKeys["Reference [it]"]);
        Assert.Equal(2, row.ExtensionData["equivalentContexts"].GetProperty("it").GetArrayLength());
    }

    [Fact]
    public void EightAngeloEntityIdsMapAcrossAllSixLanguagesWithoutExtraRows()
    {
        string[] languages = ["en", "jp", "fr", "de", "es", "it"];
        Dictionary<string, LocalizationSourceCatalog> catalogs = languages.ToDictionary(language => language, language => Catalog(Enumerable.Range(0, 8).Select(id => Entry(language, "Abilities", "descriptions_angelo_abilities", id, language + id)).ToArray()));
        LocalizationProject project = Create(catalogs, languages);
        Assert.Equal(8, project.Entries.Count);
        foreach (LocalizationRecord row in project.Entries)
        {
            int id = int.Parse(row.Key[^4..], CultureInfo.InvariantCulture);
            foreach (string language in languages)
            {
                string column = "Reference [" + language + "]";
                Assert.Equal(language + id, row.References[column]);
                Assert.True(row.ReferencePresence[column]);
                Assert.Equal(new[] { LocalizationSourceExporter.IndexedKey(language, row.File, id) }, row.ReferenceKeys[column]);
            }
        }
    }

    [Fact]
    public void KernelReferencesRequireMatchingTypedCollectionRoleAndEntityId()
    {
        LocalizationSourceEntry Kernel(string language, string role, string text)
        {
            string table = LocalizationSourceExporter.KernelTable("MagicSpells", "Name");
            LocalizationSourceEntry row = Entry(language, "Magic", table, 7, text);
            return row with { Metadata = new Dictionary<string, string?>(row.Metadata) { ["Kernel Collection"] = "MagicSpells", ["Role"] = role } };
        }
        LocalizationProject valid = Create(new Dictionary<string, LocalizationSourceCatalog> { ["en"] = Catalog(Kernel("en", "Name", "native")), ["jp"] = Catalog(Kernel("jp", "Name", "unrelated script")) }, []);
        Assert.Equal("unrelated script", valid.Entries.Single().References["Reference [jp]"]);
        LocalizationProject invalid = Create(new Dictionary<string, LocalizationSourceCatalog> { ["en"] = Catalog(Kernel("en", "Name", "same")), ["jp"] = Catalog(Kernel("jp", "Description", "same")) }, []);
        Assert.Null(invalid.Entries.Single().References["Reference [jp]"]);
    }

    [Fact]
    public void NativeCardRecordReferencesUseTheFixedCardIdContract()
    {
        LocalizationSourceEntry Card(string language)
        {
            LocalizationSourceEntry entry = Entry(language, "Cards", "mngrp_slot192_records", 109, language);
            return entry with { Metadata = new Dictionary<string, string?>(entry.Metadata) { ["Menu Section"] = "192" } };
        }
        LocalizationProject project = Create(new Dictionary<string, LocalizationSourceCatalog> { ["en"] = Catalog(Card("en")), ["jp"] = Catalog(Card("jp")) }, []);
        Assert.Equal("jp", project.Entries.Single().References["Reference [jp]"]);
        Assert.Equal(new[] { "$jp_mngrp_slot192_records_0109" }, project.Entries.Single().ReferenceKeys["Reference [jp]"]);
    }

    [Fact]
    public void UnprovedWorldAndMenuOrdinalsDoNotBecomeFalseReferences()
    {
        LocalizationSourceEntry[] Rows(string language) => [Entry(language, "World", "messages_world", 3, "same"), Entry(language, "Menus", "namedic_menu", 3, "same"), Entry(language, "Menus", "mngrp_slot000_table", 3, "same")];
        LocalizationProject project = Create(new Dictionary<string, LocalizationSourceCatalog> { ["en"] = Catalog(Rows("en")), ["jp"] = Catalog(Rows("jp")) }, []);
        Assert.Equal(3, project.Entries.Count);
        Assert.All(project.Entries, row =>
        {
            Assert.Null(row.References["Reference [jp]"]);
            Assert.Empty(row.ReferenceKeys["Reference [jp]"]);
            Assert.False(row.ReferencePresence["Reference [jp]"]);
        });
        Assert.Equal(3, project.Manifest.ExtensionData["unmappedReferenceFamilies"].EnumerateObject().Count());
    }

    [Fact]
    public void ContextVariantsPreferProvedPresentationAndOtherwiseKeepApprovedBaseReference()
    {
        LocalizationSourceEntry Variant(string language, string prefix, bool dynamic = false)
        {
            LocalizationSourceEntry entry = Entry(language, "Fields", "bccent12", 0, prefix + language);
            string key = entry.Key + "_ctx_test";
            return entry with { Key = key, Metadata = new Dictionary<string, string?>(entry.Metadata) { ["Base Key"] = entry.Key, ["Context Bindings"] = JsonSerializer.Serialize(new[] { new LocalizationSourceContextBinding(1, "different-per-language", entry.Key, key, prefix, [4], dynamic) }) } };
        }
        LocalizationSourceEntry baseline = Variant("en", "{X10Y20}");
        LocalizationSourceEntry japanese = Variant("jp", "{X10Y20}");
        LocalizationProject project = Create(new Dictionary<string, LocalizationSourceCatalog> { ["en"] = Catalog(baseline), ["jp"] = Catalog(japanese) }, []);
        Assert.Equal(japanese.Text, project.Entries.Single().References["Reference [jp]"]);
        Assert.Equal(new[] { japanese.Key }, project.Entries.Single().ReferenceKeys["Reference [jp]"]);
        Assert.False(project.Entries.Single().ExtensionData.ContainsKey("referenceContextAlternatives"));
        LocalizationSourceEntry plainJapanese = Entry("jp", "Fields", "bccent12", 0, "plain Japanese body");
        LocalizationProject incompatible = Create(new Dictionary<string, LocalizationSourceCatalog> { ["en"] = Catalog(baseline), ["jp"] = Catalog(plainJapanese, Variant("jp", "{X30Y20}")) }, []);
        Assert.Equal(plainJapanese.Text, incompatible.Entries.Single().References["Reference [jp]"]);
        Assert.Equal(new[] { plainJapanese.Key }, incompatible.Entries.Single().ReferenceKeys["Reference [jp]"]);
        Assert.Equal(2, incompatible.Entries.Single().ExtensionData["referenceContextAlternatives"].GetProperty("jp").GetArrayLength());
        LocalizationProject dynamic = Create(new Dictionary<string, LocalizationSourceCatalog> { ["en"] = Catalog(baseline), ["jp"] = Catalog(plainJapanese, Variant("jp", "{X10Y20}", true)) }, []);
        Assert.Equal(plainJapanese.Text, dynamic.Entries.Single().References["Reference [jp]"]);
        Assert.Equal(2, dynamic.Entries.Single().ExtensionData["referenceContextAlternatives"].GetProperty("jp").GetArrayLength());
    }

    [Fact]
    public void NonEnglishSystemBaseHasSemanticKeysAndOnlyAuthoredEnglishReferenceText()
    {
        LocalizationProject project = LocalizationProjectBuilder.CreateProject(new("en", "jp", "ru", [], "Test"), new Dictionary<string, LocalizationSourceCatalog> { ["en"] = Catalog(), ["jp"] = Catalog() }, TestContext.Current.CancellationToken, includeSystem: true);
        Assert.NotEmpty(project.Entries);
        Assert.All(project.Entries, row =>
        {
            Assert.StartsWith("$jp_", row.Key);
            Assert.False(row.SourcePresent);
            Assert.Null(row.Source);
            Assert.Null(row.References["Reference [jp]"]);
            Assert.NotNull(row.References["Reference [en]"]);
            Assert.StartsWith("$en_", Assert.Single(row.ReferenceKeys["Reference [en]"]));
        });
    }

    private static LocalizationProject Create(Dictionary<string, LocalizationSourceCatalog> catalogs, IReadOnlyList<string> references) => LocalizationProjectBuilder.CreateProject(new("jp", "en", "ru", references, "Test"), catalogs, TestContext.Current.CancellationToken);

    private static LocalizationSourceCatalog Catalog(params LocalizationSourceEntry[] entries) => new(entries, [], []);

    private static LocalizationSourceEntry Entry(string language, string category, string file, int id, string text) => new(category, file, LocalizationSourceExporter.IndexedKey(language, file, id), text, language, new Dictionary<string, string?> { ["Source Ordinal"] = id.ToString(CultureInfo.InvariantCulture) });
}
