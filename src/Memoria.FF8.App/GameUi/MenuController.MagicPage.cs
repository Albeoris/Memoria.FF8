using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private UiElement BuildMagicPage()
    {
        bool root = ScreenId == "magic";
        bool exchange = ScreenId.StartsWith("magic-exchange", StringComparison.Ordinal);
        var page = Grid([1.2f, 5], [1], 4);
        Place(page, NativeMenuHeader("Magic", root ? NativeCommandStrip() : InventoryNavigation(true)), 0);
        var body = Grid(exchange ? [1, 1] : [1], [1.05f, 1], exchange ? 24 : 4);
        Place(page, body, 1);
        Place(body, MagicCharacterSummary(Character, exchange), 0);
        bool chooseTarget = ScreenId is "magic-target" or "magic-exchange-target" or "magic-exchange-quantity";
        UiElement stock;
        if (root || chooseTarget || ScreenId == "magic-detail")
        {
            stock = MagicStockPreview(Character);
        }
        else
        {
            UiList list = Commands(38, 25);
            MenuMagic[] spells = Data.Magic.Where(value => value.CharacterId == Character?.Id).ToArray();
            ConfigureMagicColumns(list, spells);
            stock = InventoryQuantities(list, spells.Select(value => value.Name).ToList(), spells.Select(value => value.StockPresentation).ToList());
        }
        Place(body, Panel("MAGIC", stock), 0, 1);
        if (exchange)
        {
            MenuCharacter? recipient = Data.Characters.FirstOrDefault(value => value.Id == _exchangeId) ?? Data.Characters.FirstOrDefault(value => value.Id != Character?.Id);
            Place(body, MagicCharacterSummary(recipient, true), 1);
            Place(body, Panel(chooseTarget ? "CHOICE" : "MAGIC", chooseTarget ? Commands(38, 24) : MagicStockPreview(recipient)), 1, 1);
        }
        else if (ScreenId == "magic-target")
        {
            var overlay = new UiAnchorPanel();
            var content = new UiElement();
            content.Children.Add(page);
            overlay.Children.Add(content);
            var choices = Panel("NAME", Commands(48, 25));
            choices.Anchors = new UiAnchors(0.18f, 0.42f, 0.76f, 0.84f);
            overlay.Children.Add(choices);
            return overlay;
        }
        return page;
    }

    private UiElement MagicCharacterSummary(MenuCharacter? character, bool compact)
    {
        var summary = Grid([2, 3], [1], 10);
        var identity = Grid([1], [1, 2.8f], 10);
        identity.Margin = new UiThickness(16, 8);
        Place(identity, Portrait(character), 0);
        Place(identity, Lines([character?.Name ?? "--", $"Lv  {Number(character?.Level)}", "HP  " + (character is null ? "--" : Hp(character))], 26), 0, 1);
        Place(summary, identity, 0);
        return compact ? identity : summary;
    }

    private UiElement MagicStockPreview(MenuCharacter? character)
    {
        MenuMagic[] spells = Data.Magic.Where(value => value.CharacterId == character?.Id).ToArray();
        var list = new UiList { Id = "magic-preview-" + character?.Id, RowHeight = 38 * TextScale, FontSize = 25 * TextScale, ShowSelection = false, Focusable = true };
        ConfigureMagicColumns(list, spells);
        return InventoryQuantities(list, spells.Select(value => value.Name).ToList(), spells.Select(value => value.StockPresentation).ToList());
    }

    private void ConfigureMagicColumns(UiList list, MenuMagic[] spells)
    {
        list.Images = [];
        list.TrailingImages = spells.Select(value => Data.SharedMagicStock ? value.OwnerIcon : null).ToList();
        list.TrailingImagesBeforeSecondary = true;
    }

    private UiElement InventoryQuantities(UiList list, List<string> names, List<string> quantities)
    {
        list.Items = names;
        list.Paginated = true;
        list.SecondaryItems = quantities;
        list.AutoSecondaryWidth = true;
        list.SecondaryAlignment = UiAlignment.End;
        return list;
    }
}
