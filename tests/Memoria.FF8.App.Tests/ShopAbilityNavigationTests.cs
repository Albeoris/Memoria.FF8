using System.Collections.Immutable;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ShopAbilityNavigationTests
{
    [Fact]
    public void LearnedRowsUseCatalogNamesHelpAndNativeAvailability()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        vitals.GuardianForces[0] = vitals.GuardianForces[0] with { Flags = 1, LearnedBank = new(0, 0b11101UL << (92 - 64)) };
        vitals.GuardianForces[1] = vitals.GuardianForces[1] with { Flags = 0, LearnedBank = new(0, 1UL << (93 - 64)) };
        NativeShopHistory empty = assets.ReadNewGameShopHistory() with { Bytes = new byte[400].ToImmutableArray() };
        GameState state = new() { CharacterVitals = vitals, ShopHistory = empty };
        MenuData data = new();
        NativeShopMenuProjection.AugmentAbilities(data, state, catalog);
        Assert.Equal(["ability-92", "ability-94", "ability-95", "ability-96"], data.ShopAbilities.Select(entry => entry.Id));
        Assert.Equal([false, false, false, true], data.ShopAbilities.Select(entry => entry.Enabled));
        Assert.Equal(catalog.AbilityName(95), data.ShopAbilities[2].Label);
        Assert.Equal(catalog.AbilityDescription(95), data.ShopAbilities[2].Help);
        Assert.Equal(catalog.ShopAbilityUnavailableReason(95), data.ShopAbilities[2].DisabledReason);
        Assert.Empty(data.CallableShops);

        data = new MenuData();
        NativeShopMenuProjection.AugmentAbilities(data, state with { ShopHistory = empty with { Bytes = empty.Bytes.SetItem(20 * 3 + 16, 1) } }, catalog);
        MenuEntry shop = Assert.Single(data.CallableShops);
        Assert.Equal(catalog.ShopName(3), shop.Label);
        Assert.Equal(new MenuAction("call-shop-open", "3"), shop.Action);
        Assert.True(data.ShopAbilities[2].Enabled);
        data.RefinementAbilities.Add(97, "T Mag-RF");
        MenuController controller = new(() => data);
        controller.Open("ability");
        Assert.Equal("T Mag-RF", controller.Entries.Last().Label);
    }

    [Fact]
    public void CallingShopReturnsToChosenShopAndAbility()
    {
        MenuData party = new();
        party.ShopAbilities.Add(new MenuEntry("ability-92", "Haggle", "Discount", Enabled: false));
        party.ShopAbilities.Add(new MenuEntry("ability-95", "Call Shop", "Call a visited shop", "ability-call-shop"));
        party.CallableShops.Add(new MenuEntry("0", "First shop", "", "shop", new MenuAction("call-shop-open", "0")));
        party.CallableShops.Add(new MenuEntry("2", "Second shop", "", "shop", new MenuAction("call-shop-open", "2")));
        party.SupportedActions.Add("call-shop-open:2");
        MenuData shop = new() { Currency = 500 };
        shop.SupportedActions.Add("ability-shop-close");
        shop.SupportedActions.Add("shop-buy:item-1");
        shop.Items.Add(new MenuItem { Id = "item-1", Name = "Potion", Price = 100 });
        MenuData current = party;
        List<MenuAction> actions = [];
        MenuController controller = new(() => current, action =>
        {
            actions.Add(action);
            current = action.Kind == "ability-shop-close" ? party : shop;
        });

        controller.Open("ability");
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal("Discount", controller.Message);
        Assert.Empty(actions);
        controller.Navigate(MenuInput.Down);
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal("ability-call-shop", controller.ScreenId);
        controller.Navigate(MenuInput.Down);
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal("shop", controller.ScreenId);
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal("shop-buy", controller.ScreenId);
        controller.Navigate(MenuInput.Confirm);
        controller.Back();
        Assert.Equal("shop", controller.ScreenId);
        controller.Back();
        Assert.Equal("ability-call-shop", controller.ScreenId);
        Assert.Equal("2", controller.SelectedEntry!.Id);
        controller.Back();
        Assert.Equal("ability-95", controller.SelectedEntry!.Id);
        Assert.Equal([new MenuAction("call-shop-open", "2"), new MenuAction("shop-buy", "item-1", Value: "1"), new MenuAction("ability-shop-close")], actions);
    }

    [Theory]
    [InlineData("close")]
    [InlineData("replace")]
    [InlineData("back")]
    public void JunkShopClosesTransactionOnce(string exit)
    {
        MenuData party = new();
        party.ShopAbilities.Add(new MenuEntry("ability-96", "Junk Shop", "", "shop-junk", new MenuAction("junk-shop-open")));
        party.SupportedActions.Add("junk-shop-open");
        MenuData shop = new();
        shop.SupportedActions.Add("ability-shop-close");
        MenuData current = party;
        List<MenuAction> actions = [];
        MenuController controller = new(() => current, action =>
        {
            actions.Add(action);
            current = action.Kind == "ability-shop-close" ? party : shop;
        });
        controller.Open("ability");
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal("shop-junk", controller.ScreenId);
        if (exit == "close")
        {
            controller.Close();
            Assert.False(controller.IsOpen);
        }
        else if (exit == "replace")
        {
            controller.Open("config", true);
            Assert.Equal("config", controller.ScreenId);
        }
        else
        {
            controller.Back();
            Assert.Equal("ability", controller.ScreenId);
        }
        Assert.Equal([new MenuAction("junk-shop-open"), new MenuAction("ability-shop-close")], actions);
    }
}
