using Memoria.FF8.Mods;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private string? modSettingsPackageId;
    private BehaviorModSetting[] modSettingsOriginal = [];
    private BehaviorModSetting[] modSettingsDraft = [];
    private IGrouping<string, int>[] modSettingsCategories = [];
    private readonly HashSet<string> modSettingsExpanded = new(StringComparer.Ordinal);
    private float modSettingsScrollOffset;
    private ModSettingsEditor? modSettingsEditor;

    private static bool IsMemoriaSettingsPackage(ModPackage package)
    {
        return IsBundledMemoriaMod(package) && package.Id.Equals(ModRuntime.MemoriaPackageId, StringComparison.OrdinalIgnoreCase);
    }

    private bool HasSelectedModSettings => selectedModId is not null && modService?.ReadBehaviorSettings(selectedModId).Count > 0;

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
            Help = SystemText("mods.settings.help", "Adjust the draft, then choose Save. Cancel discards your changes."),
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
        return SystemText($"mods.behavior.{setting.Behavior}.{setting.Parameter}", setting.Label);
    }

    private string ModSettingDescription(BehaviorModSetting setting)
    {
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
                string help = expanded ? SystemText("mods.settings.collapse", "Collapse") : SystemText("mods.settings.expand", "Expand");
                rows.Add(new(new("mod-settings-category-" + index, label, help, Action: new("mod-settings-category", category.Key))));
                if (!expanded)
                {
                    continue;
                }
            }
            foreach (int settingIndex in category)
            {
                BehaviorModSetting setting = modSettingsDraft[settingIndex];
                string? lockReason = ModSettingLockReason(setting);
                string description = ModSettingDescription(setting);
                string help = lockReason is null ? description : string.IsNullOrWhiteSpace(description) ? lockReason : lockReason + "\n" + description;
                rows.Add(new(new("mod-setting-" + settingIndex, ModSettingLabel(setting), help, Action: new("mod-settings-value"), Enabled: lockReason is null), settingIndex, category.Key.Length > 0));
            }
        }
        bool canSave = modSettingsDraft.Any(setting => ModSettingLockReason(setting) is null);
        rows.Add(new(new("save", SystemText("mods.settings.save", "Save"), "", Action: new("mod-settings-save"), Enabled: canSave)));
        rows.Add(new(new("cancel", SystemText("mods.settings.cancel", "Cancel"), "", Action: new("menu-back"))));
        return rows;
    }

    private void FocusModSettingsRow(bool focused, int index)
    {
        if (focused && ScreenId == "mod-settings" && SelectedIndex != index)
        {
            _selections[ScreenId] = index;
            SoundRequested?.Invoke(MenuSound.Move);
        }
    }

    private void ChangeModSettingsDraft(int index, int value)
    {
        BehaviorModSetting setting = modSettingsDraft[index];
        if (ModSettingLockReason(setting) is null)
        {
            modSettingsDraft[index] = setting with { Value = Math.Clamp(value, setting.Min, setting.Max) };
        }
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

    private bool ExecuteModSettingsAction(MenuAction action)
    {
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
                modSettingsOriginal = modService.ReadBehaviorSettings(packageId).ToArray();
                modSettingsDraft = (BehaviorModSetting[])modSettingsOriginal.Clone();
                if (modSettingsDraft.Length > 0)
                {
                    modSettingsPackageId = packageId;
                    modSettingsCategories = Enumerable.Range(0, modSettingsDraft.Length).GroupBy(index => modSettingsDraft[index].Category, StringComparer.Ordinal).ToArray();
                    modSettingsExpanded.Clear();
                    modSettingsScrollOffset = 0;
                    RegisterModSettingsScreen();
                    Push("mod-settings");
                }
            }
            return true;
        }
        if (action.Kind != "mod-settings-save")
        {
            return false;
        }
        if (ScreenId != "mod-settings" || modService is null || modSettingsPackageId is null)
        {
            return true;
        }
        if (modSettingsDraft.All(setting => ModSettingLockReason(setting) is not null))
        {
            return true;
        }
        BehaviorModSetting[] changes = modSettingsDraft.Where((setting, index) => setting.Value != modSettingsOriginal[index].Value && ModSettingLockReason(setting) is null).ToArray();
        if (changes.Length > 0)
        {
            ModOperationResult result = modService.SetBehaviorSettings(modSettingsPackageId, changes);
            if (!result.Success)
            {
                Message = result.Error ?? SystemText("mods.settings.save-failed", "The mod settings could not be saved.");
                _view = null;
                return true;
            }
        }
        Back();
        return true;
    }

    private sealed record ModSettingsRow(MenuEntry Entry, int? SettingIndex = null, bool Indented = false);
}
