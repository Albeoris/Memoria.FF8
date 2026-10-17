using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class NativeMenuBattleUiTests
{
    [Fact]
    public void InstalledMagicalLampKeepsOriginalFormationAndDirectInventoryRoute()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        NativeFieldItem lamp = Assert.IsType<NativeFieldItem>(catalog.FieldItem(168));
        Assert.Equal(new NativeFieldItem(14, 1, 43, 3), lamp);
        Assert.True(lamp.SupportsBattleUse);
        GameState state = State(assets, false, false);
        Assert.Equal((ushort)811, lamp.BeginBattle(state.CharacterVitals!, state.Economy!, 168, false).Encounter);
        MenuData data = LiveMenuData.Build(state, catalog: catalog);
        MenuItem projected = data.Items.Single(item => item.Id == "item-168");
        Assert.True(projected.NativeFieldUseSupported);
        Assert.True(projected.NativeFieldBattleUse);
        Assert.Equal(new UiImageSource(UiImageKind.Icon, 226), projected.Icon);
        List<MenuAction> requests = [];
        MenuController controller = new(() => data, requests.Add);
        controller.Open("item-use", false);
        int index = controller.Entries.ToList().FindIndex(entry => entry.Id == "item-168");
        Assert.True(index >= 0);
        MenuEntry entry = controller.Entries[index];
        Assert.True(entry.Enabled);
        Assert.Equal(new MenuAction("item-battle", "item-168"), entry.Action);
        for (int step = 0; step < index; step++)
        {
            controller.Navigate(MenuInput.Down);
        }
        UiList list = Assert.Single(Descendants(controller.BuildView().Root).OfType<UiList>(), element => element.Id == "commands");
        Assert.Equal(projected.Icon, list.Images[index]);
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal(new MenuAction("item-battle", "item-168"), Assert.Single(requests));
        Assert.Equal("item-use", controller.ScreenId);
        Assert.False(controller.IsSelectingCharacter);
        Assert.Equal(state.Economy!.Items.Single(stock => stock.ItemId == 168).Count, projected.Quantity);
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    public void MagicalLampEligibilityUsesSavedDreamAndVisibleTimerContext(bool dream, bool timerDisplay, bool allowed)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        MenuData data = LiveMenuData.Build(State(assets, dream, timerDisplay), catalog: catalog);
        MenuItem lamp = data.Items.Single(item => item.Id == "item-168");
        Assert.True(lamp.NativeFieldBattleUse);
        Assert.Equal(allowed, lamp.NativeFieldUseSupported);
        List<MenuAction> requests = [];
        MenuController controller = new(() => data, requests.Add);
        controller.Open("item-use", false);
        int index = controller.Entries.ToList().FindIndex(entry => entry.Id == lamp.Id);
        Assert.Equal(allowed, controller.Entries[index].Enabled);
        if (!allowed)
        {
            for (int step = 0; step < index; step++)
            {
                controller.Navigate(MenuInput.Down);
            }
            controller.Navigate(MenuInput.Confirm);
            Assert.Empty(requests);
            Assert.Equal("item-use", controller.ScreenId);
        }
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void FieldMenuUsesRetainedEntryTimerDisplayInsteadOfCurrentDisplay(bool beforeMenu, bool currentDisplay)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        GameState state = State(assets, false, currentDisplay) with
        {
            EventState = JsonSerializer.SerializeToElement(new { PartyMenu = new { TimerDisplayBeforeMenu = beforeMenu } })
        };
        MenuData data = LiveMenuData.Build(state, catalog: catalog);
        MenuItem lamp = data.Items.Single(item => item.Id == "item-168");
        Assert.Equal(!beforeMenu, lamp.NativeFieldUseSupported);
        MenuController controller = new(() => data, _ => { });
        controller.Open("item-use", false);
        Assert.Equal(!beforeMenu, controller.Entries.Single(entry => entry.Id == lamp.Id).Enabled);
    }

    private static GameState State(GameAssets assets, bool dream, bool timerDisplay)
    {
        return new GameState
        {
            Scene = SceneKind.Field,
            CharacterVitals = assets.ReadNewGameCharacterVitals() with { DreamEnabled = dream },
            Economy = assets.ReadNewGameEconomy().AddItem(168, 1, out _),
            ExamTimer = NativeExamTimerState.FromReachedSet(600) with { DisplayEnabled = timerDisplay }
        };
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
