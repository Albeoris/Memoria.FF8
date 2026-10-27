using Memoria.FF8.GameUi;
using Memoria.FF8.Mods.Localization;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.App.Tests;

public sealed class LocalizationAuthoringTests
{
    [Fact]
    public void NewProjectDatasetsDoNotInspectLoadedModsOrActiveProfile()
    {
        string missingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        LocalizationAuthoringServices services = LocalizationProjectController.CreateServices(missingDirectory, missingDirectory,
            readLoaded: () => throw new InvalidOperationException("Source export must not inspect loaded mods."),
            readProfileOrder: () => throw new InvalidOperationException("Source export must not inspect the active profile."));

        IReadOnlyList<LocalizationDatasetOption> datasets = services.ReadDatasets();

        Assert.Equal(new[] { "en", "fr", "de", "es", "it", "jp" }, datasets.Select(dataset => dataset.Id));
    }

    [Theory]
    [InlineData("profile:active")]
    [InlineData("project:translation")]
    public void NewProjectRejectsModDatasetsBeforeReadingResources(string dataset)
    {
        string missingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        LocalizationAuthoringServices services = LocalizationProjectController.CreateServices(missingDirectory, missingDirectory,
            readLoaded: () => throw new InvalidOperationException("Source export must not inspect loaded mods."),
            readProfileOrder: () => throw new InvalidOperationException("Source export must not inspect the active profile."));
        LocalizationAuthoringRequest request = new(dataset, "en", "Custom", [], "Test translation");

        InvalidDataException error = Assert.Throws<InvalidDataException>(() => services.Prepare(request, TestContext.Current.CancellationToken, new Progress<string>()));

        Assert.Contains("original", error.Message, StringComparison.OrdinalIgnoreCase);
    }

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
        Assert.Contains(menus.Entries, entry => entry.Id == "base" && entry.Label == "Base game language: fr");
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
    public void BaseInventoryExcludesReferenceOnlyEntriesAndPreservesMissingReferences()
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
        LocalizationRecord original = Assert.Single(project.Entries);
        Assert.Equal(english.Key, original.Key);
        Assert.True(original.SourcePresent);
        Assert.Equal("Fire", original.Source);
        Assert.Equal("Fire", original.References["Reference [en]"]);
        Assert.Null(original.References["Reference [fr]"]);
        Assert.False(original.ReferencePresence["Reference [fr]"]);
        Assert.Empty(original.ReferenceKeys["Reference [fr]"]);
        Assert.DoesNotContain(project.Entries, row => row.Key == french.Key);
        Assert.Equal("Alternate", project.Manifest.TargetLanguage);
    }

    [Fact]
    public void MultipleCounterpartsStayOnBaseRowWithTheirReferenceIdentities()
    {
        LocalizationAuthoringRequest request = new("it", "en", "Alternate", ["en"], "Test translation");
        LocalizationSourceEntry Row(string key, string text, string language) => new("Fields", "bcport1a", key, text, language, new Dictionary<string, string?>());
        Dictionary<string, LocalizationSourceCatalog> catalogs = new()
        {
            ["en"] = new([Row("$en_bcport1a_0050", "English dialogue", "en")], [], []),
            ["it"] = new([Row("$it_bcport1a_0049", "First dialogue", "it"), Row("$it_bcport1a_0051", "Second dialogue", "it")], [], [])
        };
        LocalizationProject project = LocalizationProjectBuilder.CreateProject(request, catalogs, TestContext.Current.CancellationToken);
        LocalizationRecord english = Assert.Single(project.Entries);
        Assert.Equal("$en_bcport1a_0050", english.Key);
        Assert.True(english.SourcePresent);
        Assert.Equal("English dialogue", english.Source);
        Assert.Equal("English dialogue", english.References["Reference [en]"]);
        Assert.Equal("First dialogue{Line}Second dialogue", english.References["Reference [it]"]);
        Assert.Equal(new[] { "$it_bcport1a_0049", "$it_bcport1a_0051" }, english.ReferenceKeys["Reference [it]"]);
        Assert.Equal(2, english.ExtensionData["equivalentKeys"].GetProperty("it").GetArrayLength());
        Assert.Equal(2, english.ExtensionData["equivalentContexts"].GetProperty("it").GetArrayLength());
        Assert.DoesNotContain(project.Entries, row => row.Key.StartsWith("$it_", StringComparison.Ordinal));
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
        LocalizationSourceCatalog native = new([
            new("Fields", "bcport1a", "$en_bcport1a_0050", "Native source is not translated content", "en", new Dictionary<string, string?>()),
            new("Fields", "bcport1a", "$en_bcport1a_0000", "Untranslated native source", "en", new Dictionary<string, string?>())
        ], [], []);
        LocalizationProject result = LocalizationProjectBuilder.CreateProject(new(dataset, "en", "New independent translation", [], "Test project"), new Dictionary<string, LocalizationSourceCatalog> { [dataset] = source, ["en"] = native }, TestContext.Current.CancellationToken);
        Assert.Equal("en", result.Manifest.SourceLanguage);
        string label = "Reference [" + dataset + "]";
        Assert.Equal(dataset, result.Manifest.ReferenceLanguages[label]);
        LocalizationRecord translated = Assert.Single(result.Entries, row => row.Key == "$en_bcport1a_0050");
        Assert.Equal("Native source is not translated content", translated.Source);
        Assert.Equal("Translated dialogue", translated.References[label]);
        Assert.True(translated.ReferencePresence[label]);
        LocalizationRecord missing = Assert.Single(result.Entries, row => row.Key == "$en_bcport1a_0000");
        Assert.Equal("Untranslated native source", missing.Source);
        Assert.True(missing.SourcePresent);
        Assert.Null(missing.References[label]);
        Assert.False(missing.ReferencePresence[label]);
        Assert.Equal(new[] { "$en_bcport1a_0000" }, missing.ReferenceKeys[label]);
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

    [Fact]
    public void PatchAuthoringRequiresBasePackageWithoutProbingInstalledLanguagesOrIndependentTargetIds()
    {
        MenuController menus = new();
        menus.ConnectLocalizationAuthoring(new(Path.GetTempPath(), () => throw new InvalidOperationException("A patch must not probe installed resources."), (_, _, _) => throw new InvalidOperationException("A patch must preserve the base project's source snapshot."), ReadTargetIds: () => throw new InvalidOperationException("A patch inherits its Target identity.")));
        menus.Open("mods-create");
        Assert.Contains(menus.Entries, entry => entry.Action?.Kind == "localization-patch");
        menus.Execute(new("localization-patch"));
        Assert.Contains(menus.Entries, entry => entry.Id == "input");
        Assert.Contains(menus.Entries, entry => entry.Id == "package");
        Assert.DoesNotContain(menus.Entries, entry => entry.Id is "source" or "base" or "target" or "references");
        menus.Execute(new("localization-format"));
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
