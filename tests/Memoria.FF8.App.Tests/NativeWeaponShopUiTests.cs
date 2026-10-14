using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeWeaponShopUiTests
{
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
                Assert.Contains($"Item {material.ItemId}: {shop.Counts[material.ItemId]}/{material.Count}", item.Description);
            }
        }
        Assert.Empty(data.SupportedActions);
        Assert.False(data.SaveAvailable);
        Assert.Equal(before, JsonSerializer.Serialize(shop));
    }

    [Fact]
    public void ReadOnlyRecipeConfirmCannotUpgradeAndCancelClosesThePanel()
    {
        using GameAssets assets = InstalledAssets();
        NativeWeaponShopTransaction shop = OriginalShop(assets);
        string before = JsonSerializer.Serialize(shop);
        List<MenuAction> actions = [];
        MenuController controller = new(() => NativeWeaponShopMenuProjection.Build(shop), actions.Add);
        controller.Open("shop-junk");
        Assert.NotEmpty(controller.Entries);
        Assert.All(controller.Entries, entry => Assert.False(entry.Enabled));
        Assert.NotNull(controller.BuildView());
        controller.Navigate(MenuInput.Confirm);
        Assert.True(controller.IsOpen);
        Assert.Empty(actions);
        controller.Navigate(MenuInput.Cancel);
        Assert.False(controller.IsOpen);
        Assert.Empty(actions);
        Assert.Equal(before, JsonSerializer.Serialize(shop));
    }

    private static NativeWeaponShopTransaction OriginalShop(GameAssets assets)
    {
        //Declared installed-data/UI component, without a field visit or fabricated replay.
        return NativeWeaponShopTransaction.Begin(34, assets.ReadNativeWeaponShopDefinitions(), assets.ReadNewGameEconomy(), assets.ReadNewGameCharacterVitals(), assets.ReadNewGameWeaponRecipes()).Shop;
    }

    private static GameAssets InstalledAssets()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original weapon, kernel and initialization resources are required.");
        return new GameAssets(directory);
    }
}
