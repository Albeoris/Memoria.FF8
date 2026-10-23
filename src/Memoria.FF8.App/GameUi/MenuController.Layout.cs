using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    public float TextScale
    {
        get
        {
            if (!IsFixtureContext && (_readPreferences is not null || IsHostSettingsScreen || ScreenId is "debug-room" or "debug-autoplay" || IsNamingScreen || UseHostControls))
            {
                UiPreferences preferences = _readPreferences?.Invoke() ?? new UiPreferences();
                return preferences.TextScale / 100f * preferences.UiFontSize / 24f;
            }
            string value = Data.Settings.GetValueOrDefault("Text scale", "100%").TrimEnd('%');
            float fontScale = (IsFixtureContext ? demoFontPreferences.UiFontSize : _readPreferences?.Invoke().UiFontSize ?? 24) / 24f;
            return fontScale * (float.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out float percent) ? Math.Clamp(percent / 100, 1, 1.5f) : 1);
        }
    }
    public UiView BuildView()
    {
        RefreshAutoPlayView();
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
        var root = new UiPanel { Id = "menu-root", DrawBackground = false, Padding = PresentationProvider.Padding(this), CaptionHeight = 0 };
        var anchored = new UiAnchorPanel();
        root.Children.Add(anchored);
        anchored.Children.Add(PresentationProvider.Compose(this));
        AddTutorialMessage(anchored);
        _view = new UiView(root) { ViewportMode = PresentationProvider.ViewportMode, DesignWidth = PresentationProvider.DesignWidth(this), DesignHeight = PresentationProvider.DesignHeight(this), Cancel = Back };
        _view.Resize(_view.DesignWidth, _view.DesignHeight);
        var focusables = FocusableDescendants(root).ToArray();
        string? selectedCharacterId = CharacterChoices.ElementAtOrDefault(CharacterSelectionIndex)?.Id;
        string commandListId = ScreenId == "shop-junk" && SelectedIndex < Data.Characters.Count ? "weapon-characters" : "commands";
        _view.Focus(IsSelectingCharacter ? focusables.FirstOrDefault(element => element.Id == selectedCharacterId) : focusables.OfType<UiList>().FirstOrDefault(list => list.Id == commandListId) ?? focusables.FirstOrDefault(element => element.Id == SelectedEntry?.Id) ?? focusables.FirstOrDefault());
        return _view;
    }

    internal UiElement BuildClassicContent()
    {
        var container = new UiElement();
        if (CurrentScreen.BuildContent is not null)
        {
            var custom = new UiPanel { DrawBackground = false, CaptionHeight = 0, Padding = new UiThickness(0) };
            container.Children.Add(custom);
            CurrentScreen.BuildContent(this, custom);
        }
        else if (CurrentScreen.Layout == MenuLayout.Main)
        {
            container.Children.Add(BuildMain());
        }
        else if (ScreenId.StartsWith("junction", StringComparison.Ordinal))
        {
            container.Children.Add(BuildJunction());
        }
        else if (ScreenId.StartsWith("magic", StringComparison.Ordinal))
        {
            container.Children.Add(BuildMagicPage());
        }
        else if (ScreenId.StartsWith("switch", StringComparison.Ordinal))
        {
            container.Children.Add(BuildNativeSwitch());
        }
        else if (ScreenId.StartsWith("item", StringComparison.Ordinal))
        {
            container.Children.Add(BuildItemPage());
        }
        else if (ScreenId.StartsWith("status", StringComparison.Ordinal))
        {
            container.Children.Add(BuildNativeStatus());
        }
        else if (ScreenId == "gf" || ScreenId.StartsWith("gf-", StringComparison.Ordinal))
        {
            container.Children.Add(BuildNativeGuardianForce());
        }
        else if (ScreenId is "card" or "card-detail")
        {
            container.Children.Add(BuildNativeCard());
        }
        else if (IsControlsScreen)
        {
            container.Children.Add(BuildControlsPage());
        }
        else if (IsNativeSecondaryScreen)
        {
            container.Children.Add(BuildNativeSecondary());
        }
        else
        {
            var page = Grid([Math.Max(1, TextScale), 8, 0.7f * Math.Max(1, TextScale)], [1]);
            container.Children.Add(page);
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
        return container;
    }

    private UiElement BuildStandard()
    {
        bool characterPage = CurrentScreen.Layout is MenuLayout.Character or MenuLayout.Junction;
        bool collectionDetail = ScreenId.StartsWith("gf-", StringComparison.Ordinal) || ScreenId == "card-detail";
        bool showSummary = characterPage || collectionDetail;
        var content = Grid(showSummary ? [1.6f, 5.8f] : [1], [1]);
        if (characterPage)
        {
            var summary = Grid([1, 1], [1, 6], 2);
            Place(summary, Portrait(Character), 0, 0, 2);
            Place(summary, Label($"Lv {Number(Character?.Level)}     HP {(Character is null ? "--" : Hp(Character))}     {Character?.Weapon ?? "--"}", 19), 0, 1);
            Place(summary, Label("Commands: " + (Character?.Commands.Count > 0 ? string.Join(" / ", Character.Commands) : "--"), 18), 1, 1);
            Place(content, Panel(Character?.Name ?? "--", summary), 0);
        }
        else if (collectionDetail)
        {
            bool card = ScreenId == "card-detail";
            var summary = Grid([1], [1, 5], 8);
            Place(summary, Artwork(card ? UiImageKind.Card : UiImageKind.GuardianForcePortrait, card ? Card?.NativeId : GuardianForce?.NativeId), 0, 0);
            Place(summary, Label(card ? Card?.Name ?? "--" : GuardianForce?.Name ?? "--", 28), 0, 1);
            Place(content, Panel(null, summary), 0);
        }
        var panes = Grid([1], [0.95f, 1.05f]);
        Place(content, panes, showSummary ? 1 : 0);
        Place(panes, Panel(null, Commands(30, 19)), 0, 0);
        bool showFontPreviews = ScreenId is "config-text" or "font-picker";
        Place(panes, Panel(ScreenId == "debug-room" ? "Selected preview" : CurrentScreen.Title, showFontPreviews ? BuildFontPreviews() : Details()), 0, 1);
        return content;
    }

    private UiElement BuildFontPreviews()
    {
        UiPreferences preferences = FontPreferences;
        var previews = Grid([1], [1, 1], 8);
        var menus = Grid([1, 2], [1], 4);
        var dialogue = Grid([1, 2], [1], 4);
        Place(menus, Label($"Menus: {preferences.UiFontSize}px / {preferences.TextScale}%", 15), 0);
        Place(menus, new FontPreviewElement(preferences.GetUiFontFamily(Presentation), preferences.UiFontSize * preferences.TextScale / 100f) { Id = "ui-font-preview" }, 1);
        Place(dialogue, Label($"Dialogue: {preferences.DialogueFontSize}px (field)", 15), 0);
        Place(dialogue, new FontPreviewElement(preferences.DialogueFontFamily, preferences.DialogueFontSize * 3) { Id = "dialogue-font-preview" }, 1);
        Place(previews, menus, 0);
        Place(previews, dialogue, 0, 1);
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
            bool guardianForce = CurrentScreen.Layout == MenuLayout.GuardianForceGrid;
            int? nativeId = guardianForce ? Data.GuardianForces.FirstOrDefault(force => force.Id == entry.Id)?.NativeId : Data.Cards.FirstOrDefault(card => card.Id == entry.Id)?.NativeId;
            Place(tile, Artwork(guardianForce ? UiImageKind.GuardianForcePortrait : UiImageKind.Card, nativeId), 1);
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
        Place(header, Panel(null, Label(IsFixtureContext ? "Preview / Fixture" : CurrentScreen.Title, CurrentScreen.Title.Length > 12 ? 18 : 26, UiTextFlow.Wrap)), 0, 1);
        return header;
    }

    private UiElement Help()
    {
        string help = ScreenId == "main" && !IsSelectingCharacter ? (SelectedEntry?.Label ?? "Main") + " Menu" : SelectedEntry?.Help ?? CurrentScreen.Help;
        return Panel("HELP", Label(Message.Length > 0 ? Message : help, IsOriginalMenuPage ? 22 : 18, UiTextFlow.Wrap));
    }

    private UiElement Footer()
    {
        if (IsNamingScreen)
        {
            return Panel(null, Label("Current game", 14));
        }
        string text = $"{(IsDemo ? "DEMO · " : "")}{Data.Location}";
        return Panel(null, Label(text, 14));
    }

    private UiList Commands(float rowHeight, float fontSize)
    {
        UiList list = new()
        {
            TextFlow = UiTextFlow.SingleLine,
            Id = "commands",
            Paginated = true,
            Focusable = Entries.Count > 0,
            Enabled = !IsSelectingCharacter,
            Items = Entries.Select(entry => entry.Label).ToList(),
            Images = Entries.Select(EntryImage).ToList(),
            TrailingImages = Entries.Select(EntryMarker).ToList(),
            EnabledItems = Entries.Select(entry => entry.Enabled).ToList(),
            HighlightSelection = !IsOriginalMenuPage,
            PointerSize = IsOriginalMenuPage ? 20 * TextScale : 10,
            TextInset = IsOriginalMenuPage ? 35 * TextScale : 23,
            SelectedIndex = SelectedIndex,
            RowHeight = rowHeight * TextScale,
            FontSize = fontSize * TextScale,
            SelectionChanged = index => Select(index, Entries.Count),
            Confirmed = Activate
        };
        ConfigureCommandColumns(list);
        return list;
    }

    private UiElement Details()
    {
        return new UiList { TextFlow = UiTextFlow.Wrap, Id = "details", Paginated = true, Items = CurrentScreen.Details(this).ToList(), FontSize = 17 * TextScale, RowHeight = 29 * TextScale, ShowSelection = false };
    }

    private UiElement Lines(IReadOnlyList<string> values, float fontSize)
    {
        return Label(string.Join("\n", values), fontSize, UiTextFlow.Wrap);
    }

    private UiElement Readouts(IReadOnlyList<string> values, float fontSize)
    {
        var rows = Grid(Enumerable.Repeat(1f, Math.Max(1, values.Count)).ToArray(), [1], 3);
        for (int index = 0; index < values.Count; index++)
        {
            Place(rows, Label(values[index], fontSize), index);
        }
        return rows;
    }

    private UiPanel Panel(string? caption, UiElement content)
    {
        var panel = new UiPanel { Caption = caption, Padding = IsOriginalMenuPage ? new UiThickness(8, 4) : new UiThickness(12, 8), CaptionHeight = string.IsNullOrEmpty(caption) ? 0 : (IsOriginalMenuPage ? 15 : 24) * TextScale, CaptionFontSize = UiTheme.DefaultCaptionFontSize * TextScale };
        panel.Children.Add(content);
        return panel;
    }

    private UiLabel Label(string value, float fontSize, UiTextFlow flow = UiTextFlow.SingleLine)
    {
        return new UiLabel { Text = value, FontSize = fontSize * TextScale, TextFlow = flow, VerticalAlignment = flow == UiTextFlow.Wrap ? UiAlignment.Stretch : UiAlignment.Center };
    }

    private UiImageSource? EntryImage(MenuEntry entry)
    {
        MenuAction? action = entry.Action;
        if (action is null)
        {
            return null;
        }
        if (action.Kind is "select-magic" or "junction-magic" or "junction-magic-reassign")
        {
            return Data.Magic.FirstOrDefault(magic => magic.Id == action.SubjectId && magic.CharacterId == Character?.Id)?.OwnerIcon;
        }
        if (action.Kind is "junction-command" or "junction-ability")
        {
            IReadOnlyList<MenuAbilityChoice> choices = action.Kind == "junction-command" ? Character?.AvailableCommands ?? [] : Character?.AvailableAbilities ?? [];
            return choices.FirstOrDefault(choice => choice.Id == action.SubjectId)?.Icon;
        }
        if (action.Kind is "select-command-slot" or "select-ability-slot")
        {
            IReadOnlyList<MenuAbilityChoice> choices = action.Kind == "select-command-slot" ? Character?.AvailableCommands ?? [] : Character?.AvailableAbilities ?? [];
            return choices.FirstOrDefault(choice => choice.Name == entry.Label)?.Icon;
        }
        string? characterId = action.Kind switch
        {
            "select-character" => action.SubjectId,
            "item-use" or "magic-use" or "party-switch" or "junction-exchange" or "magic-give" => action.TargetId,
            _ => null
        };
        if (characterId is not null && Data.Characters.FirstOrDefault(character => character.Id == characterId)?.NativeId is { } characterNativeId)
        {
            return new UiImageSource(UiImageKind.CharacterPortrait, characterNativeId);
        }
        if (action.Kind is "junction-gf" or "select-gf" && Data.GuardianForces.FirstOrDefault(force => force.Id == action.SubjectId)?.NativeId is { } forceId)
        {
            return new UiImageSource(UiImageKind.GuardianForcePortrait, forceId);
        }
        if (action.Kind == "select-card" && Data.Cards.FirstOrDefault(card => card.Id == action.SubjectId)?.NativeId is { } cardId)
        {
            return new UiImageSource(UiImageKind.Card, cardId);
        }
        return action.Kind is "select-item" or "item-battle" ? Data.Items.FirstOrDefault(item => item.Id == action.SubjectId)?.Icon : null;
    }

    private UiImageSource? EntryMarker(MenuEntry entry)
    {
        if (entry.Action?.Kind is "select-magic" or "junction-magic")
        {
            return Data.Magic.FirstOrDefault(magic => magic.Id == entry.Action.SubjectId && magic.CharacterId == Character?.Id)?.Icon;
        }
        return null;
    }

    private static UiImage Portrait(MenuCharacter? character)
    {
        UiImage image = Artwork(UiImageKind.CharacterPortrait, character?.NativeId);
        image.Id = "portrait-" + character?.Id;
        return image;
    }

    private static UiImage Artwork(UiImageKind kind, int? nativeId)
    {
        return new UiImage { Source = nativeId is { } id ? new UiImageSource(kind, id) : null };
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
