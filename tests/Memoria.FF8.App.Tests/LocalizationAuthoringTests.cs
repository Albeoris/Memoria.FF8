using Memoria.FF8.GameUi;
using Memoria.FF8.Mods.Localization;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.App.Tests;

public sealed class LocalizationAuthoringTests
{
    [Fact]
    public void FormatChangesPreserveCustomTargetPackageAndReferences()
    {
        MenuController menus = CreateMenus();
        menus.Open("mods-create");
        menus.Execute(new("localization-new"));
        menus.Execute(new("localization-text", "target"));
        foreach (char character in "ru (Literary edition)")
        {
            Assert.True(menus.EnterLocalizationCharacter(character));
        }
        menus.Back();
        menus.Execute(new("localization-language", "references"));
        menus.Execute(new("localization-select-language", "fr"));
        menus.Back();
        menus.Execute(new("localization-format"));
        Assert.Contains(menus.Entries, entry => entry.Id == "target" && entry.Label == "Target: ru (Literary edition)");
        Assert.Contains(menus.Entries, entry => entry.Id == "format" && entry.Label == "Format: .xlsx");
        Assert.Contains(menus.Entries, entry => entry.Id == "references" && entry.Label.Contains("fr", StringComparison.Ordinal));
        menus.Execute(new("localization-format"));
        Assert.Contains(menus.Entries, entry => entry.Id == "target" && entry.Label == "Target: ru (Literary edition)");
    }

    [Fact]
    public void UnavailableProbeCannotBeSelectedAndScriptBaseDefaultsToReference()
    {
        MenuController menus = CreateMenus();
        menus.Open("mods-create");
        menus.Execute(new("localization-new"));
        menus.Execute(new("localization-language", "base"));
        Assert.Contains(menus.Entries, entry => entry.Id == "jp" && !entry.Enabled);
        menus.Execute(new("localization-select-language", "fr"));
        Assert.Contains(menus.Entries, entry => entry.Id == "base" && entry.Label == "Field script base: fr");
        Assert.Contains(menus.Entries, entry => entry.Id == "references" && entry.Label.Contains("fr", StringComparison.Ordinal));
    }

    [Fact]
    public void StackSelectionAndReorderingAreExplicitAndCancelPreservesSavedOrder()
    {
        MenuController menus = CreateMenus();
        IReadOnlyList<string> saved = ["a"];
        menus.ConnectLocalizationStack(() => new([new("a", "Translation A", "custom-a"), new("b", "Translation B", "custom-b")], saved, true), order => saved = order);
        menus.Open("mods-create");
        menus.Execute(new("localization-stack-open"));
        Assert.Contains(menus.CurrentScreen.Details(menus), line => line.Contains("LOW priority at TOP", StringComparison.Ordinal));
        menus.Execute(new("localization-stack-toggle", "b"));
        menus.Execute(new("localization-stack-move", Value: "-1"));
        Assert.Equal(new[] { "package-b", "package-a" }, menus.Entries.Take(2).Select(entry => entry.Id));
        menus.PromptLocalizationSelection();
        Assert.Equal(new[] { "package-b", "package-a" }, menus.Entries.Take(2).Select(entry => entry.Id));
        Assert.Equal(new[] { "a" }, saved);
        menus.Back();
        menus.Execute(new("localization-stack-open"));
        menus.Execute(new("localization-stack-toggle", "b"));
        menus.Execute(new("localization-stack-move", Value: "-1"));
        menus.Execute(new("localization-stack-apply"));
        Assert.Equal(new[] { "b", "a" }, saved);
    }

    [Fact]
    public void MixedStackPersistsExplicitPrimaryTargetWithOrderedPackages()
    {
        MenuController menus = CreateMenus();
        string? savedPrimary = null;
        IReadOnlyList<string>? savedOrder = null;
        menus.ConnectLocalizationStack(() => new([new("a", "Translation A", "custom-a"), new("b", "Translation B", "custom-b")], ["a", "b"]), (order, primary) =>
        {
            savedOrder = order;
            savedPrimary = primary;
        });
        menus.Open("mods-create");
        menus.PromptLocalizationSelection();
        Assert.Contains(menus.Entries, entry => entry.Id == "primary" && entry.Label == "Primary target: custom-a");
        menus.Execute(new("localization-stack-primary"));
        menus.Execute(new("localization-stack-apply"));
        Assert.Equal("custom-b", savedPrimary);
        Assert.Equal(new[] { "a", "b" }, savedOrder);
    }

    [Fact]
    public void UnionRetainsLanguageOnlyEntriesAndMissingSourcePresence()
    {
        LocalizationAuthoringRequest request = new("en", "en", "Alternate", ["fr"], "Test translation");
        LocalizationSourceEntry english = new("Magic", "names_magic", "$names_magic_0000", "Fire", "en", new Dictionary<string, string?>());
        LocalizationSourceEntry french = new("Magic", "names_magic", "$names_magic_0001", "Glacier", "fr", new Dictionary<string, string?>());
        Dictionary<string, LocalizationSourceCatalog> catalogs = new()
        {
            ["en"] = new([english], [], []),
            ["fr"] = new([french], [], [])
        };
        LocalizationProject project = LocalizationProjectBuilder.CreateProject(request, catalogs, TestContext.Current.CancellationToken);
        Assert.Equal(2, project.Entries.Count);
        LocalizationRecord missing = Assert.Single(project.Entries, row => row.Key == french.Key);
        Assert.False(missing.SourcePresent);
        Assert.Null(missing.Source);
        Assert.Equal("Glacier", missing.References["Reference [fr]"]);
        Assert.True(missing.ReferencePresence["Reference [fr]"]);
        Assert.Equal("Alternate", project.Manifest.TargetLanguage);
    }

    [Fact]
    public void AmbiguousCounterpartsRemainSeparateAndDoNotInventMainSource()
    {
        LocalizationAuthoringRequest request = new("it", "en", "Alternate", ["en"], "Test translation");
        LocalizationSourceEntry Row(string key, string text, string language) => new("Fields", "bcport1a", key, text, language, new Dictionary<string, string?>());
        Dictionary<string, LocalizationSourceCatalog> catalogs = new()
        {
            ["en"] = new([Row("$en_bcport1a_0050", "English dialogue", "en")], [], []),
            ["it"] = new([Row("$it_bcport1a_0049", "First dialogue", "it"), Row("$it_bcport1a_0051", "Second dialogue", "it")], [], [])
        };
        LocalizationProject project = LocalizationProjectBuilder.CreateProject(request, catalogs, TestContext.Current.CancellationToken);
        Assert.Equal(3, project.Entries.Count);
        LocalizationRecord english = Assert.Single(project.Entries, row => row.Key == "$en_bcport1a_0050");
        Assert.False(english.SourcePresent);
        Assert.Null(english.Source);
        Assert.Equal(2, english.ExtensionData["equivalentKeys"].GetProperty("it").GetArrayLength());
        Assert.Equal(2, english.ExtensionData["equivalentContexts"].GetProperty("it").GetArrayLength());
        Assert.Equal("First dialogue", Assert.Single(project.Entries, row => row.Key == "$it_bcport1a_0049").Source);
        Assert.Equal("Second dialogue", Assert.Single(project.Entries, row => row.Key == "$it_bcport1a_0051").Source);
    }

    [Fact]
    public void UndecodableSourcePreservesIdentityFieldNameAndStructuredContext()
    {
        LocalizationAuthoringRequest request = new("jp", "jp", "Alternate", [], "Test translation");
        LocalizationSourceEntry source = new("Fields", "test", "$jp_test_0000", "", "jp", new Dictionary<string, string?> { ["Source Present"] = "false", ["Decode Error"] = "Unknown source character", ["Field Name"] = "", ["Context Bindings"] = "[{\"operation\":12}]" });
        LocalizationProject project = LocalizationProjectBuilder.CreateProject(request, new Dictionary<string, LocalizationSourceCatalog> { ["jp"] = new([source], [], []) }, TestContext.Current.CancellationToken);
        LocalizationRecord row = Assert.Single(project.Entries);
        Assert.False(row.SourcePresent);
        Assert.Null(row.Source);
        Assert.Equal("", row.ExtensionData["Field Name"].GetString());
        Assert.Equal(12, row.ExtensionData["Context Bindings"][0].GetProperty("operation").GetInt32());
        Assert.Equal("Unknown source character", row.ExtensionData["sourceGap"].GetString());
    }

    [Fact]
    public void AuthoredInterfaceTranslationUsesSemanticIdentityAndLeavesItemNamesAlone()
    {
        MenuData data = new();
        data.Items.Add(new MenuItem { Id = "item-1", Name = "Junction", Quantity = 1 });
        MenuController menus = new(() => data);
        menus.ConnectLocalizationText((key, original) => key == SystemTextCatalog.Key("main.junction") ? "Translated command" : original);
        menus.Open("main");
        MenuEntry command = Assert.Single(menus.Entries, entry => entry.Id == "junction");
        Assert.Equal("Translated command", command.Label);
        Assert.Equal("junction", command.Destination);
        menus.Open("item-use");
        Assert.Contains(menus.Entries, entry => entry.Label.StartsWith("Junction", StringComparison.Ordinal));
    }

    [Fact]
    public void LoadedProjectDatasetUsesOnlyItsTranslatedContributionAndKeepsMissingRows()
    {
        LocalizationProject contribution = new()
        {
            Manifest = new() { TargetLanguage = "Custom translation", MappingVersion = Memoria.FF8.Assets.Localization.FieldCorrespondenceCatalog.Version },
            Entries =
            [
                new() { Category = "Fields", File = "bcport1a", Key = "$en_bcport1a_0050", Source = "Native source is not translated content", Target = "Translated dialogue" },
                new() { Category = "Fields", File = "bcport1a", Key = "$en_bcport1a_0000", Source = "Untranslated native source", Target = "" }
            ]
        };
        string dataset = LocalizationProjectBuilder.ProjectDatasetId(contribution);
        LocalizationSourceCatalog source = LocalizationProjectBuilder.ProjectDataset(contribution, dataset, cancellation: TestContext.Current.CancellationToken);
        Assert.Equal("Translated dialogue", Assert.Single(source.Entries, row => row.Key == "$en_bcport1a_0050").Text);
        Assert.Equal("false", Assert.Single(source.Entries, row => row.Key == "$en_bcport1a_0000").Metadata["Source Present"]);
        LocalizationProject result = LocalizationProjectBuilder.CreateProject(new(dataset, "en", "New independent translation", [], "Test project"), new Dictionary<string, LocalizationSourceCatalog> { [dataset] = source, ["en"] = new([], [], []) }, TestContext.Current.CancellationToken);
        Assert.Equal(dataset, result.Manifest.SourceLanguage);
        Assert.Equal("Translated dialogue", Assert.Single(result.Entries, row => row.Key == "$en_bcport1a_0050").Source);
        LocalizationRecord missing = Assert.Single(result.Entries, row => row.Key == "$en_bcport1a_0000");
        Assert.Null(missing.Source);
        Assert.False(missing.SourcePresent);
        Assert.Equal(contribution.Manifest.ProjectId, missing.ExtensionData["Dataset Project Id"].GetString());
    }

    [Fact]
    public void OfflineConversionExposesNoSourceLanguageRequirement()
    {
        MenuController menus = new();
        menus.ConnectLocalizationAuthoring(new(Path.GetTempPath(), () => throw new InvalidOperationException("Conversion must not probe installed game resources."), (_, _, _) => throw new InvalidOperationException("Conversion must not extract installed game resources.")));
        menus.Open("mods-create");
        menus.Execute(new("localization-convert"));
        Assert.Contains(menus.Entries, entry => entry.Id == "input");
        Assert.DoesNotContain(menus.Entries, entry => entry.Id is "source" or "base" or "target");
    }

    private static MenuController CreateMenus()
    {
        MenuController menus = new();
        menus.ConnectLocalizationAuthoring(new(Path.GetTempPath(), () => [new("en", "English"), new("fr", "French"), new("jp", "Japanese", false, "No matching decoder/resources")], (_, _, _) => throw new InvalidOperationException("No extraction expected.")));
        return menus;
    }
}
