using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class MenuSpatialNavigationTests
{
    [Theory]
    [InlineData(MenuInput.Down, "below")]
    [InlineData(MenuInput.Right, "beside")]
    public void CustomLayoutsUseTheirControlGeometryInsteadOfTheEntrySequence(MenuInput input, string expected)
    {
        List<string> activations = [];
        MenuController menu = new();
        menu.Register(new MenuScreenDefinition
        {
            Id = "spatial-layout",
            Title = "Spatial layout",
            Help = "Choose a control",
            Entries = _ => [new("origin", "Origin", ""), new("beside", "Beside", ""), new("below", "Below", "")],
            BuildContent = (_, root) =>
            {
                UiGrid grid = new() { Columns = 2, Rows = 2, Gap = 12 };
                grid.Children.Add(new UiButton { Id = "origin", Text = "Origin", Click = () => activations.Add("origin") });
                grid.Children.Add(new UiButton { Id = "below", Text = "Below", GridRow = 1, Click = () => activations.Add("below") });
                grid.Children.Add(new UiButton { Id = "beside", Text = "Beside", GridColumn = 1, Click = () => activations.Add("beside") });
                root.Children.Add(grid);
            }
        });
        menu.Open("spatial-layout");
        Assert.Equal("origin", menu.BuildView().FocusedElement?.Id);
        menu.Navigate(input);
        Assert.Equal(expected, menu.BuildView().FocusedElement?.Id);
        Assert.Equal(expected, menu.SelectedEntry?.Id);
        menu.Navigate(MenuInput.Confirm);
        Assert.Equal(new[] { expected }, activations);
    }

    [Fact]
    public void GuardianTilesKeepTheSelectedEntryAndFocusTogether()
    {
        MenuController menu = new();
        menu.Open("gf", demo: true);
        UiView view = menu.BuildView();
        UiButton first = Assert.IsType<UiButton>(view.FocusedElement);
        menu.Navigate(MenuInput.Right);
        UiButton next = Assert.IsType<UiButton>(menu.BuildView().FocusedElement);
        Assert.NotEqual(first.Id, next.Id);
        Assert.Equal(next.Id, menu.SelectedEntry?.Id);
        menu.Navigate(MenuInput.Left);
        Assert.Equal(first.Id, menu.BuildView().FocusedElement?.Id);
        Assert.Equal(first.Id, menu.SelectedEntry?.Id);
    }
}
