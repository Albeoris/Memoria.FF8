using Memoria.FF8.Mods;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private string? modSettingsPackageId;
    private BehaviorModSetting[] modSettingsDraft = [];

    private bool HasSelectedModSettings => selectedModId is not null && modService?.ReadBehaviorSettings(selectedModId).Count > 0;

    private void RegisterModSettingsScreen()
    {
        string? packageName = modService?.View.Packages.FirstOrDefault(view => view.Package.Id.Equals(modSettingsPackageId, StringComparison.OrdinalIgnoreCase))?.Package.Name;
        Register(new MenuScreenDefinition
        {
            Id = "mod-settings",
            Title = packageName is null ? "Mods · Settings" : FormatSystemText("mods.settings.package-title", "Mods · {0} · Settings", packageName),
            Help = "Adjust the draft, then choose Save. Cancel discards your changes.",
            Entries = _ => ModSettingsEntries(),
            BuildContent = (_, root) => root.Children.Add(BuildModSettingsEditor())
        });
    }

    private IReadOnlyList<MenuEntry> ModSettingsEntries()
    {
        List<MenuEntry> entries = modSettingsDraft.Select((setting, index) => new MenuEntry("mod-setting-" + index, setting.Label, "", Action: new("mod-settings-value"))).ToList();
        entries.Add(new("save", SystemText("mods.settings.save", "Save"), "", Action: new("mod-settings-save")));
        entries.Add(new("cancel", SystemText("mods.settings.cancel", "Cancel"), "", Action: new("menu-back")));
        return entries;
    }

    private UiElement BuildModSettingsEditor()
    {
        float[] rows = Enumerable.Repeat(1f, modSettingsDraft.Length + 4).ToArray();
        UiGrid grid = Grid(rows, [1]);
        Place(grid, Header(), 0);
        for (int index = 0; index < modSettingsDraft.Length; index++)
        {
            int selected = index;
            BehaviorModSetting setting = modSettingsDraft[index];
            UiNumericSlider slider = new()
            {
                Id = "mod-setting-" + index,
                Text = setting.Label,
                Min = setting.Min,
                Max = setting.Max,
                Step = 1,
                Value = setting.Value,
                ValueFormat = "0'%'",
                FontSize = 24 * TextScale,
                PointerSize = 20 * TextScale,
                ValueWidth = 72 * TextScale,
                LabelWidthRatio = 0.52f,
                FocusChanged = focused => SelectConfigRow(focused, selected),
                ValueChanged = value =>
                {
                    modSettingsDraft[selected] = setting with { Value = (int)Math.Clamp(value, setting.Min, setting.Max) };
                    _view = null;
                }
            };
            Place(grid, Panel(null, slider), index + 1);
        }
        for (int index = modSettingsDraft.Length; index < modSettingsDraft.Length + 2; index++)
        {
            int selected = index;
            MenuEntry entry = Entries[index];
            UiButton button = new()
            {
                Id = entry.Id,
                Text = entry.Label,
                FontSize = 24 * TextScale,
                PointerSize = 20 * TextScale,
                FocusChanged = focused => SelectConfigRow(focused, selected),
                Click = () => Activate(selected)
            };
            Place(grid, Panel(null, button), index + 1);
        }
        Place(grid, Footer(), rows.Length - 1);
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
