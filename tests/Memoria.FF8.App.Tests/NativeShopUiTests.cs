using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeShopUiTests
{
    [Fact]
    public void OriginalShopRowsShowPetPalsPricesWithoutMutatingTheNativeCache()
    {
        using GameAssets assets = InstalledAssets();
        NativeShopTransaction shop = OriginalShop(assets);
        string original = JsonSerializer.Serialize(shop);
        MenuData data = NativeShopMenuProjection.Build(shop);
        Assert.Equal(1000, data.Items.Single(item => item.Id == "item-191").Price);
        Assert.Equal(1000, data.Items.Single(item => item.Id == "item-192").Price);
        Assert.Equal(shop.Definitions.ResolvePrice(191).Name, data.Items.Single(item => item.Id == "item-191").Name);
        Assert.Equal(checked((int)shop.Gil), data.Currency);
        Assert.Contains("shop-buy:item-191", data.SupportedActions);
        Assert.False(data.SaveAvailable);
        Assert.Equal(original, JsonSerializer.Serialize(shop));
    }

    [Fact]
    public void GridConfirmationEmitsAnExplicitPurchaseAndRefreshesFromTheActualCache()
    {
        using GameAssets assets = InstalledAssets();
        NativeEconomy economy = assets.ReadNewGameEconomy();
        string savedInventory = JsonSerializer.Serialize(economy);
        NativeShopTransaction shop = OriginalShop(assets);
        uint gil = shop.Gil;
        byte count = shop.Counts[191];
        List<PlayerAction> inputs = [];
        // Declared UI/cache component.
        MenuController controller = new(() => NativeShopMenuProjection.Build(shop), action =>
        {
            PlayerAction input = NativeShopMenuProjection.PurchaseAction(action);
            inputs.Add(input);
            shop = shop.Buy(input.Value, input.Quantity!.Value, out bool accepted);
            Assert.True(accepted);
        });
        controller.Open("shop-buy");
        int selected = controller.Entries.ToList().FindIndex(entry => entry.Id == "item-191");
        Assert.True(selected >= 0);
        for (int index = 0; index < selected; index++)
        {
            controller.Navigate(MenuInput.Down);
        }
        Assert.NotNull(controller.BuildView());
        controller.Navigate(MenuInput.Confirm);
        PlayerAction purchase = Assert.Single(inputs);
        Assert.Equal(PlayerActionKind.BuyShopItem, purchase.Kind);
        Assert.Equal(191, purchase.Value);
        Assert.Equal(1, purchase.Quantity);
        Assert.Equal(gil - 1000, shop.Gil);
        Assert.Equal(count + 1, shop.Counts[191]);
        Assert.Equal(checked((int)shop.Gil), controller.Data.Currency);
        Assert.Equal(count + 1, controller.Data.Items.Single(item => item.Id == "item-191").Quantity);
        Assert.Equal(savedInventory, JsonSerializer.Serialize(economy));
        controller.Navigate(MenuInput.Cancel);
        Assert.False(controller.IsOpen);
        Assert.Single(inputs);
    }

    [Fact]
    public void PurchaseWithoutAnExplicitQuantityRefusesInsteadOfBuyingOne()
    {
        Assert.Throws<ArgumentException>(() => NativeShopMenuProjection.PurchaseAction(new MenuAction("shop-buy", "item-191")));
    }

    private static NativeShopTransaction OriginalShop(GameAssets assets)
    {
        NativeShopDefinitions definitions = assets.ReadNativeShopDefinitions();
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        NativeEconomy economy = NativeShopTransaction.NormalizeInventory(assets.ReadNewGameEconomy());
        NativeShopHistory history = assets.ReadNewGameShopHistory().Refresh(definitions, definitions.ResolveCatalog(0), vitals.ShopAbilityMask());
        return NativeShopTransaction.Begin(0, definitions, economy, vitals, history);
    }

    private static GameAssets InstalledAssets()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original shop and initialization resources are required.");
        return new GameAssets(directory);
    }
}
