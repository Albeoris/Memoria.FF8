using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private UiElement BuildBattleLoadoutView()
    {
        if (battleLoadoutEditor is not { Draft: var draft, Characters: var roster })
        {
            return JunctionPanel(JunctionLabel(Message.Length > 0 ? Message : BattleLoadoutEditorEntryHelp));
        }

        float scale = TextScale;
        UiGrid page = JunctionRows([Math.Max(54, 48 * scale), 0, 72 * scale], [1], 12);
        page.Id = BattleLoadoutScreen;
        BattleLoadoutEditorCharacter actor = Array.Find(roster, entry => entry.CharacterId == battleLoadoutCharacter)!;
        Place(page, BuildBattleLoadoutHeader(actor), 0);

        UiGrid columns = JunctionRows([0], [360, 450, 394], 12);
        BattleLoadoutEditorWeapon? equipped = BattleLoadoutEquippedWeapon();
        Place(columns, BuildBattleLoadoutWeapons(), 0, 0);
        Place(columns, BuildBattleLoadoutModel(draft, equipped), 0, 1);
        Place(columns, BuildBattleLoadoutLimits(draft, equipped), 0, 2);
        if (battleLoadoutPreparation is not null)
        {
            columns.Enabled = false;
        }
        Place(page, columns, 1);
        Place(page, BuildBattleLoadoutFooter(), 2);
        if (battleLoadoutSwap is { } swap)
        {
            page.Enabled = false;
            UiElement layers = new();
            layers.Children.Add(page);
            layers.Children.Add(BuildBattleLoadoutSwap(swap));
            return layers;
        }
        return page;
    }

    private UiElement BuildBattleLoadoutHeader(BattleLoadoutEditorCharacter actor)
    {
        MenuCharacter? native = Data.Characters.FirstOrDefault(character => character.NativeId == actor.CharacterId);
        UiGrid header = new() { Gap = 12 };
        header.RowDefinitions.Add(UiGridLength.Star());
        header.ColumnDefinitions.Add(UiGridLength.Pixel(52));
        header.ColumnDefinitions.Add(UiGridLength.Star(360));
        header.ColumnDefinitions.Add(UiGridLength.Star(380));
        header.ColumnDefinitions.Add(UiGridLength.Pixel(180));

        UiImage portrait = BattleLoadoutPortrait(actor.CharacterId, native);
        portrait.Id = "loadout-character-portrait";
        Place(header, portrait, 0);
        UiGrid identity = JunctionRows([17 * TextScale, 0], [1]);
        Place(identity, JunctionLabel(LoadoutText("title", "Weapons & Limit Breaks"), 12, true), 0);
        Place(identity, JunctionLabel($"{actor.Name}   {J("level", "Lv")} {Number(native?.Level)}", 23), 1);
        Place(header, identity, 0, 1);

        UiGrid hp = JunctionRows([0, 5], [1], 4);
        Place(hp, JunctionLabel("HP  " + (native is null ? "—" : Hp(native)), 22), 0);
        Place(hp, new UiProgressBar { Min = 0, Max = Math.Max(1, native?.MaximumHp ?? 1), Value = native?.CurrentHp ?? 0 }, 1);
        Place(header, hp, 0, 2);

        UiGrid navigation = JunctionRows([0], [1, 1], 8);
        foreach (int direction in new[] { -1, 1 })
        {
            if (BattleLoadoutAdjacentCharacter(direction) is not { } neighbor)
            {
                continue;
            }
            string id = direction < 0 ? "previous-character" : "next-character";
            UiGrid tile = JunctionRows([0], direction < 0 ? [32, 48] : [48, 32], 2);
            Place(tile, JunctionLabel(direction < 0 ? "⟵" : "⟶", 19), 0, direction < 0 ? 0 : 1);
            UiImage neighborPortrait = BattleLoadoutPortrait(neighbor.CharacterId);
            neighborPortrait.Id = id + "-portrait";
            Place(tile, neighborPortrait, 0, direction < 0 ? 1 : 0);
            UiButton button = BattleLoadoutButton(id, () => Execute(new("loadout-cycle", Value: direction < 0 ? "-1" : "1")));
            button.Enabled = battleLoadoutPreparation is null;
            button.Children.Add(tile);
            Place(navigation, button, 0, direction < 0 ? 0 : 1);
        }
        Place(header, navigation, 0, 3);
        return header;
    }

    private UiImage BattleLoadoutPortrait(int characterId, MenuCharacter? character = null)
    {
        character ??= Data.Characters.FirstOrDefault(entry => entry.NativeId == characterId);
        return new() { Source = character?.Portrait ?? new UiImageSource(UiImageKind.CharacterPortrait, characterId) };
    }

    private UiElement BuildBattleLoadoutWeapons()
    {
        BattleLoadoutWeaponRow[] rows = BattleLoadoutWeaponRows();
        UiGrid content = JunctionRows([34 * TextScale, 0], [1], 4);
        Place(content, JunctionLabel(LoadoutText("scene.weapon-types", "Weapon types"), 18), 0);
        int selected = Array.FindIndex(rows, row => row.Entry.Id == battleLoadoutWeaponFocus);
        if (selected < 0)
        {
            BattleLoadoutEditorWeapon? equipped = BattleLoadoutEquippedWeapon();
            int equippedType = equipped is null ? -1 : Array.FindIndex(rows, row => row.Entry.Id == BattleLoadoutTypeId(BattleLoadoutWeaponType(equipped)));
            selected = Math.Max(0, equippedType);
        }
        battleLoadoutWeaponFocus = rows.ElementAtOrDefault(selected).Entry?.Id ?? "";
        UiList list = new()
        {
            Id = BattleLoadoutWeaponListId,
            Items = rows.Select(row => row.Entry.Action?.Kind == "loadout-type" ? row.Entry.Label : "          " + row.Entry.Label).ToArray(),
            Images = rows.Select(row => row.Icon).ToArray(),
            TrailingImages = rows.Select(row => row.OwnerPortrait).ToArray(),
            ItemTextColor = index => rows[index].Equipped ? AbilityActivationColor(true) : null,
            SecondaryItems = rows.Select(row => row.Status).ToArray(),
            AutoSecondaryWidth = true,
            SecondaryAlignment = UiAlignment.End,
            TextInset = 12,
            FontSize = 15 * TextScale,
            RowHeight = 36 * TextScale,
            AutoRowHeight = false,
            KeepSelectionVisibleOnDraw = false,
            SelectedIndex = selected,
            FocusChanged = focused => SetBattleLoadoutFocus(focused, BattleLoadoutWeaponListId),
            SelectionChanged = index => battleLoadoutWeaponFocus = rows.ElementAtOrDefault(index).Entry?.Id ?? "",
            Confirmed = index =>
            {
                if (rows.ElementAtOrDefault(index).Entry?.Action is { } action)
                {
                    SoundRequested?.Invoke(MenuSound.Confirm);
                    Execute(action);
                }
            }
        };
        battleLoadoutWeaponList = list;
        Place(content, list, 1);
        return JunctionPanel(content);
    }

    private UiElement BuildBattleLoadoutModel(BattleLoadoutDraft draft, BattleLoadoutEditorWeapon? equipped)
    {
        float scale = TextScale;
        UiGrid content = JunctionRows([0, 48 * scale], [1], 5);
        Place(content, new BattleLoadoutDrawing((canvas, theme, bounds) => DrawBattleLoadoutPreview(canvas, theme, bounds, scale, draft, battleLoadoutCharacter)) { Id = "loadout-character-model" }, 0);
        UiGrid equippedRow = JunctionRows([0], [1, 45, 4, 270, 1], 4);
        if (equipped is not null)
        {
            Place(equippedRow, new UiImage { Source = new UiImageSource(UiImageKind.WeaponVariant, equipped.WeaponId) }, 0, 1);
            Place(equippedRow, JunctionLabel(equipped.Name, 18), 0, 3);
        }
        Place(content, equippedRow, 1);
        return JunctionPanel(content);
    }

    private UiElement BuildBattleLoadoutLimits(BattleLoadoutDraft draft, BattleLoadoutEditorWeapon? equipped)
    {
        UiGrid content = JunctionRows([34 * TextScale, 0], [1], 4);
        Place(content, JunctionLabel(LoadoutText("scene.limit-breaks", "Limit Breaks"), 18), 0);
        BattleLimitFamily[] families = Enum.GetValues<BattleLimitFamily>();
        HashSet<BattleLimitFamily> active = draft.ResolveFamilies(battleLoadoutCharacter).ToHashSet();
        UiGrid choices = Grid(Enumerable.Repeat(1f, families.Length).ToArray(), [1], 1);
        for (int row = 0; row < families.Length; row++)
        {
            BattleLimitFamily family = families[row];
            MenuEntry entry = BattleLoadoutLimitEntry(family);
            UiButton button = BattleLoadoutButton(entry.Id, () => Execute(entry.Action!));
            UiGrid tile = JunctionRows([0], [33, 32, 248, 48], 5);
            bool assigned = active.Contains(family);
            bool supported = BattleLoadoutSupportsLimit(equipped, family);
            Place(tile, new BattleLoadoutDrawing((canvas, theme, bounds) => DrawBattleLoadoutCheckbox(canvas, theme, bounds, assigned)) { Id = entry.Id + "-check" }, 0);
            UiImage portrait = BattleLoadoutPortrait(BattleLimitFamilyRules.SourceCharacter(family));
            portrait.Id = entry.Id + "-portrait";
            portrait.Margin = new UiThickness(0, 3);
            Place(tile, portrait, 0, 1);
            UiLabel label = JunctionLabel(entry.Label, 16);
            if (assigned)
            {
                label.Color = AbilityActivationColor(supported);
            }
            Place(tile, label, 0, 2);
            if (WeaponIconAtlas.RequiredWeaponOwner(family) is int owner)
            {
                UiColor border = supported ? new(114, 224, 140) : new(255, 120, 124);
                Place(tile, new BattleLoadoutDrawing((canvas, _, bounds) => DrawBattleLoadoutRequirement(canvas, bounds, owner, border)) { Id = entry.Id + "-weapon" }, 0, 3);
            }
            button.Children.Add(tile);
            button.Margin = new UiThickness(10, 0, 2, 0);
            Place(choices, button, row);
        }
        Place(content, choices, 1);
        return JunctionPanel(content);
    }

    private static void DrawBattleLoadoutRequirement(IUiCanvas canvas, UiRect bounds, int owner, UiColor border)
    {
        float size = Math.Min(bounds.Height - 3, bounds.Width - 3);
        if (size <= 0)
        {
            return;
        }
        UiRect icon = new(bounds.X + (bounds.Width - size) / 2, bounds.Y + (bounds.Height - size) / 2, size, size);
        canvas.Fill(icon, border);
        canvas.Fill(icon.Inset(1.5f), new UiColor(39, 39, 48));
        canvas.Image(new UiImageSource(UiImageKind.Weapon, owner), icon.Inset(3));
    }

    private static void DrawBattleLoadoutCheckbox(IUiCanvas canvas, UiTheme theme, UiRect bounds, bool assigned)
    {
        float size = Math.Min(bounds.Width, bounds.Height) * 0.55f;
        UiRect box = new(bounds.X + (bounds.Width - size) / 2, bounds.Y + (bounds.Height - size) / 2, size, size);
        canvas.Fill(box, assigned ? theme.Foreground : theme.Muted);
        canvas.Fill(box.Inset(1.25f), theme.BorderDark);
        if (assigned)
        {
            canvas.Line(box.X + size * 0.2f, box.Y + size * 0.52f, box.X + size * 0.43f, box.Y + size * 0.74f, theme.Foreground, 1.5f);
            canvas.Line(box.X + size * 0.43f, box.Y + size * 0.74f, box.X + size * 0.79f, box.Y + size * 0.25f, theme.Foreground, 1.5f);
        }
    }

    private UiElement BuildBattleLoadoutFooter()
    {
        float scale = TextScale;
        UiGrid content = JunctionRows([0, 0], [1], 4);
        if (battleLoadoutProgress is { } progress)
        {
            Place(content, JunctionLabel(LoadoutText("scene.preparing", "Preparing battle animations…"), 17), 0);
            Place(content, new BattleLoadoutDrawing((canvas, theme, bounds) =>
            {
                BattleLoadoutProgress.Count count = progress.Current;
                float ratio = Math.Clamp(count.Completed / (float)Math.Max(1, count.Total), 0, 1);
                UiRect track = new(bounds.X + 8, bounds.Y + bounds.Height / 2, bounds.Width * 0.55f, 5 * scale);
                theme.Progress(canvas, track, ratio, true);
                canvas.Text($"{count.Completed} / {count.Total}", 15 * scale, new UiRect(track.Right + 10, bounds.Y, bounds.Width - track.Width - 18, bounds.Height), theme.Foreground, UiAlignment.Center);
            }) { Id = "loadout-apply-progress" }, 1);
        }
        else
        {
            string confirm = CurrentBindingText(ControlAction.Confirm);
            string cancel = CurrentBindingText(ControlAction.Cancel);
            string previous = CurrentBindingText(ControlAction.RotateLeft);
            string next = CurrentBindingText(ControlAction.RotateRight);
            string controls = battleLoadoutExpandedType.HasValue
                ? LoadoutFormat("scene.dropdown-controls", "{0} Choose weapon   ·   {1} / {2} Change character   ·   {3} Collapse list", confirm, previous, next, cancel)
                : LoadoutFormat("scene.controls", "{0} Select   ·   {1} / {2} Change character   ·   {3} Apply and return", confirm, previous, next, cancel);
            Place(content, JunctionLabel(controls, 15, true), 0);
            Dictionary<string, string> help = BattleLoadoutEntries().ToDictionary(entry => entry.Id, entry => entry.Help);
            Place(content, new BattleLoadoutDrawing((canvas, theme, bounds) =>
            {
                string detail = Message;
                if (detail.Length == 0)
                {
                    detail = help.GetValueOrDefault(SelectedBattleLoadoutEntryId) ?? "";
                }
                canvas.Text(detail, 15 * scale, bounds, theme.Foreground, UiAlignment.Center, UiTextFlow.SingleLine);
            }) { Id = "loadout-help" }, 1);
        }
        return JunctionPanel(content);
    }

    private UiButton BattleLoadoutButton(string id, Action click)
    {
        return new()
        {
            Id = id,
            DrawBackground = false,
            PointerSize = 8,
            FocusChanged = focused => SetBattleLoadoutFocus(focused, id),
            Click = () =>
            {
                SoundRequested?.Invoke(MenuSound.Confirm);
                click();
            }
        };
    }

    private void SetBattleLoadoutFocus(bool focused, string id)
    {
        if (focused)
        {
            battleLoadoutFocus = id;
        }
    }

    private sealed class BattleLoadoutDrawing(Action<IUiCanvas, UiTheme, UiRect> paint) : UiElement
    {
        protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
        {
            paint(canvas, theme, Bounds);
        }
    }
}
