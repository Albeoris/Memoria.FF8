using Memoria.FF8.GameUi;

namespace Memoria.FF8.App.Tests;

public sealed class LocalizationReferenceOrderTests
{
    [Fact]
    public void NewProjectKeepsSourceReferenceIndependentOfColumnOrder()
    {
        MenuController menus = CreateMenus();
        menus.Open("mods-create");
        menus.Execute(new("localization-new"));

        Assert.Contains(menus.Entries, entry => entry.Id == "source-reference" && entry.Label == "Source Reference: jp");
        Assert.Contains(menus.Entries, entry => entry.Id == "base" && entry.Label == "Base game language: en");
        Assert.Contains(menus.Entries, entry => entry.Id == "source-reference" && entry.Label == "Source Reference: jp");
        Assert.Contains(menus.Entries, entry => entry.Id == "references" && entry.Label == "References: jp, en");

        menus.Execute(new("localization-language", "references"));
        menus.Execute(new("localization-select-language", "fr"));
        menus.Execute(new("localization-reference-move", "fr", Value: "-1"));
        menus.Execute(new("localization-reference-move", "fr", Value: "-1"));
        menus.Back();
        menus.Execute(new("localization-format"));

        Assert.Contains(menus.Entries, entry => entry.Id == "references" && entry.Label == "References: fr, jp, en");
        Assert.Contains(menus.Entries, entry => entry.Id == "base" && entry.Label == "Base game language: en");
    }

    [Fact]
    public void ReferencesRetainBaseLanguageAndShowColumnOrder()
    {
        MenuController menus = CreateMenus();
        menus.Open("mods-create");
        menus.Execute(new("localization-new"));
        menus.Execute(new("localization-language", "source-reference"));
        menus.Execute(new("localization-select-language", "fr"));
        menus.Execute(new("localization-language", "references"));
        menus.Execute(new("localization-select-language", "jp"));
        menus.Execute(new("localization-select-language", "en"));

        Assert.Contains(menus.Entries, entry => entry.Id == "en" && entry.Label == "[x] 1. English");
        Assert.Contains(menus.Entries, entry => entry.Id == "earlier-en" && !entry.Enabled);
        menus.Back();
        Assert.Contains(menus.Entries, entry => entry.Id == "references" && entry.Label == "References: en, fr");
        Assert.Contains(menus.Entries, entry => entry.Id == "source-reference" && entry.Label == "Source Reference: fr");
    }

    private static MenuController CreateMenus()
    {
        MenuController menus = new();
        menus.ConnectLocalizationAuthoring(new(Path.GetTempPath(), () => [new("en", "English"), new("jp", "Japanese"), new("fr", "French")], (_, _, _) => throw new InvalidOperationException("No extraction expected.")));
        return menus;
    }
}
