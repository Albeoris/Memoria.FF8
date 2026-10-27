using Memoria.FF8.Assets;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.GameUi;
using Memoria.FF8.Resources.Localization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class LiveMenuCatalogTagTests
{
    [Fact]
    public void CatalogNamesAndDescriptionsResolveCurrentNamesExactlyOnce()
    {
        Assert.SkipUnless(Directory.Exists(TestResources.GameDirectory), "Installed native menu resources are required.");
        using GameAssets assets = new(TestResources.GameDirectory);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        string language = assets.Resources.Source.Language;
        Dictionary<string, LocalizationValue> translations = new();
        foreach ((string collection, string role, int ordinal) in new[]
        {
            ("MagicSpells", "Name", 1),
            ("MagicSpells", "Description", 1),
            ("BattleItems", "Name", 1),
            ("BattleCommands", "Name", 1),
            ("Weapons", "Name", 0),
            ("JunctionAbilities", "Name", 1),
            ("JunctionAbilities", "Description", 1),
            ("ZellLimits", "Name", 0),
            ("ZellLimits", "Description", 0)
        })
        {
            translations[LocalizationSourceExporter.IndexedKey(language, LocalizationSourceExporter.KernelTable(collection, role), ordinal)] = new("{Char Angelo2} Rush", "test", "test");
        }
        translations[LocalizationSourceExporter.IndexedKey(language, "names_cards", 0)] = new("{Char Angelo1} Rush", "test", "test");
        assets.Resources.Prepare(null, localization: new(translations)).Commit();
        GameState state = new() { CharacterVitals = assets.ReadNewGameCharacterVitals(), LimitProgress = assets.ReadNewGameLimitProgress() };
        LiveMenuData.Build(state, new Dictionary<int, string> { [2] = "Comet{Page}" }, catalog);
        Assert.Equal("Comet{Page} Rush", catalog.MagicName(1));
        Assert.Equal("Comet{Page} Rush", catalog.MagicDescription(1));
        Assert.Equal("Comet{Page} Rush", catalog.ItemName(1));
        Assert.Equal("Comet{Page} Rush", catalog.CommandName(1));
        Assert.Equal("Comet{Page} Rush", catalog.WeaponName(0));
        Assert.Equal("Comet{Page} Rush", catalog.AbilityName(1));
        Assert.Equal("Comet{Page} Rush", catalog.AbilityDescription(1));
        Assert.Equal("Comet{Page} Rush", catalog.CardName(0));
        MenuCharacter zell = new() { Id = "zell", Name = "Zell", NativeId = 1 };
        catalog.ProjectLimit(zell, 0, state, new Dictionary<int, string> { [2] = "Comet{Page}" });
        Assert.Equal("Comet{Page} Rush", zell.LimitMoves[0].Name);
        Assert.Equal("Comet{Page} Rush", zell.LimitMoves[0].Description);
        LiveMenuData.Build(new GameState(), new Dictionary<int, string> { [2] = "Nova" }, catalog);
        Assert.Equal("Nova Rush", catalog.MagicName(1));
    }
}
