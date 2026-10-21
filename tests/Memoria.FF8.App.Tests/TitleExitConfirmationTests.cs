using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class TitleExitConfirmationTests
{
    [Theory]
    [InlineData(MenuInput.Confirm)]
    [InlineData(MenuInput.Cancel)]
    public void DefaultConfirmationAndCancelReturnToTitle(MenuInput input)
    {
        int exits = 0;
        MenuController menus = new();
        menus.ShowTitleExitConfirmation(() => exits++);

        Assert.Equal("no", menus.SelectedEntry?.Id);
        Assert.Equal("Do you wish to exit the game?", menus.CurrentScreen.Title);
        Assert.IsType<UiList>(menus.BuildView().FocusedElement);
        menus.Navigate(input);

        Assert.False(menus.IsOpen);
        Assert.Equal(0, exits);
        menus.Execute(new MenuAction("title-exit-confirm"));
        Assert.Equal(0, exits);
    }

    [Fact]
    public void OnlyExplicitYesExitsAndReopeningResetsSelection()
    {
        int exits = 0;
        MenuController menus = new();
        menus.ConnectPreferences(() => new UiPreferences { CursorMemory = true }, _ => { });
        menus.ShowTitleExitConfirmation(() => exits++);
        menus.SelectEntry("title-exit", "yes");
        menus.Navigate(MenuInput.Cancel);
        menus.ShowTitleExitConfirmation(() => exits++);
        Assert.Equal("no", menus.SelectedEntry?.Id);

        menus.Navigate(MenuInput.Down);
        Assert.Equal("yes", menus.SelectedEntry?.Id);
        Assert.Equal(0, exits);
        menus.Navigate(MenuInput.Confirm);

        Assert.Equal(1, exits);
        Assert.False(menus.IsOpen);
        menus.Execute(new MenuAction("title-exit-confirm"));
        Assert.Equal(1, exits);
    }
}
