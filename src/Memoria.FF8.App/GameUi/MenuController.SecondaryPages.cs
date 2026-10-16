using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private bool IsNativeSecondaryScreen => !IsHostSettingsScreen && !UseHostControls && (ScreenId == "config" || ScreenId.StartsWith("config-", StringComparison.Ordinal) || ScreenId is "save" or "save-confirm" or "load" or "load-confirm" or "seed-test" || ScreenId.StartsWith("shop", StringComparison.Ordinal) || ScreenId.StartsWith("ability", StringComparison.Ordinal) || ScreenId.StartsWith("tutorial", StringComparison.Ordinal));

    private UiElement BuildNativeSecondary()
    {
        if (ScreenId == "config")
        {
            return BuildSecondaryConfig();
        }
        if (ScreenId is "save" or "load" or "save-confirm" or "load-confirm")
        {
            return BuildSecondarySave();
        }
        if (ScreenId.StartsWith("shop", StringComparison.Ordinal))
        {
            return BuildSecondaryShop();
        }
        if (ScreenId.StartsWith("ability", StringComparison.Ordinal))
        {
            return BuildSecondaryAbility();
        }
        return BuildSecondaryReference();
    }

    private UiElement SecondaryHeader(string title, string info, string help)
    {
        var header = Grid([1, 1.2f], [3.7f, 1.3f], 5);
        Place(header, Panel("INFO.", Label(info, 24)), 0);
        Place(header, Panel(null, Label(title, 27)), 0, 1);
        Place(header, Panel("HELP", Label(Message.Length > 0 ? Message : help, 23)), 1, 0, 1, 2);
        return header;
    }

    private UiList SecondaryCommands(float height = 42, float size = 24)
    {
        return new UiList
        {
            Id = "commands",
            Items = Entries.Select(entry => entry.Label).ToList(),
            EnabledItems = Entries.Select(entry => entry.Enabled).ToList(),
            RowHeight = height * TextScale,
            FontSize = size * TextScale,
            SelectedIndex = SelectedIndex,
            HighlightSelection = false,
            PointerSize = 20 * TextScale,
            TextInset = 35 * TextScale,
            SelectionChanged = index => Select(index, Entries.Count),
            Confirmed = Activate
        };
    }

    private UiElement BuildSecondaryConfig()
    {
        var page = Grid([0.85f, 7.4f], [1], 8);
        var header = Grid([1], [3.7f, 1.3f], 5);
        Place(header, Panel("HELP", Label(Message.Length > 0 ? Message : SelectedEntry is { } setting ? "Set " + setting.Id.ToLowerInvariant() : "Select a setting.", 23)), 0);
        Place(header, Panel(null, Label("Config", 27)), 0, 1);
        Place(page, header, 0);
        var settings = Grid([1], [1.05f, 1], 4);
        UiList commands = SecondaryCommands(43);
        commands.ShowScrollbar = false;
        commands.Items = Entries.Select(entry => MenuSettings.Definitions.Any(setting => setting.Id == entry.Id) ? entry.Id : entry.Label).ToList();
        Place(settings, commands, 0);
        var values = new UiList
        {
            Id = "setting-values",
            ShowSelection = false,
            Items = Entries.Select(entry => Data.Settings.GetValueOrDefault(entry.Id, "")).ToList(),
            RowHeight = commands.RowHeight,
            FontSize = commands.FontSize,
            SelectedIndex = SelectedIndex,
            SelectionChanged = index => Select(index, Entries.Count),
            Confirmed = Activate
        };
        Place(settings, values, 0, 1);
        Place(page, Panel(null, settings), 1);
        return page;
    }

    private UiElement BuildSecondarySave()
    {
        bool confirmation = ScreenId.EndsWith("-confirm", StringComparison.Ordinal);
        bool loading = ScreenId.StartsWith("load", StringComparison.Ordinal);
        var page = Grid([1.9f, 6], [1], 5);
        Place(page, SecondaryHeader(loading ? "Load" : "Save", confirmation ? SaveSlot?.Name ?? "Slot unavailable" : "Select a save block", confirmation ? loading ? "Load the selected slot?" : "Save to the selected slot?" : loading ? "Choose block to load" : "Choose block to save"), 0);
        if (confirmation)
        {
            var area = Grid([2, 1, 2], [1, 2, 1], 5);
            Place(area, Panel(null, Lines([SaveSlot?.Name ?? "Unavailable", SaveSlot?.Summary ?? "Slot metadata unavailable."], 24)), 0, 0, 1, 3);
            Place(area, Panel(null, SecondaryCommands()), 1, 1, 2);
            Place(page, area, 1);
            return page;
        }
        if (Data.SaveSlots.Count == 0 || ScreenId == "save" && !IsDemo)
        {
            Place(page, Panel(null, SecondaryCommands()), 1);
            return page;
        }
        var cards = Grid([1, 1, 1], [1], 3);
        int first = SelectedIndex / 3 * 3;
        for (int index = first; index < Math.Min(first + 3, Entries.Count); index++)
        {
            int selected = index;
            MenuEntry entry = Entries[index];
            MenuSaveSlot? slot = Data.SaveSlots.FirstOrDefault(candidate => candidate.Id == entry.Id);
            var card = new UiButton { Id = entry.Id, Click = () => Activate(selected), HighlightSelection = false };
            var content = Grid([0.7f, 1.3f], [1], 2);
            content.Margin = new UiThickness(35, 6, 12, 6);
            Place(content, Label(slot?.Name ?? entry.Label, 23), 0);
            Place(content, Label(slot?.Summary ?? "Slot metadata unavailable.", 26), 1);
            card.Children.Add(content);
            Place(cards, card, index - first);
        }
        Place(page, cards, 1);
        return page;
    }

    private UiElement BuildSecondaryShop()
    {
        MenuItem? selected = Data.Items.FirstOrDefault(item => item.Id == SelectedEntry?.Id);
        var page = Grid([1.8f, 5.8f, 0.65f], [3, 1.7f], 5);
        string help = ScreenId == "shop-buy" ? "Choose an item to purchase." : ScreenId == "shop-sell" ? "Choose an item to sell." : ScreenId == "shop-junk" ? "Select a weapon to remodel." : "Select a shop command.";
        Place(page, SecondaryHeader("Shop", ScreenId == "shop-buy" ? "Buy" : ScreenId == "shop-sell" ? "Sell" : "Shop", selected?.Description ?? help), 0, 0, 1, 2);
        UiList commands = SecondaryCommands();
        if (ScreenId is "shop-buy" or "shop-sell")
        {
            commands.Items = Entries.Select(entry => Data.Items.FirstOrDefault(item => item.Id == entry.Id)?.Name ?? entry.Label).ToList();
            var stock = Grid([1], [2, 1], 2);
            Place(stock, commands, 0);
            Place(stock, new UiList { ShowSelection = false, Items = Entries.Select(entry => Data.Items.FirstOrDefault(item => item.Id == entry.Id) is { } item ? Number(ScreenId == "shop-sell" ? item.Price / 2 : item.Price) + " G" : "--").ToList(), RowHeight = commands.RowHeight, FontSize = commands.FontSize, SelectedIndex = SelectedIndex, SelectionChanged = index => Select(index, Entries.Count), Confirmed = Activate }, 0, 1);
            Place(page, Panel("ITEM", stock), 1);
        }
        else
        {
            Place(page, Panel(null, commands), 1);
        }
        Place(page, Panel(null, Lines([selected?.Name ?? "", "Owned", selected?.Quantity.ToString() ?? "--"], 25)), 1, 1);
        Place(page, Panel(null, Label(Number(Data.Currency) + " G", 24)), 2, 1);
        return page;
    }

    private UiElement BuildSecondaryAbility()
    {
        var page = Grid([1.8f, 5.8f], [1.1f, 1], 5);
        Place(page, SecondaryHeader("Ability", ScreenId == "ability" ? "Select an ability" : Recipe?.Ability ?? "Select an item", ScreenId == "ability" ? "Select a menu ability." : "Select an item to refine."), 0, 0, 1, 2);
        UiList commands = SecondaryCommands();
        Place(page, Panel(ScreenId == "ability" ? null : "ITEM", commands), 1);
        string[] details;
        if (ScreenId.StartsWith("ability-card", StringComparison.Ordinal))
        {
            details = [Card?.Name ?? "--", "Owned   " + (Card?.Quantity.ToString() ?? "--"), "Result", Data.Items.FirstOrDefault(item => item.Id == Card?.RefinementItemId)?.Name ?? "Unavailable", "Quantity   " + Number(Card?.RefinementQuantity)];
        }
        else if (Recipe is { } recipe && ScreenId != "ability")
        {
            details = [Data.Items.FirstOrDefault(item => item.Id == recipe.IngredientId)?.Name ?? recipe.IngredientId, $"{recipe.IngredientQuantity}  →  {recipe.ResultQuantity} {recipe.ResultName}", Character?.Name ?? "--"];
        }
        else
        {
            details = ["Choose a refinement ability."];
        }
        Place(page, Panel(null, Lines(details, 24)), 1, 1);
        return page;
    }

    private UiElement BuildSecondaryReference()
    {
        bool controls = ScreenId.StartsWith("config-", StringComparison.Ordinal);
        bool practice = ScreenId is "tutorial-seed" or "seed-test";
        var page = Grid([1.8f, 5.8f], [2, 3], 5);
        string help = controls ? SelectedEntry?.Help ?? CurrentScreen.Help : practice ? "Practice questions. Results do not change your SeeD rank." : ScreenId == "tutorial" ? SelectedEntry?.Id switch
        {
            "tutorial-junction" => "Junction explanation",
            "tutorial-battle" => "Battle explanation",
            "tutorial-gf" => "Guardian Force explanation",
            "tutorial-card" => "Card game rules",
            "tutorial-menu" => "Menu controls",
            "tutorial-seed" => "Practice SeeD questions",
            _ => "Select a heading to search"
        } : CurrentScreen.Help;
        Place(page, SecondaryHeader(controls ? "Config" : "Tutorial", controls ? CurrentScreen.Title : practice ? "Practice test" : "Select a heading to search", help), 0, 0, 1, 2);
        UiList commands = SecondaryCommands();
        if (ScreenId == "tutorial-seed")
        {
            commands.Items = ["Start practice test"];
        }
        if (Entries.Count > 0)
        {
            Place(page, Panel(null, commands), 1);
        }
        IReadOnlyList<string> details = controls ? CurrentScreen.Details(this) : SecondaryReferenceText();
        if (details.Count > 0)
        {
            Place(page, Panel(null, Lines(details, 23)), 1, Entries.Count == 0 ? 0 : 1, 1, Entries.Count == 0 ? 2 : 1);
        }
        return page;
    }

    private IReadOnlyList<string> SecondaryReferenceText()
    {
        return ScreenId switch
        {
            "tutorial" => [],
            "tutorial-seed" => ["Practice test", "Ten questions", "SeeD rank   " + Number(Data.SeeDRank)],
            "seed-test" => TestQuestion >= 10 ? ["Practice completed", $"Score: {TestCorrect}/10"] : [$"Question {TestQuestion + 1} / 10", TestQuestion % 2 == 0 ? "Does Confirm select an entry?" : "Does Cancel increase your gil?"],
            "tutorial-menu" => ["Directional controls move the cursor.", "Confirm selects an entry.", "Cancel returns to the previous page."],
            "tutorial-junction" => CurrentScreen.Details(this).Take(4).ToArray(),
            "tutorial-card" => CurrentScreen.Details(this).Take(4).ToArray(),
            _ => CurrentScreen.Details(this)
        };
    }
}
