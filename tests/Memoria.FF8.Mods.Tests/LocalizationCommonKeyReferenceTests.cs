using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Mods.Localization;
using Memoria.FF8.Resources.Localization;
using System.Text.Json;

namespace Memoria.FF8.Mods.Tests;

public sealed class LocalizationCommonKeyReferenceTests
{
    [Fact]
    public void InstalledReferencesCoverEveryExistingCommonKeyAcrossAllNonfieldCategories()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_REMASTERED_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory), "Configure installed Remastered resources for six-language reference coverage.");
        string[] languages = ["en", "jp", "fr", "de", "es", "it"];
        var catalogs = languages.ToDictionary(language => language, language => LocalizationSourceExporter.ReadNonFields(directory!, language, TestContext.Current.CancellationToken));
        var project = LocalizationProjectBuilder.CreateProject(new("jp", "en", "ru", languages, "References"), catalogs, TestContext.Current.CancellationToken);
        Assert.Equal(catalogs["en"].Entries.Count, project.Entries.Count);
        var indexes = catalogs.ToDictionary(pair => pair.Key, pair => pair.Value.Entries.ToDictionary(row => row.Key[(row.Key.IndexOf('_') + 1)..], StringComparer.Ordinal));
        Dictionary<string, Dictionary<string, int>> coverage = [];
        foreach (var row in project.Entries)
        {
            string common = row.Key[(row.Key.IndexOf('_') + 1)..];
            foreach (string language in languages)
            {
                string label = "Reference [" + language + "]";
                if (indexes[language].TryGetValue(common, out var counterpart))
                {
                    if (!counterpart.Text.Contains("{Term ", StringComparison.Ordinal))
                    {
                        Assert.Equal(counterpart.Text, row.References[label]);
                    }
                    Assert.DoesNotContain("{Term ", row.References[label] ?? "", StringComparison.Ordinal);
                    Assert.Equal(new[] { counterpart.Key }, row.ReferenceKeys[label]);
                    Assert.True(row.ReferencePresence[label]);
                    if (!coverage.TryGetValue(row.Category, out var counts))
                    {
                        counts = [];
                        coverage.Add(row.Category, counts);
                    }
                    counts[language] = counts.GetValueOrDefault(language) + 1;
                }
                else
                {
                    Assert.Null(row.References[label]);
                    Assert.Empty(row.ReferenceKeys[label]);
                }
            }
        }
        foreach (int ordinal in new[] { 0, 4, 5, 7 })
        {
            string key = LocalizationSourceExporter.IndexedKey("en", "descriptions_angelo_abilities", ordinal);
            Assert.Equal(ordinal is 0 or 5 ? "Angriff auf einen Gegner" : "Angriff auf alle Gegner", project.Entries.Single(row => row.Key == key).References["Reference [de]"]);
        }
        string? report = Environment.GetEnvironmentVariable("MEMORIA_FF8_REFERENCE_REPORT");
        if (!string.IsNullOrWhiteSpace(report))
        {
            File.WriteAllText(report, JsonSerializer.Serialize(coverage, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    [Theory]
    [InlineData("Battle")]
    [InlineData("Draw points")]
    [InlineData("Information and SeeD exam")]
    [InlineData("Menus")]
    [InlineData("Publications")]
    [InlineData("System")]
    [InlineData("Tutorials")]
    [InlineData("World")]
    [InlineData("Future category without registration")]
    public void EveryCategoryMatchesTheWholeCommonKeyWithoutOrdinalOrLanguageSpecificRules(string category)
    {
        string[] languages = ["en", "jp", "fr", "de", "es", "it"];
        LocalizationSourceEntry Row(string language, int ordinal) => new(category, "common_resource_name", "$" + language + "_common_resource_name_" + ordinal.ToString("D4"), language + ":" + ordinal, language, new Dictionary<string, string?>());
        Dictionary<string, LocalizationSourceCatalog> catalogs = languages.ToDictionary(language => language, language => new LocalizationSourceCatalog([Row(language, 1), Row(language, 0)], [], []));
        var project = LocalizationProjectBuilder.CreateProject(new("jp", "en", "ru", languages, "References"), catalogs, TestContext.Current.CancellationToken);
        Assert.Equal(2, project.Entries.Count);
        foreach (var row in project.Entries)
        {
            int ordinal = int.Parse(row.Key[^4..], System.Globalization.CultureInfo.InvariantCulture);
            foreach (string language in languages)
            {
                string column = "Reference [" + language + "]";
                Assert.Equal(language + ":" + ordinal, row.References[column]);
                Assert.Equal(new[] { "$" + language + "_common_resource_name_" + ordinal.ToString("D4") }, row.ReferenceKeys[column]);
            }
        }
    }

    [Fact]
    public void IdenticalTextWithDifferentCommonKeysDoesNotCreateACounterpart()
    {
        LocalizationSourceEntry Row(string language, string file) => new("Battle", file, "$" + language + "_" + file + "_0000", "same", language, new Dictionary<string, string?>());
        var project = LocalizationProjectBuilder.CreateProject(new("jp", "en", "ru", ["jp", "en"], "References"), new Dictionary<string, LocalizationSourceCatalog> { ["en"] = new([Row("en", "c0m003_dialogue_battle")], [], []), ["jp"] = new([Row("jp", "c0m005_dialogue_battle")], [], []) }, TestContext.Current.CancellationToken);
        var row = Assert.Single(project.Entries);
        Assert.Null(row.References["Reference [jp]"]);
        Assert.Empty(row.ReferenceKeys["Reference [jp]"]);
    }

    [Fact]
    public void MissingForeignAuthoredTextIsNotReplacedWithEnglishUnderAJapaneseLabel()
    {
        LocalizationSourceEntry english = new("System", "interface_system", "$en_interface_system_main.junction", "Junction", "en", new Dictionary<string, string?>());
        var project = LocalizationProjectBuilder.CreateProject(new("jp", "en", "ru", ["jp", "en"], "References"), new Dictionary<string, LocalizationSourceCatalog> { ["en"] = new([english], [], []), ["jp"] = new([], [], []) }, TestContext.Current.CancellationToken);
        var row = Assert.Single(project.Entries);
        Assert.Equal("Junction", row.References["Reference [en]"]);
        Assert.Null(row.References["Reference [jp]"]);
        Assert.False(row.ReferencePresence["Reference [jp]"]);
    }
}
