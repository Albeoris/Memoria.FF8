using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private bool IsOriginalMenuPage => !ScreenId.StartsWith("debug-", StringComparison.Ordinal) && !ScreenId.StartsWith("mods", StringComparison.Ordinal) && !IsNamingScreen && !UseHostControls && CurrentScreen.Layout != MenuLayout.BattleHud;

    private UiElement BuildMain()
    {
        var page = Grid([1.05f, 6.55f, 1], [3, 1.1f], 6);
        Place(page, Help(), 0);
        var partyArea = Grid([1, 1, 1, 3], [1], 0);
        partyArea.Margin = new UiThickness(20, 0, 10, 0);
        Place(page, partyArea, 1);
        MenuCharacter[] party = CharacterChoices.Where(character => character.InParty).ToArray();
        for (int index = 0; index < 3; index++)
        {
            MenuCharacter? character = party.ElementAtOrDefault(index);
            var row = Grid([1], [0.75f, 1.7f, 1.15f, 2.7f], 5);
            Place(row, Portrait(character), 0);
            Place(row, Label(character?.Name ?? "", 28), 0, 1);
            Place(row, Label(character is null ? "" : $"Lv {Number(character.Level)}", 23), 0, 2);
            Place(row, Label(character is null ? "" : "HP " + Hp(character), 23), 0, 3);
            UiPanel status = Panel(null, row);
            status.Id = "party-status-" + index;
            if (IsSelectingCharacter && character is not null)
            {
                string characterId = character.Id;
                UiButton choice = new() { Id = characterId, DrawBackground = false, HighlightSelection = false, Click = () => ChooseCharacter(characterId) };
                status.Margin = new UiThickness(24, 0, 0, 0);
                choice.Children.Add(status);
                Place(partyArea, choice, index);
            }
            else
            {
                Place(partyArea, status, index);
            }
        }
        MenuCharacter[] reserveCharacters = CharacterChoices.Where(character => !character.InParty).ToArray();
        var reserves = Grid(Enumerable.Repeat(1f, Math.Max(3, (reserveCharacters.Length + 1) / 2)).ToArray(), [1, 1], 3);
        for (int index = 0; index < reserveCharacters.Length; index++)
        {
            MenuCharacter character = reserveCharacters[index];
            var member = Grid([1, 1], [0.75f, 2.5f], 3);
            Place(member, Portrait(character), 0, 0, 2);
            Place(member, Label(character.Name, 26), 0, 1);
            Place(member, Label($"Lv {Number(character.Level)}   HP {character.HpDisplay ?? Number(character.CurrentHp)}", 20), 1, 1);
            if (IsSelectingCharacter)
            {
                string characterId = character.Id;
                UiButton choice = new() { Id = characterId, DrawBackground = false, HighlightSelection = false, Click = () => ChooseCharacter(characterId) };
                member.Margin = new UiThickness(24, 0, 0, 0);
                choice.Children.Add(member);
                Place(reserves, choice, index / 2, index % 2);
            }
            else
            {
                Place(reserves, member, index / 2, index % 2);
            }
        }
        Place(partyArea, Panel(null, reserves), 3);
        var sidebar = Grid([6.3f, 0.3f, 1.4f], [1], 0);
        Place(page, sidebar, 0, 1, 3);
        Place(sidebar, Panel(null, Commands(40, 29)), 0);
        Place(page, Panel(null, Label(Data.Location, 29)), 2);
        string time = Data.PlayTime.HasValue ? $"{(int)Data.PlayTime.Value.TotalHours:00}:{Data.PlayTime.Value.Minutes:00}" : "--:--";
        Place(sidebar, Panel(null, Readouts([$"PLAY  {time}", $"SeeD  {Number(Data.SeeDRank)}", $"{Number(Data.Currency)}G"], 23)), 2);
        return page;
    }

    private UiElement NativeMenuHeader(string title, UiElement? commands = null, bool fullWidthCommands = false)
    {
        var header = Grid([0.85f, 1.15f], [3, 1.1f], 3);
        Place(header, Panel(null, commands ?? new UiElement()), 0, 0, 1, fullWidthCommands ? 2 : 1);
        if (!fullWidthCommands)
        {
            Place(header, Panel(null, Label(title, 27)), 0, 1);
        }
        UiElement help = Help();
        Place(header, help, 1, 0, 1, 2);
        return header;
    }

    private UiElement NativeCommandStrip()
    {
        UiElement commands = Commands(38, 24);
        if (commands is UiList list)
        {
            list.Orientation = UiOrientation.Horizontal;
        }
        return commands;
    }

    private UiElement BuildJunction()
    {
        if (ScreenId is "junction-magic" or "junction-stock" or "junction-element" or "junction-status")
        {
            return BuildMagicJunction();
        }
        if (ScreenId is "junction-command" or "junction-ability" or "junction-command-choice" or "junction-ability-choice")
        {
            return BuildAbilityJunction();
        }
        var page = Grid([1.45f, 2.7f, 1.8f], [1], 4);
        UiElement tabs;
        bool choosingCategory = ScreenId is "junction-options" or "junction-off" or "junction-auto";
        if (ScreenId == "junction" || choosingCategory)
        {
            tabs = NativeCommandStrip();
            if (choosingCategory && tabs is UiList categories)
            {
                categories.FontSize = 20 * TextScale;
                categories.TextInset = 26 * TextScale;
            }
        }
        else
        {
            var strip = new UiGrid { Columns = 4, Gap = 0 };
            string[] names = ["Junction", "Off", "Auto", "Ability"];
            string[] destinations = ["junction-options", "junction-off", "junction-auto", "junction-command"];
            for (int index = 0; index < names.Length; index++)
            {
                string destination = destinations[index];
                strip.Children.Add(new UiButton { Text = names[index], FontSize = 24 * TextScale, DrawBackground = false, HighlightSelection = false, Click = () => Push(destination) });
            }
            tabs = strip;
        }
        Place(page, NativeMenuHeader("Junction", tabs, choosingCategory), 0);
        var body = Grid([1], [3, 1.1f], 4);
        Place(page, body, 1);
        var character = Grid([2.1f, 0.7f], [1], 4);
        Place(body, character, 0);
        var identity = Grid([1], [0.9f, 3], 10);
        identity.Margin = new UiThickness(20, 0, 0, 0);
        Place(character, identity, 0);
        UiImage portrait = Portrait(Character);
        Place(identity, portrait, 0);
        var summary = Grid([1, 1, 1], [1], 0);
        Place(identity, summary, 0, 1);
        Place(summary, Label(Character?.Name ?? "--", 30), 0);
        Place(summary, Label($"Lv      {Number(Character?.Level)}", 25), 1);
        Place(summary, Label("HP    " + (Character is null ? "--" : Hp(Character)), 25), 2);
        var experience = Grid([1, 1], [1.6f, 1.4f], 0);
        Place(character, experience, 1);
        Place(experience, Label("Current EXP", 22), 0);
        Place(experience, Label(Number(Character?.Experience) + "p", 22), 0, 1);
        Place(experience, Label("Next LEVEL", 22), 1);
        Place(experience, Label(Number(Character?.NextLevelExperience) + "p", 22), 1, 1);
        bool choosingEntity = ScreenId != "junction" && !choosingCategory;
        if (choosingEntity)
        {
            UiPanel selection = Panel("JUNCTION", Commands(31, 21));
            selection.Id = "junction-selection";
            Place(body, selection, 0, 1);
        }
        else
        {
            Place(body, Panel("COMMAND", Readouts(Character?.Commands.Take(4).ToArray() ?? ["--"], 25)), 0, 1);
        }
        var footer = Grid([1], choosingEntity ? [3, 1.1f] : [1], 4);
        var stats = Grid([1], [1, 1], 12);
        Place(stats, StatColumn(["HP", "Str", "Vit", "Mag", "Spr"]), 0);
        Place(stats, StatColumn(["", "Spd", "Eva", "Hit", "Luck"]), 0, 1);
        UiPanel status = Panel(null, stats);
        status.Id = "junction-stats";
        Place(footer, status, 0);
        if (choosingEntity)
        {
            Place(footer, Panel(null, JunctionSelectionDetails()), 0, 1);
        }
        Place(page, footer, 2);
        return page;
    }

    private UiElement BuildMagicJunction()
    {
        if (IsEffectJunctionPage)
        {
            var effectPage = Grid([1.35f, 6.3f], [1], 4);
            Place(effectPage, NativeMenuHeader("Junction", Label("Magic", 25)), 0);
            Place(effectPage, BuildEffectJunction(false), 1);
            return effectPage;
        }
        var page = Grid([1.35f, 0.8f, 5.5f], [1], 4);
        Place(page, NativeMenuHeader("Junction", Label("Magic", 25)), 0);
        Place(page, Panel(null, Label($"{Character?.Name ?? "--"}    Lv {Number(Character?.Level)}    HP {(Character is null ? "--" : Hp(Character))}", 24)), 1);
        var body = Grid([1], [1, 1], 4);
        Place(page, body, 2);
        bool choosingSpell = ScreenId == "junction-stock";
        Place(body, Panel(choosingSpell ? "MAGIC STOCK" : "JUNCTION", Commands(29, 21)), 0, choosingSpell ? 1 : 0);
        IReadOnlyDictionary<string, int>? preview = choosingSpell && SelectedEntry is { Enabled: true, Action.Kind: "junction-magic" or "junction-magic-reassign" } entry ? Character?.JunctionPreview?.Invoke(_junctionSlot, entry.Action.SubjectId) : null;
        var details = Grid([0.7f, 5, 0.65f], [1], 2);
        string selection = choosingSpell ? $"{_junctionSlot}    {SelectedEntry?.Label ?? "--"}" : SelectedEntry?.Label ?? "--";
        Place(details, Label(selection, 21), 0);
        var stats = Grid(Enumerable.Repeat(1f, 9).ToArray(), [1], 0);
        string[] names = ["HP", "Str", "Vit", "Mag", "Spr", "Spd", "Eva", "Hit", "Luck"];
        for (int index = 0; index < names.Length; index++)
        {
            string name = names[index];
            var row = Grid([1], [0.7f, 1.8f, 2.1f], 3);
            Place(stats, row, index);
            Place(row, JunctionSlotLabel(name, name, 21), 0);
            Place(row, JunctionSlotLabel(name, Character?.Junctions.GetValueOrDefault(name, "----") ?? "----", 20), 0, 1);
            int? current = name == "HP" ? Character?.MaximumHp : Character?.Stats.TryGetValue(name, out int value) == true ? value : null;
            int? projected = preview?.TryGetValue(name, out int next) == true ? next : null;
            string display = Number(current);
            if (projected is int after && current is int before && after != before)
            {
                display += $" → {after}";
            }
            UiLabel label = Label(display, 20);
            label.Id = "junction-value-" + name;
            if (projected is int changed && current is int previous && changed != previous)
            {
                label.Color = changed > previous ? new UiColor(120, 190, 255) : new UiColor(255, 120, 120);
            }
            Place(row, label, 0, 2);
        }
        Place(details, stats, 1);
        Place(details, choosingSpell ? Label("Current → Preview    Confirm to apply", 17) : JunctionAbilityPageLink(), 2);
        Place(body, Panel("STATUS", details), 0, choosingSpell ? 0 : 1);
        return page;
    }

    private UiElement JunctionSelectionDetails()
    {
        if (ScreenId == "junction-gf")
        {
            MenuGuardianForce? force = Data.GuardianForces.FirstOrDefault(value => value.Id == SelectedEntry?.Id);
            var details = Grid([1, 1, 1, 1], [1], 0);
            details.Id = "junction-gf-details";
            Place(details, Label(force?.Name ?? "--", 18), 0);
            Place(details, Label(force is null ? "--" : $"Lv {Number(force.Level)}    HP {Number(force.CurrentHp)} / {Number(force.MaximumHp)}", 18), 1);
            Place(details, Label("Learning", 18), 2);
            UiLabel learning = Label(force?.Learning ?? "--", 18);
            learning.Id = "junction-gf-learning";
            Place(details, learning, 3);
            return details;
        }
        return Lines([Character?.Name ?? "--", "HP  " + (Character is null ? "--" : Hp(Character)), "" + (SelectedEntry?.Help ?? CurrentScreen.Help)], 18);
    }

    private UiElement StatColumn(string[] names)
    {
        var column = Grid(Enumerable.Repeat(1f, names.Length).ToArray(), [1], 0);
        for (int index = 0; index < names.Length; index++)
        {
            string name = names[index];
            var row = Grid([1], [0.8f, 2.2f, 0.9f], 4);
            Place(column, row, index);
            if (name.Length == 0)
            {
                continue;
            }
            Place(row, JunctionSlotLabel(name, name, 23), 0);
            Place(row, JunctionSlotLabel(name, Character?.Junctions.GetValueOrDefault(name, "----") ?? "----", 23), 0, 1);
            string value = name == "HP" ? Number(Character?.MaximumHp) : Character?.Stats.TryGetValue(name, out int stat) == true ? stat.ToString() : "--";
            Place(row, Label(value, 23), 0, 2);
        }
        return column;
    }

    private UiLabel JunctionSlotLabel(string slot, string text, float fontSize)
    {
        UiLabel label = Label(text, fontSize);
        label.Muted = !IsDemo && Character?.AvailableJunctionSlots.Contains(slot) != true;
        return label;
    }

    private UiElement BuildItemPage()
    {
        bool rootPage = ScreenId == "item";
        bool targetPage = ScreenId == "item-target";
        var page = Grid([1.2f, 5], [1], 4);
        UiElement strip = rootPage ? NativeCommandStrip() : InventoryNavigation(false);
        Place(page, NativeMenuHeader("Item", strip), 0);
        var body = Grid([1], [1, 1], 4);
        Place(page, body, 1);
        UiElement inventory;
        if (!rootPage && !targetPage)
        {
            inventory = Commands(37, 25);
        }
        else
        {
            var stock = new UiList { Id = "inventory-preview", FontSize = 25 * TextScale, RowHeight = 37 * TextScale, ShowSelection = false, Focusable = true };
            stock.Items = Data.Items.Select(value => value.Name).ToList();
            stock.Images = Data.Items.Select(value => value.Icon).ToList();
            inventory = stock;
        }
        if (inventory is UiList inventoryList && ScreenId != "item-target")
        {
            inventory = InventoryQuantities(inventoryList, Data.Items.Select(value => value.Name).ToList(), Data.Items.Select(value => value.Quantity.ToString()).ToList());
        }
        Place(body, Panel("ITEM", inventory), 0);
        var right = Grid([3, 1], [1], 3);
        Place(body, right, 0, 1);
        UiElement characters = targetPage ? Commands(47, 25) : InventoryParty();
        Place(right, Panel("NAME                 HP", characters), 0);
        string[] conditions = targetPage ? Character?.Conditions.ToArray() ?? [] : [];
        Place(right, Panel("STATUS", Lines(conditions, 22)), 1);
        return page;
    }

    private UiElement InventoryNavigation(bool magic)
    {
        var strip = new UiGrid { Columns = magic ? 4 : 3, Gap = 0 };
        string[] captions = magic ? ["Use", "Stock", "Exchange", "Rearrange"] : ["Use", "Rearrange", "Sort"];
        string[] destinations = magic ? ["magic-use", "magic-stock", "magic-exchange", "magic"] : ["item-use", "item-rearrange", "item"];
        for (int index = 0; index < captions.Length; index++)
        {
            string destination = destinations[index];
            strip.Children.Add(new UiButton
            {
                Text = captions[index],
                FontSize = 24 * TextScale,
                DrawBackground = false,
                HighlightSelection = false,
                Click = () =>
                {
                    if (!magic && destination == "item")
                    {
                        Execute(new MenuAction("item-sort"));
                    }
                    else
                    {
                        Push(destination);
                    }
                }
            });
        }
        return strip;
    }

    private UiElement InventoryParty()
    {
        MenuCharacter[] characters = Data.Characters.Where(character => character.InParty).ToArray();
        var party = Grid(Enumerable.Repeat(1f, Math.Max(6, characters.Length)).ToArray(), [1], 2);
        for (int index = 0; index < characters.Length; index++)
        {
            MenuCharacter character = characters[index];
            var row = Grid([1], [1.8f, 1], 4);
            Place(row, Label(character.Name, 25), 0);
            Place(row, Label(character.HpDisplay ?? Number(character.CurrentHp), 25), 0, 1);
            Place(party, row, index);
        }
        return party;
    }
}
