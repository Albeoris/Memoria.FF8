using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeWeaponShopUiTests
{
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
                Assert.Contains($"Item {material.ItemId}: {shop.Counts[material.ItemId]}/{material.Count}", item.Description);
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

    private static GameAssets InstalledAssets()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original weapon, kernel and initialization resources are required.");
        return new GameAssets(directory);
    }
}
