using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    public float TextScale
    {
        get
        {
            if (IsHostSettingsScreen || ScreenId == "debug-room" || IsNamingScreen || UseHostControls)
            {
                UiPreferences preferences = _readPreferences?.Invoke() ?? new UiPreferences();
                return preferences.TextScale / 100f * preferences.UiFontSize / 24f;
            }
            string value = Data.Settings.GetValueOrDefault("Text scale", "100%").TrimEnd('%');
            float fontScale = (_readPreferences?.Invoke().UiFontSize ?? 24) / 24f;
            return fontScale * (float.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out float percent) ? Math.Clamp(percent / 100, 1, 1.5f) : 1);
        }
    }
    public UiView BuildView()
    {
        if (_viewControllerPresentation != CurrentControllerPresentation || _viewControlDevice != CurrentControlDevice)
        {
            _view = null;
            _viewControllerPresentation = CurrentControllerPresentation;
            _viewControlDevice = CurrentControlDevice;
        }
        if (_view is not null)
        {
            return _view;
        }
        var root = new UiPanel { Id = "menu-root", DrawBackground = false, Padding = new UiThickness(20), CaptionHeight = 0 };
        var anchored = new UiAnchorPanel();
        root.Children.Add(anchored);
        if (CurrentScreen.BuildContent is not null)
        {
            var custom = new UiPanel { DrawBackground = false, CaptionHeight = 0, Padding = new UiThickness(0) };
            anchored.Children.Add(custom);
            CurrentScreen.BuildContent(this, custom);
        }
        else if (CurrentScreen.Layout == MenuLayout.Main)
        {
            anchored.Children.Add(BuildMain());
        }
        else if (ScreenId == "junction")
        {
            anchored.Children.Add(BuildJunction());
        }
        else
        {
            var page = Grid([1, 8, 0.7f], [1]);
            anchored.Children.Add(page);
            Place(page, Header(), 0);
            UiElement content = CurrentScreen.Layout switch
            {
                MenuLayout.GuardianForceGrid or MenuLayout.CardGrid => BuildCollection(),
                MenuLayout.BattleHud => BuildBattle(),
                _ => BuildStandard()
            };
            Place(page, content, 1);
            Place(page, Footer(), 2);
        }
        _view = new UiView(root) { DesignWidth = 960, DesignHeight = 672, Cancel = Back };
        _view.Resize(960, 672);
        var focusables = FocusableDescendants(root).ToArray();
        _view.Focus(focusables.OfType<UiList>().FirstOrDefault(list => list.Id == "commands") ?? focusables.FirstOrDefault(element => element.Id == SelectedEntry?.Id) ?? focusables.FirstOrDefault());
        return _view;
    }

    private UiElement BuildMain()
    {
        var page = Grid([1, 7.5f, 0.7f], [3, 1]);
        Place(page, Help(), 0, 0);
        var partyArea = Grid([1.15f, 1.15f, 1.15f, 2.1f], [1]);
        Place(page, partyArea, 1, 0);
        MenuCharacter[] party = Data.Characters.Where(character => character.InParty).ToArray();
        for (int index = 0; index < 3; index++)
        {
            MenuCharacter? character = party.ElementAtOrDefault(index);
            var row = Grid([1], [2.4f, 1.3f, 2.8f]);
            Place(row, Label(character?.Name ?? "--", 30), 0, 0);
            Place(row, Label($"Lv  {Number(character?.Level)}", 23), 0, 1);
            Place(row, Label("HP  " + (character is null ? "--" : Hp(character)), 23), 0, 2);
            Place(partyArea, Panel("STATUS", row), index);
        }
        var reserves = Data.Characters.Where(character => !character.InParty).ToArray();
        var reserveGrid = new UiGrid { Columns = 2, Gap = 12 };
        foreach (MenuCharacter character in reserves)
        {
            var row = Grid([1, 1], [1], 2);
            row.Height = 54 * TextScale;
            row.VerticalAlignment = UiAlignment.Start;
            Place(row, Label(character.Name, 21), 0);
            Place(row, Label($"Lv {Number(character.Level)}  HP {Hp(character)}", 17), 1);
            reserveGrid.Children.Add(row);
        }
        Place(partyArea, Panel(null, reserveGrid), 3);
        var sidebar = Grid([6.5f, 1.3f], [1]);
        Place(page, sidebar, 0, 1, 2);
        Place(sidebar, Panel(null, Commands(41, 27)), 0);
        string time = Data.PlayTime.HasValue ? $"{(int)Data.PlayTime.Value.TotalHours:00}:{Data.PlayTime.Value.Minutes:00}" : "--:--";
        Place(sidebar, Panel(null, Lines([$"Play      {time}", $"SeeD   {Number(Data.SeeDRank)}", $"{Number(Data.Currency)}G"], 20)), 1);
        Place(page, Footer(), 2, 0, 1, 2);
        return page;
    }

    private UiElement BuildJunction()
    {
        var page = Grid([0.8f, 1, 3.3f, 3.2f, 0.65f], [3, 1]);
        var tabs = new UiGrid { Columns = Math.Max(1, Entries.Count), Gap = 4 };
        for (int index = 0; index < Entries.Count; index++)
        {
            int selected = index;
            MenuEntry entry = Entries[index];
            string label = entry.Label switch
            {
                "Elemental" => "Elem",
                "Command" => "Cmd",
                _ => entry.Label
            };
            tabs.Children.Add(new UiButton { Id = entry.Id, Text = label, Click = () => Activate(selected) });
        }
        Place(page, tabs, 0, 0);
        Place(page, Panel(null, Label("Junction", 28)), 0, 1);
        Place(page, Help(), 1, 0, 1, 2);
        var characterArea = Grid([1], [0.65f, 2.8f]);
        Place(page, characterArea, 2, 0);
        Place(characterArea, Portrait(Character?.Name ?? "?"), 0, 0);
        var summary = Grid([1.2f, 1, 1, 0.8f], [1], 4);
        Place(characterArea, summary, 0, 1);
        Place(summary, Label(Character?.Name ?? "--", 34), 0);
        Place(summary, Label($"Level       {Number(Character?.Level)}", 26), 1);
        Place(summary, Label("HP       " + (Character is null ? "--" : Hp(Character)), 26), 2);
        Place(summary, Label($"EXP {Number(Character?.Experience)}p   Next {Number(Character?.NextLevelExperience)}p", 18), 3);
        Place(page, Panel("COMMAND", Lines(Character?.Commands.Take(4).ToArray() ?? ["--"], 25)), 2, 1);
        var stats = Grid([1], [1, 1], 20);
        Place(stats, StatColumn(["HP", "Str", "Vit", "Mag", "Spr"]), 0, 0);
        Place(stats, StatColumn(["", "Spd", "Eva", "Hit", "Luck"]), 0, 1);
        Place(page, Panel(null, stats), 3, 0, 1, 2);
        Place(page, Footer(), 4, 0, 1, 2);
        return page;
    }

    private UiElement StatColumn(string[] names)
    {
        var column = Grid(Enumerable.Repeat(1f, names.Length).ToArray(), [1], 4);
        for (int index = 0; index < names.Length; index++)
        {
            string name = names[index];
            var row = Grid([1], [0.7f, 2.4f, 0.7f], 8);
            Place(column, row, index);
            if (name.Length == 0)
            {
                continue;
            }
            Place(row, Label(name, 24), 0, 0);
            Place(row, Label(Character?.Junctions.GetValueOrDefault(name, "--") ?? "--", 24), 0, 1);
            string value = name == "HP" ? Number(Character?.MaximumHp) : Character?.Stats.TryGetValue(name, out int stat) == true ? stat.ToString() : "--";
            Place(row, Label(value, 24), 0, 2);
        }
        return column;
    }

    private UiElement BuildStandard()
    {
        bool characterPage = CurrentScreen.Layout is MenuLayout.Character or MenuLayout.Junction;
        var content = Grid(characterPage ? [1.3f, 5.8f] : [1], [1]);
        if (characterPage)
        {
            var summary = Grid([1, 1], [1], 2);
            Place(summary, Label($"Lv {Number(Character?.Level)}     HP {(Character is null ? "--" : Hp(Character))}     {Character?.Weapon ?? "--"}", 19), 0);
            Place(summary, Label("Commands: " + (Character?.Commands.Count > 0 ? string.Join(" / ", Character.Commands) : "--"), 18), 1);
            Place(content, Panel(Character?.Name ?? "--", summary), 0);
        }
        var panes = Grid([1], [0.95f, 1.05f]);
        Place(content, panes, characterPage ? 1 : 0);
        Place(panes, Panel(null, Commands(30, 19)), 0, 0);
        bool showFontPreviews = ScreenId == "debug-ui" || ScreenId == "config" && SelectedEntry?.Id is ("UI font" or "UI font size" or "Dialogue font" or "Dialogue font size");
        Place(panes, Panel(ScreenId == "debug-room" ? "Selected preview" : CurrentScreen.Title, showFontPreviews ? BuildFontPreviews() : Details()), 0, 1);
        return content;
    }

    private UiElement BuildFontPreviews()
    {
        UiPreferences preferences = _readPreferences?.Invoke() ?? new UiPreferences();
        var previews = Grid([0.7f, 2, 0.7f, 2, 0.7f], [1]);
        Place(previews, Label($"UI · {preferences.UiFontFamily} · {preferences.UiFontSize}px", 17), 0);
        Place(previews, new FontPreviewElement(preferences.UiFontFamily, preferences.UiFontSize * preferences.TextScale / 100f) { Id = "ui-font-preview" }, 1);
        Place(previews, Label($"Dialogue · {preferences.DialogueFontFamily} · {preferences.DialogueFontSize}px", 17), 2);
        Place(previews, new FontPreviewElement(preferences.DialogueFontFamily, preferences.DialogueFontSize * 3) { Id = "dialogue-font-preview" }, 3);
        Place(previews, Label("Unicode samples use OS shaping and fallback.", 15), 4);
        return previews;
    }

    private UiElement BuildCollection()
    {
        var content = Grid([3.7f, 1.2f], [1]);
        int columns = Math.Max(1, CurrentScreen.Columns);
        int visibleRows = CurrentScreen.Layout == MenuLayout.GuardianForceGrid ? 2 : 4;
        int page = SelectedIndex / (columns * visibleRows);
        int first = page * columns * visibleRows;
        var tiles = new UiGrid { Columns = columns, Rows = visibleRows, Gap = 6 };
        Place(content, tiles, 0);
        for (int index = first; index < Math.Min(Entries.Count, first + columns * visibleRows); index++)
        {
            int selected = index;
            MenuEntry entry = Entries[index];
            var button = new UiButton { Id = entry.Id, Click = () => Activate(selected) };
            var tile = Grid([0.5f, 2.1f, 0.55f], [1], 3);
            tile.Margin = new UiThickness(9);
            button.Children.Add(tile);
            Place(tile, Label(entry.Label, 14), 0);
            Place(tile, new UiSigil { Seed = StableHash(entry.Id + entry.Label), Initials = entry.Label[..1] }, 1);
            string detail = CurrentScreen.Layout == MenuLayout.GuardianForceGrid ? "Lv  " + Number(Data.GuardianForces.FirstOrDefault(force => force.Id == entry.Id)?.Level) : "x" + (Data.Cards.FirstOrDefault(card => card.Id == entry.Id)?.Quantity.ToString() ?? "--");
            Place(tile, Label(detail, 20), 2);
            int local = index - first;
            Place(tiles, button, local / columns, local % columns);
        }
        if (Entries.Count == 0)
        {
            Place(tiles, Label("No collection data", 20), 0, 0, visibleRows, columns);
        }
        int pages = Math.Max(1, (Entries.Count + columns * visibleRows - 1) / (columns * visibleRows));
        Place(content, Panel($"Page {page + 1} / {pages}", Details()), 1);
        return content;
    }

    private UiElement BuildBattle()
    {
        var battlefield = Grid([2.5f, 2], [1, 2.6f]);
        var enemies = new UiGrid { Columns = Math.Max(1, Data.BattleTargets.Count), Gap = 20 };
        foreach (MenuBattleTarget target in Data.BattleTargets)
        {
            var enemy = Grid([2.5f, 0.6f, 0.7f], [1], 4);
            Place(enemy, new UiSigil { Seed = StableHash(target.Id), Initials = target.Name[..1] }, 0);
            Place(enemy, Label(target.Name, 18), 1);
            Place(enemy, Label($"HP {Number(target.CurrentHp)} / {Number(target.MaximumHp)}", 16), 2);
            enemies.Children.Add(enemy);
        }
        Place(battlefield, enemies, 0, 0, 1, 2);
        Place(battlefield, Panel(CurrentScreen.Title, Commands(30, 20)), 1, 0);
        var party = new UiGrid { Columns = 1, Gap = 8 };
        foreach (MenuCharacter character in Data.Characters.Where(character => character.InParty))
        {
            var member = Grid([1, 0.6f], [1.2f, 1.5f, 1.4f], 8);
            Place(member, Label(character.Name, 22), 0, 0);
            Place(member, Label("HP " + Hp(character), 20), 0, 1);
            var atb = new UiProgressBar { Min = 0, Max = 1, Value = character.AtbProgress ?? 0, Text = character.AtbProgress.HasValue ? "ATB" : "ATB --" };
            Place(member, atb, 0, 2);
            Place(member, Label(string.Join(" / ", character.Conditions), 14), 1, 0, 1, 3);
            party.Children.Add(member);
        }
        var information = Grid([2, 1], [1], 6);
        Place(information, party, 0);
        Place(information, Details(), 1);
        Place(battlefield, Panel("PARTY", information), 1, 1);
        return battlefield;
    }

    private UiElement Header()
    {
        var header = Grid([1], [4, 1]);
        Place(header, Help(), 0, 0);
        Place(header, Panel(null, Label(CurrentScreen.Title, CurrentScreen.Title.Length > 12 ? 18 : 26)), 0, 1);
        return header;
    }

    private UiElement Help()
    {
        return Panel("HELP", Label(Message.Length > 0 ? Message : SelectedEntry?.Help ?? CurrentScreen.Help, 18));
    }

    private UiElement Footer()
    {
        if (IsNamingScreen)
        {
            return Panel(null, Label($"Current game    Confirm: {CurrentBindingText(ControlAction.Confirm)}    Back: {CurrentBindingText(ControlAction.Cancel)}", 14));
        }
        string text = IsCapturingControls ? "Release all to apply · Escape or click Cancel to cancel" : IsControlsScreen ? "Arrow keys: select · Enter: confirm · Escape: back · Mouse supported" : $"{(IsDemo ? "DEMO · " : "")}{Data.Location}    Confirm: {CurrentBindingText(ControlAction.Confirm)}    Back: {CurrentBindingText(ControlAction.Cancel)}    Menu: {CurrentBindingText(ControlAction.Menu)}";
        return Panel(null, Label(text, 14));
    }

    private UiElement Commands(float rowHeight, float fontSize)
    {
        if (Entries.Count == 0)
        {
            return Label("Unavailable", 20);
        }
        return new UiList
        {
            Id = "commands",
            Items = Entries.Select(entry => entry.Label).ToList(),
            SelectedIndex = Math.Clamp(SelectedIndex, 0, Entries.Count - 1),
            RowHeight = rowHeight * TextScale,
            FontSize = fontSize * TextScale,
            SelectionChanged = index => Select(index, Entries.Count),
            Confirmed = Activate
        };
    }

    private UiElement Details()
    {
        return new UiList { Id = "details", Items = CurrentScreen.Details(this).ToList(), FontSize = 17 * TextScale, RowHeight = 29 * TextScale, ShowSelection = false };
    }

    private UiElement Lines(IReadOnlyList<string> values, float fontSize)
    {
        var lines = new UiGrid { Columns = 1, Gap = 3 };
        foreach (string value in values)
        {
            lines.RowDefinitions.Add(UiGridLength.Pixel(fontSize * TextScale * 1.5f));
            Place(lines, Label(value, fontSize), lines.Children.Count);
        }
        lines.RowDefinitions.Add(UiGridLength.Star());
        return lines;
    }

    private UiPanel Panel(string? caption, UiElement content)
    {
        var panel = new UiPanel { Caption = caption, Padding = new UiThickness(12, 8), CaptionHeight = string.IsNullOrEmpty(caption) ? 0 : 24 * TextScale };
        panel.Children.Add(content);
        return panel;
    }

    private UiLabel Label(string value, float fontSize)
    {
        return new UiLabel { Text = value, FontSize = fontSize * TextScale, VerticalAlignment = UiAlignment.Center, Height = fontSize * TextScale * 1.4f };
    }

    private static UiPortrait Portrait(string name)
    {
        return new UiPortrait { Id = "portrait-" + name, Seed = StableHash(name), Initials = name[..1] };
    }

    private static UiGrid Grid(float[] rows, float[] columns, float gap = 12)
    {
        var grid = new UiGrid { Gap = gap };
        foreach (float weight in rows)
        {
            grid.RowDefinitions.Add(UiGridLength.Star(weight));
        }
        foreach (float weight in columns)
        {
            grid.ColumnDefinitions.Add(UiGridLength.Star(weight));
        }
        return grid;
    }

    private static void Place(UiElement parent, UiElement child, int row, int column = 0, int rowSpan = 1, int columnSpan = 1)
    {
        child.GridRow = row;
        child.GridColumn = column;
        child.GridRowSpan = rowSpan;
        child.GridColumnSpan = columnSpan;
        parent.Children.Add(child);
    }

    private static int StableHash(string value)
    {
        int hash = 17;
        foreach (char character in value)
        {
            hash = unchecked(hash * 31 + character);
        }
        return hash;
    }

    private static IEnumerable<UiElement> FocusableDescendants(UiElement element)
    {
        if (element.Focusable)
        {
            yield return element;
        }
        foreach (UiElement child in element.Children)
        {
            foreach (UiElement descendant in FocusableDescendants(child))
            {
                yield return descendant;
            }
        }
    }
}
