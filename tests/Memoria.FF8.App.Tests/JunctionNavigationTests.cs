using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class JunctionNavigationTests
{
    [Fact]
    public void SlotPagesMoveInNativeOrderAndRetainTheirSelections()
    {
        MenuController menus = new();
        menus.Open("junction-magic", demo: true);
        menus.Navigate(MenuInput.Down);
        menus.Navigate(MenuInput.Left);
        Assert.Equal("junction-element", menus.ScreenId);
        menus.Navigate(MenuInput.Down);
        menus.Navigate(MenuInput.Down);
        menus.Navigate(MenuInput.Left);
        Assert.Equal("junction-status", menus.ScreenId);
        menus.Navigate(MenuInput.Down);
        menus.Navigate(MenuInput.Left);
        Assert.Equal("junction-status", menus.ScreenId);
        Assert.Equal(1, menus.SelectedIndex);
        menus.Navigate(MenuInput.Right);
        Assert.Equal("junction-element", menus.ScreenId);
        Assert.Equal(2, menus.SelectedIndex);
        menus.Navigate(MenuInput.Right);
        Assert.Equal("junction-magic", menus.ScreenId);
        Assert.Equal(1, menus.SelectedIndex);
        menus.Navigate(MenuInput.Right);
        Assert.Equal("junction-magic", menus.ScreenId);
        Assert.Equal(1, menus.SelectedIndex);
    }

    [Theory]
    [InlineData("junction-magic", MenuInput.Left)]
    [InlineData("junction-element", MenuInput.Left)]
    [InlineData("junction-status", MenuInput.Right)]
    public void SwitchingPagesPreservesCancelParentWithoutAddingHistory(string screen, MenuInput direction)
    {
        MenuController menus = new();
        menus.Open("junction-options", demo: true);
        menus.Push(screen);
        menus.Navigate(direction);
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal("junction-stock", menus.ScreenId);
        menus.Navigate(MenuInput.Cancel);
        Assert.NotEqual(screen, menus.ScreenId);
        menus.Navigate(MenuInput.Cancel);
        Assert.Equal("junction-options", menus.ScreenId);
        menus.Navigate(MenuInput.Cancel);
        Assert.False(menus.IsOpen);
    }

    [Theory]
    [InlineData("junction-magic")]
    [InlineData("junction-element")]
    [InlineData("junction-status")]
    [InlineData("junction-stock")]
    public void ShoulderInputsStillSwitchCharacterOnEveryMagicJunctionPage(string screen)
    {
        MenuController menus = new();
        menus.Open(screen, demo: true);
        string character = menus.Character!.Id;
        menus.Navigate(MenuInput.PageRight);
        Assert.Equal(screen, menus.ScreenId);
        Assert.NotEqual(character, menus.Character!.Id);
        menus.Navigate(MenuInput.PageLeft);
        Assert.Equal(character, menus.Character!.Id);
    }

    [Fact]
    public void StockArrowsKeepPagingMagicInsteadOfSwitchingSlotScreens()
    {
        MenuController menus = new();
        menus.Open("junction-magic", demo: true);
        string character = menus.Character!.Id;
        menus.Data.Magic.Clear();
        for (int index = 0; index < 32; index++)
        {
            menus.Data.Magic.Add(new MenuMagic { Id = index.ToString(), Name = $"Magic {index}", CharacterId = character, Quantity = 100 });
        }
        menus.Navigate(MenuInput.Confirm);
        UiView view = menus.BuildView();
        view.Resize(960, 672);
        UiList stock = Assert.IsType<UiList>(view.FocusedElement);
        int pageSize = stock.EffectivePageSize;
        menus.Navigate(MenuInput.Right);
        Assert.Equal("junction-stock", menus.ScreenId);
        Assert.Equal(pageSize, menus.SelectedIndex);
        view = menus.BuildView();
        view.Resize(960, 672);
        stock = Assert.IsType<UiList>(view.FocusedElement);
        Assert.Equal(1, stock.CurrentPage);
        menus.Navigate(MenuInput.Left);
        Assert.Equal("junction-stock", menus.ScreenId);
        Assert.Equal(0, menus.SelectedIndex);
        Assert.Equal(character, menus.Character!.Id);
    }
}
