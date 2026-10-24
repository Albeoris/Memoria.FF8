using Memoria.FF8.Runtime;
using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class InventorySortNavigationTests
{
    [Theory]
    [InlineData("item-use", "commands")]
    [InlineData("item", "inventory-preview")]
    public void SortDispatchesLiveActionAndRefreshesInventoryOnCurrentPage(string screen, string inventoryId)
    {
        MenuData original = CreateInventory("Zeta", "Alpha");
        MenuData sorted = CreateInventory("Alpha", "Zeta");
        MenuData current = original;
        List<MenuAction> requests = [];
        MenuController controller = new(() => current, action =>
        {
            requests.Add(action);
            current = sorted;
        });
        controller.Open(screen);
        UiView view = controller.BuildView();
        view.Resize(960, 672);
        Assert.Equal(new[] { "Zeta", "Alpha" }, Inventory(view, inventoryId).Items);

        if (screen == "item-use")
        {
            UiButton sort = Assert.Single(Descendants(view.Root).OfType<UiButton>(), button => button.Text == "Sort");
            view.Pointer(sort.Bounds.X + sort.Bounds.Width / 2, sort.Bounds.Y + sort.Bounds.Height / 2, true);
        }
        else
        {
            controller.Navigate(MenuInput.Right);
            controller.Navigate(MenuInput.Right);
            Assert.Equal("sort", controller.SelectedEntry?.Id);
            controller.Navigate(MenuInput.Confirm);
        }

        Assert.Equal("item-sort", Assert.Single(requests).Kind);
        Assert.Equal(screen, controller.ScreenId);
        Assert.Same(sorted, controller.Data);
        Assert.Empty(controller.Message);
        Assert.Equal(new[] { "Alpha", "Zeta" }, Inventory(controller.BuildView(), inventoryId).Items);
    }

    [Fact]
    public void LiveProjectionKeepsSavedOrderUntilExplicitSort()
    {
        NativeItemStock[] items = Enumerable.Repeat(new NativeItemStock(0, 0), NativeEconomy.ItemSlotCount).ToArray();
        items[0] = new(190, 2);
        items[2] = new(7, 1);
        NativeEconomy economy = new()
        {
            Version = 1,
            Rule = NativeEconomy.InitializationRule,
            InitOutSha256 = new string('A', 64),
            Items = items,
            Gil = 0,
            PastGil = 0,
            EventBankGil72 = 0,
            EventBankPastGil76 = 0
        };
        GameState state = new() { Scene = SceneKind.Field, Economy = economy };

        MenuData before = LiveMenuData.Build(state);
        MenuData after = LiveMenuData.Build(state with { Economy = economy.SortItems() });

        Assert.Contains("item-sort", before.SupportedActions);
        Assert.Equal(new[] { "item-190", "item-7" }, before.Items.Select(item => item.Id));
        Assert.Equal(new[] { "item-7", "item-190" }, after.Items.Select(item => item.Id));
        Assert.Equal(new[] { 1, 2 }, after.Items.Select(item => item.Quantity));
        Assert.Equal((byte)190, state.Economy.Items[0].ItemId);
    }

    private static MenuData CreateInventory(params string[] names)
    {
        MenuData data = new();
        data.SupportedActions.Add("item-sort");
        foreach (string name in names)
        {
            data.Items.Add(new MenuItem { Id = name, Name = name, Quantity = 1 });
        }
        return data;
    }

    private static UiList Inventory(UiView view, string id)
    {
        return Assert.Single(Descendants(view.Root).OfType<UiList>(), list => list.Id == id);
    }

    private static IEnumerable<UiElement> Descendants(UiElement element)
    {
        yield return element;
        foreach (UiElement child in element.Children)
        {
            foreach (UiElement descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }
}
