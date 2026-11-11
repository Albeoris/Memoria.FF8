using Memoria.FF8.Mods;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private string? modSettingsPackageId;
    private BehaviorModSetting[] modSettingsDraft = [];

    private bool HasSelectedModSettings => selectedModId is not null && modService?.ReadBehaviorSettings(selectedModId).Count > 0;

    private string? ModSettingLockReason(BehaviorModSetting setting)
    {
        return modSettingsPackageId is null ? null : modService?.GetBehaviorSettingLockReason(modSettingsPackageId, setting.Behavior, setting.Parameter);
    }

    private string? ModSettingsLockReason => modSettingsDraft.Select(ModSettingLockReason).FirstOrDefault(reason => reason is not null);

    private void RegisterModSettingsScreen()
    {
        string? packageName = modService?.View.Packages.FirstOrDefault(view => view.Package.Id.Equals(modSettingsPackageId, StringComparison.OrdinalIgnoreCase))?.Package.Name;
        Register(new MenuScreenDefinition
        {
            Id = "mod-settings",
            Title = packageName is null ? SystemText("mods.settings.title", "Mods · Settings") : FormatSystemText("mods.settings.package-title", "Mods · {0} · Settings", packageName),
            Help = SystemText("mods.settings.help", "Adjust the draft, then choose Save. Cancel discards your changes."),
            Entries = _ => ModSettingsEntries(),
            BuildContent = (_, root) => root.Children.Add(BuildModSettingsEditor())
        });
    }

    private string ModSettingLabel(BehaviorModSetting setting)
    {
        return SystemText($"mods.behavior.{setting.Behavior}.{setting.Parameter}", setting.Label);
    }

    private string ModSettingDescription(BehaviorModSetting setting)
    {
        string description = SystemText($"mods.behavior.{setting.Behavior}.{setting.Parameter}.description", setting.Description);
        string bounds = FormatSystemText("mods.settings.range", "Range: {0}{3}–{1}{3} · Step: {2}{3}", setting.Min, setting.Max, setting.Step, setting.Unit);
        return string.IsNullOrWhiteSpace(description) ? bounds : description + "\n" + bounds;
    }

    private IReadOnlyList<MenuEntry> ModSettingsEntries()
    {
        List<MenuEntry> entries = modSettingsDraft.Select((setting, index) => new MenuEntry("mod-setting-" + index, ModSettingLabel(setting), ModSettingLockReason(setting) ?? ModSettingDescription(setting), Action: new("mod-settings-value"), Enabled: ModSettingLockReason(setting) is null)).ToList();
        entries.Add(new("save", SystemText("mods.settings.save", "Save"), ModSettingsLockReason ?? "", Action: new("mod-settings-save"), Enabled: ModSettingsLockReason is null));
        entries.Add(new("cancel", SystemText("mods.settings.cancel", "Cancel"), "", Action: new("menu-back")));
        return entries;
    }

    private UiElement BuildModSettingsEditor()
    {
        float[] rows = [1, .. Enumerable.Repeat(1.5f, modSettingsDraft.Length), 0.8f, 0.8f, 1.2f];
        UiGrid grid = Grid(rows, [1]);
        Place(grid, Header(), 0);
        for (int index = 0; index < modSettingsDraft.Length; index++)
        {
            int selected = index;
            BehaviorModSetting setting = modSettingsDraft[index];
            bool locked = ModSettingLockReason(setting) is not null;
            UiNumericSlider slider = new()
            {
                Id = "mod-setting-" + index,
                Text = ModSettingLabel(setting),
                Max = setting.Max,
                Min = setting.Min,
                Step = setting.Step,
                Value = setting.Value,
                ValueFormat = "0'" + setting.Unit.Replace("'", "''", StringComparison.Ordinal) + "'",
                Enabled = !locked,
                ReadOnly = locked,
                FontSize = 24 * TextScale,
                PointerSize = 20 * TextScale,
                ValueWidth = 72 * TextScale,
                LabelWidthRatio = 0.52f,
                FocusChanged = focused => SelectConfigRow(focused, selected),
                ValueChanged = value =>
                {
                    if (ModSettingLockReason(setting) is not null)
                    {
                        return;
                    }
                    modSettingsDraft[selected] = setting with { Value = (int)Math.Clamp(value, setting.Min, setting.Max) };
                    _view = null;
                }
            };
            UiGrid option = Grid([1, 1], [1]);
            Place(option, slider, 0);
            UiLabel description = Label(ModSettingDescription(setting), 16, UiTextFlow.Wrap);
            description.Id = "mod-setting-description-" + index;
            Place(option, description, 1);
            Place(grid, Panel(null, option), index + 1);
        }
        for (int index = modSettingsDraft.Length; index < modSettingsDraft.Length + 2; index++)
        {
            int selected = index;
            MenuEntry entry = Entries[index];
            UiButton button = new()
            {
                Id = entry.Id,
                Text = entry.Label,
                Enabled = entry.Enabled,
                FontSize = 24 * TextScale,
                PointerSize = 20 * TextScale,
                FocusChanged = focused => SelectConfigRow(focused, selected),
                Click = () => Activate(selected)
            };
            Place(grid, Panel(null, button), index + 1);
        }
        UiLabel help = Label(ModSettingsLockReason ?? CurrentScreen.Help, 16, UiTextFlow.Wrap);
        help.Id = "mod-settings-help";
        Place(grid, Panel(null, help), rows.Length - 1);
        return grid;
    }

    private bool ExecuteModSettingsAction(MenuAction action)
    {
        if (action.Kind == "mod-settings-value")
        {
            return true;
        }
        if (action.Kind == "mod-settings-open")
        {
            if (selectedModId is not null && modService is not null)
            {
                modSettingsPackageId = selectedModId;
                modSettingsDraft = modService.ReadBehaviorSettings(selectedModId).ToArray();
                if (modSettingsDraft.Length > 0)
                {
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
        if (ModSettingsLockReason is { } lockReason)
        {
            Message = lockReason;
            _view = null;
            return true;
        }
        foreach (BehaviorModSetting setting in modSettingsDraft)
        {
            ModOperationResult result = modService.SetBehaviorSetting(modSettingsPackageId, setting.Behavior, setting.Parameter, setting.Value);
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
}
