using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class MenuSkinOverlapTests
{
    [Theory]
    [InlineData(1280, 720)]
    [InlineData(2560, 1080)]
    public void Vector_magic_targets_have_their_own_pane_clear_of_the_spell_stock(int width, int height)
    {
        MenuController menu = new();
        menu.Open("magic-target", demo: true);
        UiView view = menu.BuildView();
        view.Resize(width, height);
        UiList targets = Assert.Single(Descendants(view.Root).OfType<UiList>(), list => list.Id == "commands");
        UiList stock = Assert.Single(Descendants(view.Root).OfType<UiList>(), list => list.Id?.StartsWith("magic-preview-", StringComparison.Ordinal) == true);
        Assert.True(targets.Bounds.Right <= stock.Bounds.X);
        Assert.True(targets.Bounds.Height > 0);
        Assert.True(stock.Bounds.Height > 0);
        Assert.Equal(menu.SelectedIndex, targets.SelectedIndex);
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
