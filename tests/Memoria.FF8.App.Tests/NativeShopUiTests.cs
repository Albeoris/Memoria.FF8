using System.Buffers.Binary;
using Albeoris.Games.FF8.Jsm;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class NativeShopUiTests
{
    [Fact]
    public void OriginalShopRowsShowPetPalsPricesWithoutMutatingTheNativeCache()
    {
        using GameAssets assets = InstalledAssets();
        NativeShopTransaction shop = OriginalShop(assets);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        string original = JsonSerializer.Serialize(shop);
        MenuData data = NativeShopMenuProjection.Build(shop, catalog);
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
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        uint gil = shop.Gil;
        byte count = shop.Counts[191];
        List<PlayerAction> inputs = [];
        // Declared UI/cache component.
        MenuController controller = new(() => NativeShopMenuProjection.Build(shop, catalog), action =>
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

    [Fact]
    public void SaleListsOwnedItemsOutsideTheCatalogAndUsesNativePrices()
    {
        using GameAssets assets = InstalledAssets();
        NativeShopTransaction shop = OriginalShop(assets);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        int itemId = Enumerable.Range(1, 198).First(id => !shop.AvailableItems.Contains((byte)id) && shop.Definitions.Prices[id].Name is not null);
        shop = shop with { Counts = shop.Counts.SetItem(itemId, 2), AbilityMask = 2 };
        uint gil = shop.Gil;
        List<PlayerAction> inputs = [];
        MenuController controller = new(() => NativeShopMenuProjection.Build(shop, catalog), action =>
        {
            PlayerAction input = NativeShopMenuProjection.PurchaseAction(action);
            inputs.Add(input);
            shop = shop.Sell(input.Value, input.Quantity!.Value, out bool accepted);
            Assert.True(accepted);
        });
        controller.Open("shop");
        Assert.Equal(new[] { "shop-buy", "shop-sell" }, controller.Entries.Select(entry => entry.Id));
        controller.Navigate(MenuInput.Down);
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal("shop-sell", controller.ScreenId);
        MenuItem row = controller.Data.Items.Single(item => item.Id == $"item-{itemId}");
        Assert.Null(row.Price);
        Assert.Equal((int)shop.Definitions.SellPrice(itemId, 2), row.SalePrice);
        Assert.DoesNotContain($"shop-buy:item-{itemId}", controller.Data.SupportedActions);
        int selected = controller.Entries.ToList().FindIndex(entry => entry.Id == row.Id);
        for (int index = 0; index < selected; index++)
        {
            controller.Navigate(MenuInput.Down);
        }
        Assert.NotNull(controller.BuildView());
        controller.Navigate(MenuInput.Confirm);
        PlayerAction sale = Assert.Single(inputs);
        Assert.Equal(PlayerActionKind.SellShopItem, sale.Kind);
        Assert.Equal(itemId, sale.Value);
        Assert.Equal(1, sale.Quantity);
        Assert.Equal(gil + (uint)row.SalePrice!.Value, shop.Gil);
        Assert.Equal(1, shop.Counts[itemId]);
        Assert.Equal(1, controller.Data.Items.Single(item => item.Id == row.Id).Quantity);
        controller.Navigate(MenuInput.Cancel);
        Assert.Equal("shop", controller.ScreenId);
    }

    [Fact]
    public void SaleRequiresExplicitQuantity()
    {
        Assert.Throws<ArgumentException>(() => NativeShopMenuProjection.PurchaseAction(new MenuAction("shop-sell", "item-191")));
    }

    [Fact]
    public void ExecutedShopSalePublishesLocalCacheThenCommitsOnClose()
    {
        using GameAssets assets = InstalledAssets();
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
        session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
        session.InitializeNewGameShotAvailability(assets.ReadNewGameShotAvailability());
        session.InitializeNewGameInputConfiguration(assets.ReadNewGameInputConfiguration());
        session.InitializeNewGameShopHistory(assets.ReadNewGameShopHistory());
        EventEngine engine = new(session) { ResolveNativeShopDefinitions = assets.ReadNativeShopDefinitions };
        // Declared opcode/menu component using original initialized owners, without a field visit.
        byte[] script = new byte[40];
        script[3] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(script.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(script.AsSpan(4), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(script.AsSpan(6), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(script.AsSpan(12), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(script.AsSpan(14), 6);
        int[] operations = [(int)Jsm.Opcode.LBL, (int)Jsm.Opcode.RET << 24 | 8, (int)Jsm.Opcode.LBL, (int)Jsm.Opcode.PSHN_L << 24, (int)Jsm.Opcode.MENUSHOP, (int)Jsm.Opcode.RET << 24 | 8];
        for (int index = 0; index < operations.Length; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(script.AsSpan(16 + index * 4), operations[index]);
        }
        engine.LoadField(74, script, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        for (int callback = 0; engine.PartyMenu?.Phase != NativePartyMenuPhase.Active && callback < 10; callback++)
        {
            engine.Step();
        }
        Assert.Equal(NativePartyMenuPhase.Active, engine.PartyMenu?.Phase);
        NativeShopTransaction shop = engine.PartyMenu!.Shop!;
        int itemId = Enumerable.Range(1, 198).First(id => shop.Counts[id] > 1);
        byte count = shop.Counts[itemId];
        uint gil = shop.Gil;
        string saved = JsonSerializer.Serialize(session.Economy);
        Assert.False(engine.Apply(new PlayerAction(PlayerActionKind.SellShopItem, itemId, Quantity: count + 1)).Accepted);
        Assert.True(engine.Apply(new PlayerAction(PlayerActionKind.SellShopItem, itemId, Quantity: 1)).Accepted);
        Assert.Equal(count - 1, engine.PartyMenu!.Shop!.Counts[itemId]);
        Assert.Equal(saved, JsonSerializer.Serialize(session.Economy));
        JsonElement checkpoint = engine.Capture();
        session.SetEventState(checkpoint);
        engine.Restore(checkpoint);
        Assert.True(engine.Apply(new PlayerAction(PlayerActionKind.Cancel)).Accepted);
        Assert.Equal(gil + shop.Definitions.SellPrice(itemId, shop.AbilityMask), session.Economy!.Gil);
        Assert.Equal(count - 1, session.Economy.Items.Single(stock => stock.ItemId == itemId).Count);
    }

    [Theory]
    [InlineData("shop-buy")]
    [InlineData("shop-sell")]
    public void ShopRowsShowOriginalIconsAndSelectionChangesTheHelpDescription(string screen)
    {
        using GameAssets assets = InstalledAssets();
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        NativeShopTransaction shop = OriginalShop(assets);
        shop = shop with { Counts = shop.Counts.SetItem(1, 6).SetItem(191, 2) };
        string original = JsonSerializer.Serialize(shop);
        MenuController controller = new(() => NativeShopMenuProjection.Build(shop, catalog));
        controller.Open(screen);
        Assert.True(controller.Entries.Count > 1);
        for (int selection = 0; selection < 2; selection++)
        {
            UiView view = controller.BuildView();
            MenuItem selected = controller.Data.Items.Single(item => item.Id == controller.SelectedEntry!.Id);
            int itemId = int.Parse(selected.Id.AsSpan(5), System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(catalog.ItemDescription(itemId), selected.Description);
            Assert.NotNull(selected.Icon);
            Assert.Equal(catalog.ItemIcon(itemId), selected.Icon);
            Assert.Contains(Descendants(view.Root).OfType<UiLabel>(), label => label.Text == selected.Description);
            UiList list = Assert.Single(Descendants(view.Root).OfType<UiList>(), element => element.Id == "commands");
            Assert.Equal(controller.Entries.Count, list.Images.Count);
            Assert.Equal(controller.Entries.Select(entry => controller.Data.Items.Single(item => item.Id == entry.Id).Icon), list.Images);
            Assert.Equal(selected.Icon, list.Images[controller.SelectedIndex]);
            Assert.Equal(controller.Entries.Count, list.SecondaryItems.Count);
            controller.Navigate(MenuInput.Down);
        }
        Assert.Equal(original, JsonSerializer.Serialize(shop));
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
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original shop and initialization resources are required.");
        return new GameAssets(directory);
    }
}
