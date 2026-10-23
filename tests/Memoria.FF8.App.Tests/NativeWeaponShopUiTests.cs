using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class NativeWeaponShopUiTests
{
    [Fact]
    public void Known_recipe_explains_missing_previous_upgrade_and_cannot_be_selected()
    {
        using GameAssets assets = InstalledAssets();
        NativeWeaponRecipes recipes = assets.ReadNewGameWeaponRecipes();
        recipes = recipes with { Bits = recipes.Bits | 1u << 2 };
        var prepared = NativeWeaponShopTransaction.Begin(34, assets.ReadNativeWeaponShopDefinitions(), assets.ReadNewGameEconomy(), assets.ReadNewGameCharacterVitals(), recipes, new() { WeaponUpgradeOrder = true });
        MenuData data = NativeWeaponShopMenuProjection.Build(prepared.Shop, prepared.Vitals);
        Assert.Contains("Previous weapon upgrade not obtained", Assert.Single(data.Items, item => item.Id == "weapon-2").Description);
        Assert.DoesNotContain("weapon-remodel:weapon-2", data.SupportedActions);
    }

    [Fact]
    public void Material_discovered_recipe_explains_missing_magazine_and_cannot_be_selected()
    {
        using GameAssets assets = InstalledAssets();
        NativeWeaponRecipes recipes = assets.ReadNewGameWeaponRecipes();
        recipes = recipes with { Bits = recipes.Bits | 1u << 1 };
        var prepared = NativeWeaponShopTransaction.Begin(34, assets.ReadNativeWeaponShopDefinitions(), assets.ReadNewGameEconomy(), assets.ReadNewGameCharacterVitals(), recipes, new() { WeaponMagazineRequired = true });
        NativeWeaponShopTransaction restored = JsonSerializer.Deserialize<NativeWeaponShopTransaction>(JsonSerializer.Serialize(prepared.Shop))!;
        MenuData data = NativeWeaponShopMenuProjection.Build(restored with { AvailableRecipes = [(byte)0xC1] }, prepared.Vitals);
        Assert.Contains("Weapons Monthly recipe not read", Assert.Single(data.Items, item => item.Id == "weapon-1").Description);
        Assert.DoesNotContain("weapon-remodel:weapon-1", data.SupportedActions);
    }

    [Theory]
    [InlineData(Memoria.FF8.Events.PlayerActionKind.SelectWeaponShopCharacter, -1)]
    [InlineData(Memoria.FF8.Events.PlayerActionKind.SelectWeaponShopCharacter, 6)]
    [InlineData(Memoria.FF8.Events.PlayerActionKind.RemodelWeapon, -1)]
    [InlineData(Memoria.FF8.Events.PlayerActionKind.RemodelWeapon, 28)]
    public void InvalidWeaponInputRefusesBeforeSessionMutation(Memoria.FF8.Events.PlayerActionKind kind, int value)
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        Memoria.FF8.Events.EventEngine events = new(session, NativeEventMemory.FromDWords(new Dictionary<int, int>()));
        GameState before = session.State;
        Assert.Throws<ArgumentException>(() => events.Apply(new Memoria.FF8.Events.PlayerAction(kind, value)));
        Assert.Same(before, session.State);
    }

    [Fact]
    public void InitialNativeCharacterOffersShowExactRecipesFeesAndMaterialsWithoutMutation()
    {
        using GameAssets assets = InstalledAssets();
        NativeWeaponShopTransaction shop = OriginalShop(assets);
        string before = JsonSerializer.Serialize(shop);
        MenuData data = NativeWeaponShopMenuProjection.Build(shop);
        Assert.NotEmpty(shop.AvailableRecipes);
        Assert.Equal(shop.AvailableRecipes.Length, data.Items.Count);
        Assert.Equal(checked((int)shop.Gil), data.Currency);
        for (int index = 0; index < shop.AvailableRecipes.Length; index++)
        {
            int id = shop.AvailableRecipes[index] & 0x3F;
            NativeWeaponRecipe recipe = shop.Definitions.Recipes[id];
            MenuItem item = data.Items[index];
            Assert.Equal($"weapon-{id}", item.Id);
            Assert.Equal(recipe.Name, item.Name);
            Assert.Equal(checked((int)shop.Price(id)), item.Price);
            Assert.Equal(shop.SelectedCharacter, recipe.Character);
            foreach (NativeItemStock material in recipe.Materials.Where(material => material.ItemId != 0))
            {
                Assert.Contains(item.Materials, value => value.Name == $"Item {material.ItemId}" && value.Owned == shop.Counts[material.ItemId] && value.Required == material.Count);
            }
        }
        Assert.Contains("weapon-character:character-0", data.SupportedActions);
        Assert.False(data.SaveAvailable);
        Assert.Equal(before, JsonSerializer.Serialize(shop));
    }

    [Fact]
    public void CharacterSelectionDispatchesAndUnaffordableRecipeCannotUpgrade()
    {
        using GameAssets assets = InstalledAssets();
        NativeWeaponShopTransaction shop = OriginalShop(assets);
        string before = JsonSerializer.Serialize(shop);
        List<MenuAction> actions = [];
        MenuController controller = new(() => NativeWeaponShopMenuProjection.Build(shop), actions.Add);
        controller.Open("shop-junk");
        Assert.NotEmpty(controller.Entries);
        Assert.True(controller.Entries[0].Enabled);
        Assert.All(controller.Entries.Skip(shop.EligibleMask == 1 ? 1 : 0), entry => Assert.False(entry.Enabled));
        Assert.NotNull(controller.BuildView());
        controller.Navigate(MenuInput.Confirm);
        Assert.True(controller.IsOpen);
        Assert.Equal(new MenuAction("weapon-character", "character-0"), Assert.Single(actions));
        controller.Navigate(MenuInput.Cancel);
        Assert.False(controller.IsOpen);
        Assert.Single(actions);
        Assert.Equal(before, JsonSerializer.Serialize(shop));
    }

    private static NativeWeaponShopTransaction OriginalShop(GameAssets assets)
    {
        //Declared installed-data/UI component, without a field visit or fabricated replay.
        return NativeWeaponShopTransaction.Begin(34, assets.ReadNativeWeaponShopDefinitions(), assets.ReadNewGameEconomy(), assets.ReadNewGameCharacterVitals(), assets.ReadNewGameWeaponRecipes()).Shop;
    }

    [Fact]
    public void AffordableRecipeDispatchesRemodelAndEquippedRecipeIsDisabled()
    {
        using GameAssets assets = InstalledAssets();
        NativeEconomy economy = assets.ReadNewGameEconomy().DeepCopy();
        economy.Items[0] = new(66, 2);
        economy.Items[1] = new(118, 8);
        var prepared = NativeWeaponShopTransaction.Begin(34, assets.ReadNativeWeaponShopDefinitions(), economy, assets.ReadNewGameCharacterVitals(), assets.ReadNewGameWeaponRecipes());
        MenuData data = NativeWeaponShopMenuProjection.Build(prepared.Shop, prepared.Vitals);
        Assert.Contains("weapon-remodel:weapon-1", data.SupportedActions);
        Assert.Equal(new Memoria.FF8.Events.PlayerAction(Memoria.FF8.Events.PlayerActionKind.RemodelWeapon, 1), NativeWeaponShopMenuProjection.PlayerAction(new MenuAction("weapon-remodel", "weapon-1")));
        var remodeled = prepared.Shop.Remodel(1, prepared.Vitals, prepared.Recipes, out bool accepted);
        Assert.True(accepted);
        Assert.DoesNotContain("weapon-remodel:weapon-1", NativeWeaponShopMenuProjection.Build(remodeled.Shop, remodeled.Vitals).SupportedActions);
    }

    [Fact]
    public void WorkshopSeparatesCharactersWeaponsAndOriginalMaterialsAfterLiveSelection()
    {
        using GameAssets assets = InstalledAssets();
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        var prepared = NativeWeaponShopTransaction.Begin(34, assets.ReadNativeWeaponShopDefinitions(), assets.ReadNewGameEconomy(), assets.ReadNewGameCharacterVitals(), assets.ReadNewGameWeaponRecipes());
        string before = JsonSerializer.Serialize(prepared.Shop);
        List<MenuAction> actions = [];
        MenuController controller = new(() => NativeWeaponShopMenuProjection.Build(prepared.Shop, prepared.Vitals, catalog), actions.Add);
        controller.Open("shop-junk");
        controller.Navigate(MenuInput.Confirm);
        Assert.Single(actions);
        Assert.Empty(controller.Message);
        for (int index = 0; index < controller.Data.Characters.Count; index++)
        {
            controller.Navigate(MenuInput.Down);
        }
        UiElement[] elements = Descendants(controller.BuildView().Root).ToArray();
        MenuItem selected = controller.Data.Items.Single(item => item.Id == controller.SelectedEntry!.Id);
        UiList characters = Assert.Single(elements.OfType<UiList>(), list => list.Id == "weapon-characters");
        UiList weapons = Assert.Single(elements.OfType<UiList>(), list => list.Id == "commands");
        UiList materials = Assert.Single(elements.OfType<UiList>(), list => list.Id == "weapon-materials");
        Assert.False(characters.ShowSelection);
        Assert.True(weapons.ShowSelection);
        Assert.Equal(controller.Data.Items.Select(item => item.Name), weapons.Items);
        Assert.Equal(controller.Data.Items.Select(item => item.Price + " G"), weapons.SecondaryItems);
        Assert.Contains(elements.OfType<UiLabel>(), label => label.Text == "Junk Shop");
        Assert.Contains(elements.OfType<UiLabel>(), label => label.Text == selected.Description);
        Assert.DoesNotContain(elements.OfType<UiLabel>(), label => label.Text == "Owned" || label.Text.Contains("live owner"));
        NativeWeaponRecipe recipe = prepared.Shop.Definitions.Recipes[int.Parse(selected.Id.AsSpan(7))];
        Assert.Equal(recipe.Materials.Count(material => material.ItemId != 0), materials.Items.Count);
        int row = 0;
        foreach (NativeItemStock material in recipe.Materials.Where(material => material.ItemId != 0))
        {
            Assert.Equal(catalog.ItemName(material.ItemId), materials.Items[row]);
            Assert.Equal(catalog.ItemIcon(material.ItemId), materials.Images[row]);
            Assert.Equal($"{prepared.Shop.Counts[material.ItemId]} / {material.Count}", materials.SecondaryItems[row]);
            row++;
        }
        Assert.Equal(before, JsonSerializer.Serialize(prepared.Shop));
    }

    [Fact]
    public void WorkshopPointerRowsKeepCharacterAndWeaponActionIndicesSeparate()
    {
        MenuData data = new();
        data.Characters.Add(new MenuCharacter { Id = "character-0", Name = "Squall", InParty = true });
        data.Characters.Add(new MenuCharacter { Id = "character-1", Name = "Zell" });
        data.Items.Add(new MenuItem { Id = "weapon-0", Name = "Revolver", Price = 100 });
        data.Items.Add(new MenuItem { Id = "weapon-1", Name = "Shear Trigger", Price = 200 });
        data.SupportedActions.UnionWith(["weapon-character:character-0", "weapon-character:character-1", "weapon-remodel:weapon-1"]);
        List<MenuAction> actions = [];
        MenuController controller = new(() => data, actions.Add);
        controller.Open("shop-junk");
        UiList characters = Descendants(controller.BuildView().Root).OfType<UiList>().Single(list => list.Id == "weapon-characters");
        characters.SelectionChanged!(1);
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal(new MenuAction("weapon-character", "character-1"), Assert.Single(actions));
        controller.Navigate(MenuInput.Down);
        Assert.Equal("weapon-0", controller.SelectedEntry!.Id);
        UiList weapons = Descendants(controller.BuildView().Root).OfType<UiList>().Single(list => list.Id == "commands");
        weapons.SelectionChanged!(1);
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal(new MenuAction("weapon-remodel", "weapon-1"), actions[1]);
        controller.Navigate(MenuInput.Up);
        controller.Navigate(MenuInput.Up);
        Assert.Equal("character-1", controller.SelectedEntry!.Id);
    }

    private static IEnumerable<UiElement> Descendants(UiElement root)
    {
        yield return root;
        foreach (UiElement child in root.Children)
        {
            foreach (UiElement descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static GameAssets InstalledAssets()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original weapon, kernel and initialization resources are required.");
        return new GameAssets(directory);
    }
}
