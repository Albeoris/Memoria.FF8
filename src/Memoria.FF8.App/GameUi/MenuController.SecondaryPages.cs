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
        Place(header, Panel(SystemText("ui.secondaryheader.panel.info", "INFO."), Label(info, 24, UiTextFlow.Wrap)), 0);
        Place(header, Panel(null, Label(title, 27, UiTextFlow.Wrap)), 0, 1);
        Place(header, Panel(SystemText("ui.secondaryheader.panel.help", "HELP"), Label(Message.Length > 0 ? Message : help, 23, UiTextFlow.Wrap)), 1, 0, 1, 2);
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
        Place(header, Panel(SystemText("ui.buildsecondaryconfig.panel.help", "HELP"), new UiLabel { Text = help, FontSize = 18 * TextScale, TextFlow = UiTextFlow.Wrap }), 0);
        Place(header, Panel(null, Label(ScreenId == "new-game-options" ? SystemText("ui.buildsecondaryconfig.label.new.game", "New Game") : SettingsPlacement.Pages.TryGetValue(ScreenId, out SettingsPage? pageDefinition) ? pageDefinition.Label : SystemText("ui.buildsecondaryconfig.label.config", "Config"), 27, UiTextFlow.Wrap)), 0, 1);
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
        Place(page, Panel(IsFixtureContext && !newGame ? SystemText("ui.buildsecondaryconfig.panel.preview.fixture", "Preview / Fixture") : null, content), 1);
        if (ScreenId == "config-text")
        {
            Place(page, Panel(SystemText("ui.buildsecondaryconfig.panel.menus.dialogue.preview", "Menus / Dialogue preview"), BuildFontPreviews()), 2);
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
        Place(page, SecondaryHeader(loading ? SystemText("ui.buildsecondarysave.header.load", "Load") : SystemText("ui.buildsecondarysave.header.save", "Save"), confirmation ? SaveSlot?.Name ?? SystemText("ui.buildsecondarysave.header.slot.unavailable", "Slot unavailable") : SystemText("ui.buildsecondarysave.header.select.a.save.block", "Select a save block"), confirmation ? loading ? SystemText("ui.buildsecondarysave.header.load.the.selected.slot", "Load the selected slot?") : SystemText("ui.buildsecondarysave.header.save.to.the.selected.slot", "Save to the selected slot?") : loading ? SystemText("ui.buildsecondarysave.header.choose.block.to.load", "Choose block to load") : SystemText("ui.buildsecondarysave.header.choose.block.to.save", "Choose block to save")), 0);
        if (confirmation)
        {
            var area = Grid([2, 1, 2], [1, 2, 1], 5);
            Place(area, Panel(null, Lines([SaveSlot?.Name ?? SystemText("ui.buildsecondarysave.details.unavailable", "Unavailable"), SaveSlot?.Summary ?? SystemText("ui.buildsecondarysave.details.slot.metadata.unavailable", "Slot metadata unavailable.")], 24)), 0, 0, 1, 3);
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
            Place(content, Label(slot?.Summary ?? SystemText("ui.buildsecondarysave.label.slot.metadata.unavailable", "Slot metadata unavailable."), 26, UiTextFlow.Wrap), 1);
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
        string help = ScreenId == "shop-buy" ? SystemText("ui.buildsecondaryshop.help.choose.an.item.to.purchase", "Choose an item to purchase.") : ScreenId == "shop-sell" ? SystemText("ui.buildsecondaryshop.help.choose.an.item.to.sell", "Choose an item to sell.") : ScreenId == "shop-junk" ? SystemText("ui.buildsecondaryshop.help.select.a.weapon.to.remodel", "Select a weapon to remodel.") : SystemText("ui.buildsecondaryshop.help.select.a.shop.command", "Select a shop command.");
        Place(page, SecondaryHeader(SystemText("ui.buildsecondaryshop.header.shop", "Shop"), ScreenId == "shop-buy" ? SystemText("ui.buildsecondaryshop.header.buy", "Buy") : ScreenId == "shop-sell" ? SystemText("ui.buildsecondaryshop.header.sell", "Sell") : SystemText("ui.buildsecondaryshop.header.shop", "Shop"), selected?.Description ?? help), 0, 0, 1, 2);
        UiList commands = SecondaryCommands();
        if (ScreenId is "shop-buy" or "shop-sell")
        {
            commands.Items = Entries.Select(entry => Data.Items.FirstOrDefault(item => item.Id == entry.Id)?.Name ?? entry.Label).ToList();
            commands.SecondaryItems = Entries.Select(entry => Data.Items.FirstOrDefault(item => item.Id == entry.Id) is { } item ? Number(ScreenId == "shop-sell" ? item.SalePrice ?? item.Price / 2 : item.Price) + " G" : "--").ToList();
            commands.Images = Entries.Select(entry => Data.Items.FirstOrDefault(item => item.Id == entry.Id)?.Icon).ToList();
            commands.AutoSecondaryWidth = true;
            Place(page, Panel(SystemText("ui.buildsecondaryshop.panel.item", "ITEM"), commands), 1);
        }
        else
        {
            Place(page, Panel(null, commands), 1);
        }
        Place(page, Panel(null, Readouts([selected?.Name ?? "", SystemText("ui.buildsecondaryshop.details.owned", "Owned"), selected?.Quantity.ToString() ?? "--"], 25)), 1, 1);
        Place(page, Panel(null, Label(FormatSystemText("ui.buildsecondaryshop.label.g.template", "{0} G", Number(Data.Currency)), 24)), 2, 1);
        return page;
    }

    private UiElement BuildWeaponShop()
    {
        MenuItem? selected = Data.Items.FirstOrDefault(item => item.Id == SelectedEntry?.Id);
        MenuCharacter? character = Data.Characters.FirstOrDefault(value => value.InParty);
        var page = Grid([1.8f * Math.Max(1, TextScale), 5.8f, 0.65f], [3, 2.4f], 5);
        Place(page, SecondaryHeader(SystemText("ui.buildweaponshop.header.junk.shop", "Junk Shop"), character?.Name ?? SystemText("ui.buildweaponshop.header.select.a.character", "Select a character"), selected?.Description ?? SystemText("ui.buildweaponshop.header.select.a.character.or.a.weapon.to.remodel", "Select a character or a weapon to remodel.")), 0, 0, 1, 2);
        var choices = Grid([2, 3], [1], 5);
        int characterCount = Data.Characters.Count;
        UiList characters = SecondaryCommands(36, 23);
        characters.Id = "weapon-characters";
        characters.Items = Data.Characters.Select(value => value.Name).ToList();
        characters.EnabledItems = Entries.Take(characterCount).Select(entry => entry.Enabled).ToList();
        characters.SelectedIndex = SelectedIndex < characterCount ? SelectedIndex : Math.Max(0, Data.Characters.FindIndex(value => value.InParty));
        characters.ShowSelection = SelectedIndex < characterCount;
        Place(choices, Panel(SystemText("ui.buildweaponshop.panel.character", "CHARACTER"), characters), 0);
        UiList weapons = SecondaryCommands();
        weapons.Items = Data.Items.Select(item => item.Name).ToList();
        weapons.EnabledItems = Entries.Skip(characterCount).Select(entry => entry.Enabled).ToList();
        weapons.SecondaryItems = Data.Items.Select(item => Number(item.Price) + " G").ToList();
        weapons.AutoSecondaryWidth = true;
        weapons.SelectedIndex = Math.Max(0, SelectedIndex - characterCount);
        weapons.ShowSelection = SelectedIndex >= characterCount;
        weapons.SelectionChanged = index => Select(index + characterCount, Entries.Count);
        weapons.Confirmed = index => Activate(index + characterCount);
        Place(choices, Panel(SystemText("ui.buildweaponshop.panel.weapon", "WEAPON"), weapons), 1);
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
        Place(details, Label(selected is null ? SystemText("ui.buildweaponshop.label.select.a.weapon.to.view.materials", "Select a weapon to view materials.") : SystemText("ui.buildweaponshop.label.owned.required", "Owned / Required"), 19), 1);
        Place(details, materials, 2);
        Place(page, Panel(SystemText("ui.buildweaponshop.panel.materials", "MATERIALS"), details), 1, 1);
        Place(page, Panel(null, Label(FormatSystemText("ui.buildweaponshop.label.g.template", "{0} G", Number(Data.Currency)), 24)), 2, 1);
        return page;
    }

    private UiElement BuildSecondaryAbility()
    {
        var page = Grid([1.8f * Math.Max(1, TextScale), 5.8f], [1.35f, 1], 5);
        Place(page, SecondaryHeader(SystemText("ui.buildsecondaryability.header.ability", "Ability"), ScreenId == "ability" ? SystemText("ui.buildsecondaryability.header.select.an.ability", "Select an ability") : RefinementAbility, ScreenId == "ability" ? SystemText("ui.buildsecondaryability.header.select.a.menu.ability", "Select a menu ability.") : SelectedEntry?.Help ?? SystemText("ui.buildsecondaryability.header.select.an.item.to.refine", "Select an item to refine.")), 0, 0, 1, 2);
        UiList commands = SecondaryCommands();
        if (ScreenId == "ability")
        {
            commands.Images = Entries.Select(_ => NativeAbilityMenuMetadata.Icon(92)).ToList();
        }
        if (ScreenId == "ability-recipes")
        {
            commands.Images = Entries.Select(entry => Data.Recipes.First(recipe => recipe.Id == entry.Id)).Select(recipe => recipe.IngredientIcon ?? Data.Items.FirstOrDefault(item => item.Id == recipe.IngredientId)?.Icon).ToList();
            commands.Items = Entries.Select(entry => entry.Label).ToList();
            commands.SecondaryItems = Entries.Select(entry => Data.Recipes.First(recipe => recipe.Id == entry.Id)).Select(RefinementStock).ToList();
            commands.AutoSecondaryWidth = true;
            commands.SecondaryAlignment = UiAlignment.End;
        }
        Place(page, Panel(ScreenId == "ability" ? null : SystemText("ui.buildsecondaryability.panel.item", "ITEM"), commands), 1);
        string[] details;
        if (ScreenId.StartsWith("ability-card", StringComparison.Ordinal))
        {
            details = [Card?.Name ?? "--", SystemText("refinement.summary.owned", "Owned   ") + (Card?.Quantity.ToString() ?? "--"), "Result", Card?.RefinementItemName ?? Data.Items.FirstOrDefault(item => item.Id == Card?.RefinementItemId)?.Name ?? "Unavailable", SystemText("refinement.summary.quantity", "Quantity   ") + Number(Card?.RefinementQuantity)];
        }
        else if (HighlightedRefinementRecipe is { } recipe && ScreenId != "ability")
        {
            details = [recipe.IngredientName ?? Data.Items.FirstOrDefault(item => item.Id == recipe.IngredientId)?.Name ?? recipe.IngredientId, $"{recipe.IngredientQuantity}  →  {recipe.ResultQuantity} {recipe.ResultName}", SystemText("refinement.summary.owned", "Owned   ") + RefinementResultStock(recipe).ToString(System.Globalization.CultureInfo.InvariantCulture), .. RefinementResultOwners(recipe)];
        }
        else
        {
            details = [ScreenId == "ability" ? SystemText("refinement.prompt.ability", "Choose a refinement ability.") : SystemText("refinement.prompt.empty", "No items to refine.")];
        }
        Place(page, Panel(null, RefinementDetailsView(details)), 1, 1);
        return page;
    }

    private UiElement RefinementDetailsView(IReadOnlyList<string> details)
    {
        var rows = new UiGrid { Columns = 1, Rows = details.Count + 1 };
        for (int index = 0; index < details.Count; index++)
        {
            rows.RowDefinitions.Add(UiGridLength.Auto);
            UiImageSource? icon = HighlightedRefinementRecipe is { } recipe ? index switch
            {
                0 => recipe.IngredientIcon ?? Data.Items.FirstOrDefault(item => item.Id == recipe.IngredientId)?.Icon,
                1 => recipe.ResultIcon ?? Data.Items.FirstOrDefault(item => item.Id == recipe.ResultId)?.Icon,
                _ => null
            } : null;
            UiElement content = Label(details[index], 24, UiTextFlow.Wrap);
            if (icon is not null)
            {
                var line = new UiGrid { Columns = 2, Rows = 1 };
                line.ColumnDefinitions.Add(UiGridLength.Pixel(28 * TextScale));
                line.ColumnDefinitions.Add(UiGridLength.Star());
                Place(line, new UiImage { Source = icon, Width = 24 * TextScale, Height = 24 * TextScale, VerticalAlignment = UiAlignment.Center }, 0);
                Place(line, content, 0, 1);
                content = line;
            }
            Place(rows, content, index);
        }
        rows.RowDefinitions.Add(UiGridLength.Star());
        return rows;
    }

    private UiElement BuildSecondaryReference()
    {
        bool controls = ScreenId.StartsWith("config-", StringComparison.Ordinal);
        bool practice = ScreenId is "tutorial-seed" or "seed-test";
        var page = Grid([1.8f * Math.Max(1, TextScale), 5.8f], [2, 3], 5);
        string help = controls ? SelectedEntry?.Help ?? CurrentScreen.Help : practice ? SystemText("ui.buildsecondaryreference.help.practice.questions.results.do.not.change.your.seed", "Practice questions. Results do not change your SeeD rank.") : ScreenId == "tutorial" ? SelectedEntry?.Id switch
        {
            "tutorial-junction" => SystemText("ui.buildsecondaryreference.help.junction.explanation", "Junction explanation"),
            "tutorial-battle" => SystemText("ui.buildsecondaryreference.help.battle.explanation", "Battle explanation"),
            "tutorial-gf" => SystemText("ui.buildsecondaryreference.help.guardian.force.explanation", "Guardian Force explanation"),
            "tutorial-card" => SystemText("ui.buildsecondaryreference.help.card.game.rules", "Card game rules"),
            "tutorial-menu" => SystemText("ui.buildsecondaryreference.help.menu.controls", "Menu controls"),
            "tutorial-seed" => SystemText("ui.buildsecondaryreference.help.practice.seed.questions", "Practice SeeD questions"),
            _ => SystemText("ui.buildsecondaryreference.help.select.a.heading.to.search", "Select a heading to search")
        } : CurrentScreen.Help;
        Place(page, SecondaryHeader(controls ? SystemText("ui.buildsecondaryreference.header.config", "Config") : SystemText("ui.buildsecondaryreference.header.tutorial", "Tutorial"), controls ? CurrentScreen.Title : practice ? SystemText("ui.buildsecondaryreference.header.practice.test", "Practice test") : SystemText("ui.buildsecondaryreference.header.select.a.heading.to.search", "Select a heading to search"), help), 0, 0, 1, 2);
        UiList commands = SecondaryCommands();
        if (ScreenId == "tutorial-seed")
        {
            commands.Items = [SystemText("ui.buildsecondaryreference.items.start.practice.test", "Start practice test")];
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
            "tutorial-seed" => [SystemText("ui.secondaryreferencetext.details.practice.test", "Practice test"), SystemText("ui.secondaryreferencetext.details.ten.questions", "Ten questions"), FormatSystemText("ui.secondaryreferencetext.details.seed.rank.template", "SeeD rank   {0}", Number(Data.SeeDRank))],
            "seed-test" => TestQuestion >= 10 ? [SystemText("ui.secondaryreferencetext.details.practice.completed", "Practice completed"), FormatSystemText("ui.secondaryreferencetext.details.score.10", "Score: {0}/10", TestCorrect)] : [FormatSystemText("ui.secondaryreferencetext.details.question.10", "Question {0} / 10", TestQuestion + 1), TestQuestion % 2 == 0 ? SystemText("ui.secondaryreferencetext.details.does.confirm.select.an.entry", "Does Confirm select an entry?") : SystemText("ui.secondaryreferencetext.details.does.cancel.increase.your.gil", "Does Cancel increase your gil?")],
            "tutorial-menu" => [SystemText("ui.secondaryreferencetext.details.the.menu.contains.character.status.junctions.items.and", "The menu contains character status, junctions, items and magic."), SystemText("ui.secondaryreferencetext.details.character.pages.show.the.selected.party.member", "Character pages show the selected party member."), SystemText("ui.secondaryreferencetext.details.status.includes.limit.break.information", "Status includes Limit Break information.")],
            "tutorial-junction" => CurrentScreen.Details(this).Take(4).ToArray(),
            "tutorial-card" => CurrentScreen.Details(this).Take(4).ToArray(),
            _ => CurrentScreen.Details(this)
        };
    }
}
