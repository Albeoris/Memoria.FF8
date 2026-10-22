using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private MenuPresentation presentation;
    private IMenuPresentation presentationProvider = new VectorMenuPresentation();

    public MenuPresentation Presentation
    {
        get => presentation;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            if (presentation != value)
            {
                presentation = value;
                PresentationProvider = value == MenuPresentation.Vector ? new VectorMenuPresentation() : new ClassicMenuPresentation();
            }
        }
    }

    /// <summary>Replacing a presentation invalidates only the retained view, preserving all navigation and game state.</summary>
    public IMenuPresentation PresentationProvider
    {
        get => presentationProvider;
        set
        {
            presentationProvider = value ?? throw new ArgumentNullException(nameof(value));
            _view = null;
        }
    }

    public UiTheme CreateTheme()
    {
        UiTheme theme = PresentationProvider.CreateTheme(TextScale);
        if (_readPreferences is null && !IsDemo || PresentationProvider is not (ClassicMenuPresentation or VectorMenuPresentation))
        {
            return theme;
        }
        string family = (_readPreferences?.Invoke() ?? demoFontPreferences).GetUiFontFamily(Presentation);
        return theme is VectorUiTheme
            ? new VectorUiTheme { FontSize = theme.FontSize, PanelBackgroundAlpha = theme.PanelBackgroundAlpha, FontFamily = family }
            : new UiTheme { FontSize = theme.FontSize, PanelBackgroundAlpha = theme.PanelBackgroundAlpha, FontFamily = family };
    }
    internal UiThickness ClassicRootPadding => new(ScreenId == "magazine" ? 0 : IsOriginalMenuPage ? 18 : 20);

    private bool UsesAbilityJunctionComposition => ScreenId is "junction-command" or "junction-ability" or "junction-command-choice" or "junction-ability-choice";
    internal bool UsesVectorComposition => ScreenId == "main" || ScreenId.StartsWith("junction", StringComparison.Ordinal) && !UsesAbilityJunctionComposition;

    internal UiElement BuildVectorContent()
    {
        if (ScreenId == "main")
        {
            return BuildVectorMain();
        }
        if (ScreenId == "magic-target")
        {
            return BuildVectorMagicTarget();
        }
        if (UsesAbilityJunctionComposition)
        {
            return BuildClassicContent();
        }
        if (ScreenId.StartsWith("junction", StringComparison.Ordinal))
        {
            return BuildVectorJunction();
        }
        return BuildClassicContent();
    }

    private UiElement BuildVectorMagicTarget()
    {
        var page = Grid([1.2f, 5], [1], 4);
        Place(page, NativeMenuHeader("Magic", InventoryNavigation(true)), 0);
        var body = Grid([1], [1.05f, 1], 12);
        var characterAndTargets = Grid([2, 3], [1], 10);
        Place(characterAndTargets, MagicCharacterSummary(Character, true), 0);
        Place(characterAndTargets, Panel("NAME", Commands(48, 25)), 1);
        Place(body, characterAndTargets, 0);
        Place(body, Panel("MAGIC", MagicStockPreview(Character)), 0, 1);
        Place(page, body, 1);
        return page;
    }

    private UiElement BuildVectorMain()
    {
        var page = VectorRows([80, 0, 32], [3.2f, 1], 12);
        page.Id = "vector-main";
        Place(page, VectorHeader(SelectedEntry?.Label ?? "Menu", SelectedEntry?.Help ?? CurrentScreen.Help), 0);
        var party = VectorRows([18, 0, 18, Math.Max(72, ((CharacterChoices.Count(member => !member.InParty) + 2) / 3) * 56)], [1], 4);
        Place(page, party, 1);
        Place(party, VectorLabel("A C T I V E   P A R T Y", 11, true), 0);
        MenuCharacter[] active = CharacterChoices.Where(member => member.InParty).ToArray();
        var cards = Grid([1, 1, 1], [1], 6);
        Place(party, cards, 1);
        for (int index = 0; index < 3; index++)
        {
            UiElement card = VectorPartyCard(active.ElementAtOrDefault(index), false);
            if (!card.Focusable)
            {
                card.Id = "party-status-" + index;
            }
            Place(cards, card, index);
        }
        Place(party, VectorLabel("R E S E R V E", 11, true), 2);
        MenuCharacter[] reserve = CharacterChoices.Where(member => !member.InParty).ToArray();
        var reserves = Grid(Enumerable.Repeat(1f, Math.Max(1, (reserve.Length + 2) / 3)).ToArray(), [1, 1, 1], 8);
        for (int index = 0; index < reserve.Length; index++)
        {
            Place(reserves, VectorPartyCard(reserve[index], true), index / 3, index % 3);
        }
        Place(party, reserves, 3);
        var sidebar = VectorRows([0, 85], [1], 12);
        var commands = (UiList)Commands(32, 23);
        commands.Paginated = false;
        commands.AutoRowHeight = false;
        commands.RowHeight = Math.Min(32, 320f / Math.Max(1, Entries.Count));
        commands.FontSize = Math.Min(23 * TextScale, commands.RowHeight * 0.85f);
        commands.TextInset = 22;
        Place(sidebar, commands, 0);
        string time = Data.PlayTime.HasValue ? $"{(int)Data.PlayTime.Value.TotalHours:00}:{Data.PlayTime.Value.Minutes:00}" : "--:--";
        Place(sidebar, new VectorReadouts([("P L A Y   T I M E", time), ("S e e D   R A N K", Number(Data.SeeDRank)), ("", Number(Data.Currency) + " G")]), 1);
        Place(page, VectorPanel(null, sidebar, 16), 0, 1, 2);
        var footer = Grid([1], [1, 1], 8);
        Place(footer, VectorLabel(Data.Location, 14, true), 0);
        Place(footer, VectorPrompts(), 0, 1);
        Place(page, new VectorRuleContainer(footer), 2, 0, 1, 2);
        return page;
    }

    private UiElement VectorPartyCard(MenuCharacter? member, bool compact)
    {
        var content = Grid([1], compact ? [0.8f, 1.4f] : [1.2f, 1, 1.8f], 10);
        Place(content, Portrait(member), 0);
        var name = Grid([1, 1], [1], 0);
        Place(name, VectorLabel(member?.Name ?? "", compact ? 15 : 28), 0);
        Place(name, VectorLabel(member is null ? "" : $"Lv  {Number(member.Level)}", compact ? 13 : 22, true), 1);
        Place(content, name, 0, 1);
        var hp = VectorRows([0, 7], [1], 2);
        Place(hp, VectorLabel(member is null ? "" : (compact ? "" : "HP   ") + Hp(member), compact ? 12 : 22), 0);
        Place(hp, new UiProgressBar { Min = 0, Max = Math.Max(1, member?.MaximumHp ?? 1), Value = member?.CurrentHp ?? 0, Height = compact ? 4 : 7, VerticalAlignment = UiAlignment.Start }, 1);
        if (compact)
        {
            var summary = Grid([2, 1], [1], 0);
            Place(summary, name, 0);
            Place(summary, hp, 1);
            content.Children.Remove(name);
            Place(content, summary, 0, 1);
        }
        else
        {
            hp.VerticalAlignment = UiAlignment.End;
            hp.Height = 42;
            Place(content, hp, 0, 2);
        }
        UiPanel panel = VectorPanel(null, content, compact ? 3 : 10);
        if (IsSelectingCharacter && member is not null)
        {
            string id = member.Id;
            var choice = new UiButton { Id = id, DrawBackground = false, Click = () => ChooseCharacter(id) };
            choice.Children.Add(panel);
            return choice;
        }
        return panel;
    }

    private UiElement BuildVectorJunction()
    {
        var page = VectorRows([Math.Max(78, 24 * TextScale * 1.5f + 16), 0, 34], [1], 12);
        page.Id = "vector-junction";
        var header = Grid([1], [1, 1.3f], 12);
        Place(header, VectorHeader("Junction", "Junction magic to strengthen your attributes."), 0);
        Place(header, BuildVectorJunctionTabs(), 0, 1);
        Place(page, new VectorRuleContainer(header, bottom: true), 0);
        var body = Grid([1], ScreenId == "junction-stock" ? [2.5f, 3.3f, 3.6f] : [2.65f, 4.8f, 1.95f], 10);
        Place(page, body, 1);
        Place(body, VectorCharacterSummary(), 0);
        bool stats = ScreenId is "junction" or "junction-magic";
        if (stats)
        {
            Place(body, VectorAttributes(ScreenId == "junction-magic"), 0, 1);
            Place(body, VectorPanel("C O M M A N D S", new VectorReadouts((Character?.Commands ?? []).Take(4).Select(command => (command, "")).ToArray(), 24), 12), 0, 2);
        }
        else
        {
            UiElement selection;
            if (ScreenId is "junction-command" or "junction-ability" or "junction-command-choice" or "junction-ability-choice")
            {
                selection = Commands(30, 19);
            }
            else
            {
                selection = Commands(30, 19);
            }
            Place(body, VectorPanel(ScreenId == "junction-stock" ? "M A G I C   S T O C K" : "J U N C T I O N", selection, 12), 0, 1);
            IReadOnlyDictionary<string, int>? preview = ScreenId == "junction-stock" && SelectedEntry is { Enabled: true, Action.Kind: "junction-magic" } entry ? Character?.JunctionPreview?.Invoke(_junctionSlot, entry.Action.SubjectId) : null;
            Place(body, ScreenId == "junction-stock" ? VectorAttributes(false, preview) : VectorPanel("D E T A I L S", ScreenId.StartsWith("junction-gf", StringComparison.Ordinal) ? JunctionSelectionDetails() : Details(), 12), 0, 2);
        }
        var footer = Grid([1], [1.7f, 1], 16);
        Place(footer, VectorLabel(Message.Length > 0 ? Message : SelectedEntry?.Help ?? CurrentScreen.Help, 13, true), 0);
        Place(footer, VectorPrompts(), 0, 1);
        Place(page, new VectorRuleContainer(footer), 2);
        return page;
    }

    private UiElement BuildVectorJunctionTabs()
    {
        float fontSize = 24 * TextScale;
        UiElement tabs;
        if (ScreenId == "junction")
        {
            var commands = (UiList)NativeCommandStrip();
            commands.Paginated = false;
            commands.FontSize = fontSize;
            commands.TextInset = 16 * TextScale;
            commands.PointerSize = 20 * TextScale;
            tabs = commands;
        }
        else
        {
            var strip = Grid([1], [1, 1, 1, 1], 0);
            string[] names = ["Junction", "Off", "Auto", "Ability"];
            string[] destinations = ["junction-options", "junction-off", "junction-auto", "junction-command"];
            int activeIndex = ScreenId.StartsWith("junction-off", StringComparison.Ordinal) ? 1 : ScreenId.StartsWith("junction-auto", StringComparison.Ordinal) ? 2 : ScreenId.Contains("command", StringComparison.Ordinal) || ScreenId.Contains("ability", StringComparison.Ordinal) ? 3 : 0;
            for (int index = 0; index < names.Length; index++)
            {
                string destination = destinations[index];
                var tab = new UiButton { Text = names[index], FontSize = fontSize, TextInset = 16 * TextScale, DrawBackground = false, PointerSize = 20 * TextScale, Click = () => Push(destination) };
                Place(strip, index == activeIndex ? new VectorActiveTab(tab) : tab, 0, index);
            }
            tabs = strip;
        }
        var host = new UiElement { Id = "junction-tabs", VerticalAlignment = UiAlignment.End, Height = Math.Max(42, fontSize * 1.5f + 8) };
        host.Children.Add(tabs);
        return host;
    }

    private UiElement VectorCharacterSummary()
    {
        var content = VectorRows([0, 42, 26, 36, 43], [1], 2);
        Place(content, Portrait(Character), 0);
        var identity = Grid([1], [1, 1, 1], 0);
        identity.ColumnDefinitions[0] = UiGridLength.Pixel(18);
        identity.ColumnDefinitions[2] = UiGridLength.Pixel(18);
        Place(identity, new UiButton { Text = "‹", TextInset = 0, DrawBackground = false, FontSize = 28, PointerSize = 0, Click = () => Navigate(MenuInput.PageLeft), Width = 32, HorizontalAlignment = UiAlignment.Center }, 0);
        Place(identity, VectorLabel(Character?.Name ?? "--", 34), 0, 1);
        Place(identity, new UiButton { Text = "›", TextInset = 0, DrawBackground = false, FontSize = 28, PointerSize = 0, Click = () => Navigate(MenuInput.PageRight), Width = 32, HorizontalAlignment = UiAlignment.Center }, 0, 2);
        Place(content, identity, 1);
        Place(content, VectorLabel("Lv   " + Number(Character?.Level), 22, true), 2);
        var hp = VectorRows([0, 7], [1], 1);
        Place(hp, VectorLabel("HP   " + (Character is null ? "--" : Hp(Character)), 21), 0);
        Place(hp, new UiProgressBar { Min = 0, Max = Math.Max(1, Character?.MaximumHp ?? 1), Value = Character?.CurrentHp ?? 0, Height = 7 }, 1);
        Place(content, hp, 3);
        Place(content, new VectorReadouts([("Current EXP", Number(Character?.Experience)), ("Next level", Number(Character?.NextLevelExperience))], 14), 4);
        return VectorPanel(null, content, 12);
    }

    private UiElement VectorAttributes(bool interactive, IReadOnlyDictionary<string, int>? preview = null)
    {
        string[] names = ["HP", "Str", "Vit", "Mag", "Spr", "Spd", "Eva", "Hit", "Luck"];
        var table = VectorRows([24, 0], [1], 5);
        Place(table, new VectorReadouts([("S T A T       M A G I C", "V A L U E")], 12), 0);
        var rows = Grid(Enumerable.Repeat(1f, names.Length).ToArray(), [1], 0);
        for (int index = 0; index < names.Length; index++)
        {
            string stat = names[index];
            int entryIndex = Entries.ToList().FindIndex(entry => entry.Action?.Kind == "select-slot" && entry.Action.SubjectId == stat);
            bool enabled = !interactive || entryIndex >= 0 && Entries[entryIndex].Enabled;
            int? current = stat == "HP" ? Character?.MaximumHp : Character?.Stats.TryGetValue(stat, out int value) == true ? value : null;
            string display = Number(current);
            if (preview?.TryGetValue(stat, out int projected) == true && projected != current)
            {
                display += " → " + projected;
            }
            UiElement row = new VectorAttributeRow(stat, Character?.Junctions.GetValueOrDefault(stat, "----") ?? "----", display, interactive && entryIndex == SelectedIndex) { Enabled = enabled };
            if (interactive && entryIndex >= 0)
            {
                int selected = entryIndex;
                var button = new UiButton { Id = Entries[entryIndex].Id, DrawBackground = false, HighlightSelection = false, PointerSize = 20 * TextScale, Enabled = enabled, Click = () => Activate(selected) };
                button.Children.Add(row);
                row = button;
            }
            Place(rows, row, index);
        }
        Place(table, rows, 1);
        return VectorPanel("A T T R I B U T E   J U N C T I O N S", table, 16);
    }

    private UiElement VectorHeader(string title, string help)
    {
        var header = VectorRows([12, 0, 19], [1], 1);
        Place(header, VectorLabel("M E N U", 10, true), 0);
        Place(header, VectorLabel(title, 40), 1);
        Place(header, VectorLabel(help, 13, true), 2);
        var branded = Grid([1], [70, 1], 14);
        branded.ColumnDefinitions[0] = UiGridLength.Pixel(70);
        branded.ColumnDefinitions[1] = UiGridLength.Star();
        Place(branded, new VectorCrest(), 0);
        Place(branded, header, 0, 1);
        return branded;
    }

    private UiElement VectorPrompts() => new VectorText($"{BindingText(ControlAction.MoveUp, CurrentControlDevice)}{BindingText(ControlAction.MoveDown, CurrentControlDevice)}  Navigate    {BindingText(ControlAction.Confirm, CurrentControlDevice)}  Select    {BindingText(ControlAction.Cancel, CurrentControlDevice)}  Back    F7  UI", 10 * TextScale, horizontalAlignment: UiAlignment.End);

    private static UiGrid VectorRows(float[] heights, float[] columns, float gap)
    {
        UiGrid grid = Grid(Enumerable.Repeat(1f, heights.Length).ToArray(), columns, gap);
        for (int index = 0; index < heights.Length; index++)
        {
            grid.RowDefinitions[index] = heights[index] == 0 ? UiGridLength.Star() : UiGridLength.Pixel(heights[index]);
        }
        return grid;
    }

    private UiElement VectorLabel(string text, float size, bool muted = false) => new VectorText(text, size * TextScale, muted);

    private static UiPanel VectorPanel(string? caption, UiElement content, float inset)
    {
        var panel = new UiPanel { Caption = caption, Padding = inset, CaptionHeight = caption is null ? 0 : 30, CaptionFontSize = 15 };
        panel.Children.Add(content);
        return panel;
    }
}
