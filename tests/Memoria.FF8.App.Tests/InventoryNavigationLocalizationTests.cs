using Memoria.FF8.GameUi;
using Memoria.FF8.Resources.Localization;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class InventoryNavigationLocalizationTests
{
    [Theory]
    [InlineData(MenuPresentation.Classic)]
    [InlineData(MenuPresentation.Vector)]
    public void MagicCommandsKeepCatalogLabelsAndAvailabilityAfterEnteringUse(MenuPresentation presentation)
    {
        MenuData data = new() { SharedMagicStock = true };
        data.Characters.Add(new MenuCharacter { Id = "zell", Name = "Zell", InParty = true });
        data.Magic.Add(new MenuMagic { Id = "z", CharacterId = "zell", Name = "Zeta", Quantity = 1 });
        data.Magic.Add(new MenuMagic { Id = "a", CharacterId = "zell", Name = "Alpha", Quantity = 1 });
        MenuController menu = new(() => data) { Presentation = presentation };
        Dictionary<string, string> translations = new()
        {
            [SystemTextCatalog.Key("ui.register.entry.help.use")] = "Использовать",
            [SystemTextCatalog.Key("ui.register.entry.help.stock")] = "В запас",
            [SystemTextCatalog.Key("ui.register.entry.label.exchange")] = "Обмен",
            [SystemTextCatalog.Key("ui.register.entry.help.rearrange")] = "Переставить"
        };
        menu.ConnectLocalizationText((key, original) => translations.GetValueOrDefault(key, original));
        menu.Open("magic");
        UiList rootStrip = Assert.IsType<UiList>(menu.BuildView().FocusedElement);
        Assert.Equal(new[] { "Использовать", "В запас", "Обмен", "Переставить" }, rootStrip.Items);

        menu.Navigate(MenuInput.Confirm);
        Assert.Equal("magic-use", menu.ScreenId);
        UiView view = menu.BuildView();
        Assert.Equal("commands", view.FocusedElement?.Id);
        UiButton[] buttons = Descendants(view.Root).OfType<UiButton>().ToArray();
        Assert.Equal(rootStrip.Items, buttons.Select(button => button.Text));
        Assert.Equal(rootStrip.EnabledItems, buttons.Select(button => button.Enabled));

        UiButton exchange = buttons[2];
        view.Pointer(exchange.Bounds.X + exchange.Bounds.Width / 2, exchange.Bounds.Y + exchange.Bounds.Height / 2, true);
        Assert.Equal("magic-use", menu.ScreenId);
        UiButton rearrange = buttons[3];
        view.Pointer(rearrange.Bounds.X + rearrange.Bounds.Width / 2, rearrange.Bounds.Y + rearrange.Bounds.Height / 2, false);
        view.Pointer(rearrange.Bounds.X + rearrange.Bounds.Width / 2, rearrange.Bounds.Y + rearrange.Bounds.Height / 2, true);
        Assert.Equal("magic-use", menu.ScreenId);
        Assert.Equal(new[] { "Alpha", "Zeta" }, data.Magic.Select(spell => spell.Name));

        menu.Open("magic-target");
        Assert.Equal(rootStrip.Items, Descendants(menu.BuildView().Root).OfType<UiButton>().Select(button => button.Text));
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
