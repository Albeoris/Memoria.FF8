using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private bool IsNativeSecondaryScreen => !IsHostSettingsScreen && !UseHostControls && (IsConfigSettingsScreen || ScreenId.StartsWith("config-", StringComparison.Ordinal) || ScreenId is "save" or "save-confirm" or "load" or "load-confirm" or "seed-test" || ScreenId.StartsWith("shop", StringComparison.Ordinal) || ScreenId.StartsWith("ability", StringComparison.Ordinal) || ScreenId.StartsWith("tutorial", StringComparison.Ordinal));

    private UiElement BuildNativeSecondary()
    {
        if (IsConfigSettingsScreen)
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
        Place(header, Panel("INFO.", Label(info, 24, UiTextFlow.Wrap)), 0);
        Place(header, Panel(null, Label(title, 27, UiTextFlow.Wrap)), 0, 1);
        Place(header, Panel("HELP", Label(Message.Length > 0 ? Message : help, 23, UiTextFlow.Wrap)), 1, 0, 1, 2);
        return header;
    }

    private UiList SecondaryCommands(float height = 42, float size = 24)
    {
        return new UiList
        {
            TextFlow = UiTextFlow.SingleLine,
            Id = "commands",
            Paginated = true,
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
        bool newGame = ScreenId == "new-game-options";
        var page = Grid(ScreenId == "config-text" ? [1.7f * TextScale, 4, 3] : [1.4f * TextScale, newGame ? 3.5f : 6.85f], [1], 8);
        var header = Grid([1], [3.7f, 1.3f], 5);
        string help = Message.Length > 0 ? Message : SelectedEntry?.Help ?? CurrentScreen.Help;
        Place(header, Panel("HELP", new UiLabel { Text = help, FontSize = 18 * TextScale, TextFlow = UiTextFlow.Wrap }), 0);
        Place(header, Panel(null, Label(ScreenId == "new-game-options" ? "New Game" : SettingsPlacement.Pages.TryGetValue(ScreenId, out SettingsPage? pageDefinition) ? pageDefinition.Label : "Config", 27, UiTextFlow.Wrap)), 0, 1);
        Place(page, header, 0);
        int count = Math.Min(Math.Max(1, Entries.Count), ConfigRowsPerPage);
        int first = SelectedIndex / count * count;
        var content = Grid([1, 0.055f], [1], 4);
        content.RowDefinitions[1] = UiGridLength.Pixel(22 * TextScale);
        var rows = new UiGrid { Columns = 1, Gap = 0 };
        for (int index = 0; index < count; index++)
        {
            rows.RowDefinitions.Add(UiGridLength.Star(1));
        }
        for (int index = first; index < Math.Min(first + count, Entries.Count); index++)
        {
            Place(rows, BuildConfigRow(Entries[index], index), index - first);
        }
        Place(content, rows, 0);
        int pages = Math.Max(1, (Entries.Count + count - 1) / count);
        Place(content, Label($"{(ScreenId == "config" ? "Categories" : CurrentScreen.Title)}     {first / count + 1} / {pages}", 15), 1);
        Place(page, Panel(IsFixtureContext && !newGame ? "Preview / Fixture" : null, content), 1);
        if (ScreenId == "config-text")
        {
            Place(page, Panel("Menus / Dialogue preview", BuildFontPreviews()), 2);
        }
        if (newGame)
        {
            UiAnchorPanel modal = new();
            page.Anchors = new UiAnchors(0.08f, 0.12f, 0.92f, 0.88f);
            modal.Children.Add(page);
            return modal;
        }
        return page;
    }

    private UiElement BuildSecondarySave()
    {
        bool confirmation = ScreenId.EndsWith("-confirm", StringComparison.Ordinal);
        bool loading = ScreenId.StartsWith("load", StringComparison.Ordinal);
        var page = Grid([1.9f * Math.Max(1, TextScale), 6], [1], 5);
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
            Place(content, Label(slot?.Summary ?? "Slot metadata unavailable.", 26, UiTextFlow.Wrap), 1);
            card.Children.Add(content);
            Place(cards, card, index - first);
        }
        Place(page, cards, 1);
        return page;
    }

    private UiElement BuildSecondaryShop()
    {
        if (ScreenId == "shop-junk")
        {
            return BuildWeaponShop();
        }
        MenuItem? selected = Data.Items.FirstOrDefault(item => item.Id == SelectedEntry?.Id);
        var page = Grid([1.8f * Math.Max(1, TextScale), 5.8f, 0.65f], [3, 1.7f], 5);
        string help = ScreenId == "shop-buy" ? "Choose an item to purchase." : ScreenId == "shop-sell" ? "Choose an item to sell." : ScreenId == "shop-junk" ? "Select a weapon to remodel." : "Select a shop command.";
        Place(page, SecondaryHeader("Shop", ScreenId == "shop-buy" ? "Buy" : ScreenId == "shop-sell" ? "Sell" : "Shop", selected?.Description ?? help), 0, 0, 1, 2);
        UiList commands = SecondaryCommands();
        if (ScreenId is "shop-buy" or "shop-sell")
        {
            commands.Items = Entries.Select(entry => Data.Items.FirstOrDefault(item => item.Id == entry.Id)?.Name ?? entry.Label).ToList();
            commands.SecondaryItems = Entries.Select(entry => Data.Items.FirstOrDefault(item => item.Id == entry.Id) is { } item ? Number(ScreenId == "shop-sell" ? item.SalePrice ?? item.Price / 2 : item.Price) + " G" : "--").ToList();
            commands.Images = Entries.Select(entry => Data.Items.FirstOrDefault(item => item.Id == entry.Id)?.Icon).ToList();
            commands.AutoSecondaryWidth = true;
            Place(page, Panel("ITEM", commands), 1);
        }
        else
        {
            Place(page, Panel(null, commands), 1);
        }
        Place(page, Panel(null, Readouts([selected?.Name ?? "", "Owned", selected?.Quantity.ToString() ?? "--"], 25)), 1, 1);
        Place(page, Panel(null, Label(Number(Data.Currency) + " G", 24)), 2, 1);
        return page;
    }

    private UiElement BuildWeaponShop()
    {
        MenuItem? selected = Data.Items.FirstOrDefault(item => item.Id == SelectedEntry?.Id);
        MenuCharacter? character = Data.Characters.FirstOrDefault(value => value.InParty);
        var page = Grid([1.8f * Math.Max(1, TextScale), 5.8f, 0.65f], [3, 2.4f], 5);
        Place(page, SecondaryHeader("Junk Shop", character?.Name ?? "Select a character", selected?.Description ?? "Select a character or a weapon to remodel."), 0, 0, 1, 2);
        var choices = Grid([2, 3], [1], 5);
        int characterCount = Data.Characters.Count;
        UiList characters = SecondaryCommands(36, 23);
        characters.Id = "weapon-characters";
        characters.Items = Data.Characters.Select(value => value.Name).ToList();
        characters.EnabledItems = Entries.Take(characterCount).Select(entry => entry.Enabled).ToList();
        characters.SelectedIndex = SelectedIndex < characterCount ? SelectedIndex : Math.Max(0, Data.Characters.FindIndex(value => value.InParty));
        characters.ShowSelection = SelectedIndex < characterCount;
        Place(choices, Panel("CHARACTER", characters), 0);
        UiList weapons = SecondaryCommands();
        weapons.Items = Data.Items.Select(item => item.Name).ToList();
        weapons.EnabledItems = Entries.Skip(characterCount).Select(entry => entry.Enabled).ToList();
        weapons.SecondaryItems = Data.Items.Select(item => Number(item.Price) + " G").ToList();
        weapons.AutoSecondaryWidth = true;
        weapons.SelectedIndex = Math.Max(0, SelectedIndex - characterCount);
        weapons.ShowSelection = SelectedIndex >= characterCount;
        weapons.SelectionChanged = index => Select(index + characterCount, Entries.Count);
        weapons.Confirmed = index => Activate(index + characterCount);
        Place(choices, Panel("WEAPON", weapons), 1);
        Place(page, choices, 1);
        var materials = new UiList
        {
            Id = "weapon-materials",
            Focusable = false,
            Items = selected?.Materials.Select(material => material.Name).ToList() ?? [],
            Images = selected?.Materials.Select(material => material.Icon).ToList() ?? [],
            SecondaryItems = selected?.Materials.Select(material => $"{material.Owned} / {material.Required}").ToList() ?? [],
            AutoSecondaryWidth = true,
            ShowSelection = false,
            RowHeight = 42 * TextScale,
            FontSize = 23 * TextScale
        };
        var details = Grid([0.7f, 0.5f, 4], [1], 5);
        Place(details, Label(selected?.Name ?? character?.Name ?? "", 25), 0);
        Place(details, Label(selected is null ? "Select a weapon to view materials." : "Owned / Required", 19), 1);
        Place(details, materials, 2);
        Place(page, Panel("MATERIALS", details), 1, 1);
        Place(page, Panel(null, Label(Number(Data.Currency) + " G", 24)), 2, 1);
        return page;
    }

    private UiElement BuildSecondaryAbility()
    {
        var page = Grid([1.8f * Math.Max(1, TextScale), 5.8f], [1.35f, 1], 5);
        Place(page, SecondaryHeader("Ability", ScreenId == "ability" ? "Select an ability" : RefinementAbility, ScreenId == "ability" ? "Select a menu ability." : SelectedEntry?.Help ?? "Select an item to refine."), 0, 0, 1, 2);
        UiList commands = SecondaryCommands();
        if (ScreenId == "ability-recipes")
        {
            commands.Items = Entries.Select(entry => entry.Label).ToList();
            commands.SecondaryItems = Entries.Select(entry => Data.Recipes.First(recipe => recipe.Id == entry.Id)).Select(RefinementStock).ToList();
            commands.AutoSecondaryWidth = true;
            commands.SecondaryAlignment = UiAlignment.End;
        }
        Place(page, Panel(ScreenId == "ability" ? null : "ITEM", commands), 1);
        string[] details;
        if (ScreenId.StartsWith("ability-card", StringComparison.Ordinal))
        {
            details = [Card?.Name ?? "--", "Owned   " + (Card?.Quantity.ToString() ?? "--"), "Result", Card?.RefinementItemName ?? Data.Items.FirstOrDefault(item => item.Id == Card?.RefinementItemId)?.Name ?? "Unavailable", "Quantity   " + Number(Card?.RefinementQuantity)];
        }
        else if (HighlightedRefinementRecipe is { } recipe && ScreenId != "ability")
        {
            details = [recipe.IngredientName ?? Data.Items.FirstOrDefault(item => item.Id == recipe.IngredientId)?.Name ?? recipe.IngredientId, $"{recipe.IngredientQuantity}  →  {recipe.ResultQuantity} {recipe.ResultName}", "Owned   " + RefinementStock(recipe), .. (recipe.NativeKind is 0 or 2 ? new[] { Character?.Name ?? "--" } : Array.Empty<string>())];
        }
        else
        {
            details = [ScreenId == "ability" ? "Choose a refinement ability." : "No items to refine."];
        }
        Place(page, Panel(null, Lines(details, 24)), 1, 1);
        return page;
    }

    private UiElement BuildSecondaryReference()
    {
        bool controls = ScreenId.StartsWith("config-", StringComparison.Ordinal);
        bool practice = ScreenId is "tutorial-seed" or "seed-test";
        var page = Grid([1.8f * Math.Max(1, TextScale), 5.8f], [2, 3], 5);
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
            "tutorial-menu" => ["The menu contains character status, junctions, items and magic.", "Character pages show the selected party member.", "Status includes Limit Break information."],
            "tutorial-junction" => CurrentScreen.Details(this).Take(4).ToArray(),
            "tutorial-card" => CurrentScreen.Details(this).Take(4).ToArray(),
            _ => CurrentScreen.Details(this)
        };
    }
}
