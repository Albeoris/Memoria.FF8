using Memoria.FF8.Mods;
using Memoria.FF8.Resources.Localization;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private string? modSettingsPackageId;
    private BehaviorModSetting[] modSettings = [];
    private IGrouping<string, int>[] modSettingsCategories = [];
    private readonly HashSet<string> modSettingsExpanded = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> modSettingsComponentExpansion = [];
    private float modSettingsScrollOffset;
    private string? focusedModSettingsId;
    private ModSettingsEditor? modSettingsEditor;

    private static bool IsMemoriaSettingsPackage(ModPackage package)
    {
        return package.IsSettingsGroup || IsBundledMemoriaMod(package) && package.Id.Equals(ModRuntime.MemoriaPackageId, StringComparison.OrdinalIgnoreCase);
    }

    private bool HasSelectedModSettings => selectedModId is not null && (modService?.ReadBehaviorSettings(selectedModId).Count > 0 || selectedModId == ModRuntime.MemoriaPackageId && OfficialEntries.Any());

    private string? ModSettingLockReason(BehaviorModSetting setting)
    {
        return modSettingsPackageId is null ? null : modService?.GetBehaviorSettingLockReason(modSettingsPackageId, setting.Behavior, setting.Parameter);
    }

    private void RegisterModSettingsScreen()
    {
        string? packageName = modService?.View.Packages.FirstOrDefault(view => view.Package.Id.Equals(modSettingsPackageId, StringComparison.OrdinalIgnoreCase))?.Package.Name;
        Register(new MenuScreenDefinition
        {
            Id = "mod-settings",
            Title = packageName is null ? SystemText("mods.settings.screen-title", "Mods · Settings") : FormatSystemText("mods.settings.package-title", "Mods · {0} · Settings", packageName),
            Help = SystemText("mods.settings.auto-apply", "Changes are applied automatically."),
            Entries = _ => ModSettingsRows().Select(row => row.Entry).ToArray(),
            BuildContent = (_, root) =>
            {
                modSettingsEditor = new ModSettingsEditor(this);
                root.Children.Add(modSettingsEditor);
            }
        });
    }

    private string ModSettingLabel(BehaviorModSetting setting)
    {
        return setting.Descriptor?.Name ?? SystemText($"mods.behavior.{setting.Behavior}.{setting.Parameter}", setting.Label);
    }

    private string ModSettingDescription(BehaviorModSetting setting)
    {
        if (setting.Descriptor is { } descriptor)
        {
            return descriptor.DescriptionForLanguage(interfaceLanguage?.Invoke());
        }
        if (modSettingsPackageId == ModRuntime.MemoriaPackageId && setting is { Behavior: "component", Parameter: "Level-Based Stat Bonuses" })
        {
            return SystemText("mods.level-based-stat-bonuses.description", SystemTextCatalog.LevelBasedStatBonusesDescriptionForLanguage(interfaceLanguage?.Invoke()));
        }
        string description = SystemText($"mods.behavior.{setting.Behavior}.{setting.Parameter}.description", setting.Description);
        if (setting.Control == "toggle")
        {
            return description;
        }
        string bounds = FormatSystemText("mods.settings.range", "Range: {0}{3}–{1}{3} · Step: {2}{3}", setting.Min, setting.Max, setting.Step, setting.Unit);
        return string.IsNullOrWhiteSpace(description) ? bounds : description + "\n" + bounds;
    }

    private IReadOnlyList<ModSettingsRow> ModSettingsRows()
    {
        List<ModSettingsRow> rows = [];
        for (int index = 0; index < modSettingsCategories.Length; index++)
        {
            IGrouping<string, int> category = modSettingsCategories[index];
            if (category.Key.Length > 0)
            {
                bool expanded = modSettingsExpanded.Contains(category.Key);
                string label = SystemText("mods.settings.category." + category.Key.ToLowerInvariant(), category.Key);
                string help = ModSettingsExpansionLabel(expanded);
                rows.Add(new(new("mod-settings-category-" + (modLibrary is null ? index.ToString() : category.Key), label, help, Action: new("mod-settings-category", category.Key))));
                if (!expanded)
                {
                    continue;
                }
            }
            int depth = category.Key.Length > 0 ? 1 : 0;
            var children = category.Where(index => modSettings[index].ParentComponent is not null).ToLookup(index => modSettings[index].ParentComponent, StringComparer.Ordinal);
            foreach (int settingIndex in category)
            {
                BehaviorModSetting setting = modSettings[settingIndex];
                if (setting.ParentComponent is not null)
                {
                    continue;
                }
                bool? expanded = setting.Control == "toggle" && children.Contains(setting.Parameter) ? modSettingsComponentExpansion.GetValueOrDefault(SettingId(setting), setting.Value != 0) : null;
                rows.Add(ModSettingRow(settingIndex, depth, expanded));
                if (expanded == true)
                {
                    rows.AddRange(children[setting.Parameter].Select(childIndex => ModSettingRow(childIndex, depth + 1)));
                }
            }
        }
        if (modSettingsPackageId == ModRuntime.MemoriaPackageId)
        {
            AddOfficialSettingsRows(rows);
        }
        return rows;
    }

    private ModSettingsRow ModSettingRow(int index, int depth, bool? expanded = null)
    {
        BehaviorModSetting setting = modSettings[index];
        string? lockReason = ModSettingLockReason(setting);
        string description = ModSettingDescription(setting);
        string help = lockReason is null ? description : string.IsNullOrWhiteSpace(description) ? lockReason : lockReason + "\n" + description;
        if (expanded is bool isExpanded)
        {
            help += "\n" + CurrentBindingText(ControlAction.Card) + " · " + ModSettingsExpansionLabel(isExpanded);
        }
        string id = modLibrary is null && setting.Descriptor is null ? "mod-setting-" + index : SettingId(setting);
        return new(new(id, ModSettingLabel(setting), help, Action: new("mod-settings-value"), Enabled: lockReason is null), index, depth, expanded);
    }

    private string ModSettingsExpansionLabel(bool expanded)
    {
        return expanded ? SystemText("mods.settings.collapse", "Collapse") : SystemText("mods.settings.expand", "Expand");
    }

    private void FocusModSettingsRow(bool focused, int index)
    {
        if (!focused || ScreenId != "mod-settings")
        {
            return;
        }
        focusedModSettingsId = ModSettingsRows().ElementAtOrDefault(index)?.Entry.Id;
        if (SelectedIndex != index)
        {
            _selections[ScreenId] = index;
            SoundRequested?.Invoke(MenuSound.Move);
        }
    }

    private void ApplyModSetting(int index, int value, bool expandable = false)
    {
        if (modService is null || modSettingsPackageId is null)
        {
            return;
        }
        BehaviorModSetting setting = modSettings[index];
        if (setting.Value == value)
        {
            return;
        }
        ModOperationResult result = modService.SetBehaviorSetting(modSettingsPackageId, setting.Behavior, setting.Parameter, value);
        if (!result.Success)
        {
            Message = result.Error ?? SystemText("mods.settings.save-failed", "The mod settings could not be saved.");
            _view = null;
            return;
        }
        modSettings[index] = setting with { Value = value };
        if (Message.Length > 0)
        {
            Message = "";
            _view = null;
        }
        if (expandable)
        {
            SetModSettingsComponentExpanded(index, value != 0);
        }
    }

    private void SetModSettingsComponentExpanded(int index, bool expanded)
    {
        modSettingsComponentExpansion[SettingId(modSettings[index])] = expanded;
        _view = null;
    }

    private bool NavigateModSettings(MenuInput input)
    {
        UiView view = BuildView();
        ModSettingsEditor editor = modSettingsEditor!;
        switch (input)
        {
            case MenuInput.Up:
            case MenuInput.Down:
            case MenuInput.Tab:
            case MenuInput.ReverseTab:
                UiElement[] controls = editor.NavigationControls.Where(control => control.Enabled && !control.Bounds.IsEmpty).ToArray();
                if (controls.Length > 0)
                {
                    int index = Array.IndexOf(controls, view.FocusedElement);
                    int direction = input is MenuInput.Up or MenuInput.ReverseTab ? -1 : 1;
                    int next = index < 0 ? direction < 0 ? controls.Length - 1 : 0 : index + direction;
                    next = input is MenuInput.Tab or MenuInput.ReverseTab ? (next + controls.Length) % controls.Length : Math.Clamp(next, 0, controls.Length - 1);
                    view.Focus(controls[next]);
                }
                break;
            case MenuInput.PageUp:
            case MenuInput.PageDown:
                editor.Page(view, input == MenuInput.PageUp ? -1 : 1);
                break;
            case MenuInput.Left:
            case MenuInput.Right:
                UiKey directionKey = input == MenuInput.Left ? UiKey.Left : UiKey.Right;
                if (SelectedEntry is { Action: { Kind: "mod-settings-category" } category } entry && view.FocusedElement?.Id == entry.Id)
                {
                    if (modSettingsExpanded.Contains(category.SubjectId) != (input == MenuInput.Right))
                    {
                        Activate(SelectedIndex);
                    }
                }
                else
                {
                    view.Key(directionKey);
                }
                break;
            case MenuInput.Confirm:
                view.Key(UiKey.Confirm);
                break;
            case MenuInput.Alternate:
                if (ModSettingsRows().ElementAtOrDefault(SelectedIndex) is { SettingIndex: int settingIndex, Expanded: bool expanded } row && view.FocusedElement?.Id == row.Entry.Id)
                {
                    SetModSettingsComponentExpanded(settingIndex, !expanded);
                }
                break;
            case MenuInput.Cancel:
                Back();
                break;
        }
        return true;
    }

    private void HandleModSettingsWheel(int x, int y, int delta)
    {
        var point = BuildView().ToDesignPoint(x, y);
        if (modSettingsEditor?.Scroll is { } scroll && scroll.Bounds.Contains(point.X, point.Y))
        {
            scroll.ScrollBy(-delta / 120f * 45 * TextScale);
        }
    }

    private static string SettingId(BehaviorModSetting setting) => "mod-setting-" + setting.Behavior + "/" + setting.Parameter;

    private void RefreshModSettingsRows()
    {
        if (ScreenId != "mod-settings" || modSettingsPackageId is null || modService is null)
        {
            return;
        }
        string? selectedId = focusedModSettingsId ?? _view?.FocusedElement?.Id ?? SelectedEntry?.Id;
        modSettings = modService.ReadBehaviorSettings(modSettingsPackageId).ToArray();
        modSettingsCategories = Enumerable.Range(0, modSettings.Length).GroupBy(index => modSettings[index].Category, StringComparer.Ordinal).ToArray();
        int selected = ModSettingsRows().ToList().FindIndex(row => row.Entry.Id == selectedId);
        _selections[ScreenId] = Math.Max(0, selected);
        _view = null;
    }

    private bool ExecuteModSettingsAction(MenuAction action)
    {
        if (ExecuteOfficialSettingsAction(action))
        {
            return true;
        }
        if (action.Kind == "mod-settings-value")
        {
            return true;
        }
        if (action.Kind == "mod-settings-category")
        {
            if (ScreenId == "mod-settings")
            {
                string? selectedId = SelectedEntry?.Id;
                if (!modSettingsExpanded.Add(action.SubjectId))
                {
                    modSettingsExpanded.Remove(action.SubjectId);
                }
                List<ModSettingsRow> rows = ModSettingsRows().ToList();
                int selected = rows.FindIndex(row => row.Entry.Id == selectedId);
                _selections[ScreenId] = selected >= 0 ? selected : rows.FindIndex(row => row.Entry.Action?.SubjectId == action.SubjectId);
                _view = null;
            }
            return true;
        }
        if (action.Kind == "mod-settings-open")
        {
            string? packageId = action.SubjectId.Length > 0 ? action.SubjectId : selectedModId;
            if (packageId is not null && modService is not null)
            {
                modSettings = modService.ReadBehaviorSettings(packageId).ToArray();
                if (modSettings.Length > 0 || packageId == ModRuntime.MemoriaPackageId && OfficialEntries.Any())
                {
                    modSettingsPackageId = packageId;
                    modSettingsCategories = Enumerable.Range(0, modSettings.Length).GroupBy(index => modSettings[index].Category, StringComparer.Ordinal).ToArray();
                    modSettingsExpanded.Clear();
                    modSettingsComponentExpansion.Clear();
                    modSettingsScrollOffset = 0;
                    focusedModSettingsId = null;
                    RegisterModSettingsScreen();
                    Push("mod-settings");
                }
            }
            return true;
        }
        return false;
    }

    private sealed record ModSettingsRow(MenuEntry Entry, int? SettingIndex = null, int Depth = 0, bool? Expanded = null);
}
