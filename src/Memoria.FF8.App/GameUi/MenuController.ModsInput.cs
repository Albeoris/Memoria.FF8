using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    internal bool IsCatalogSearchEditing => IsOpen && (ScreenId == "mods-catalog-search" || ScreenId == "mods-catalog" && catalogSearchEditing);
    internal bool IsModSearchEditing => IsCatalogSearchEditing || IsOpen && ScreenId == "mods-search";
    internal bool IsModManager => IsOpen && ScreenId is "mods" or "mods-create" or "mods-catalog" or "mods-downloads" or "mods-preview" or "mod-settings";

    private bool NavigateModManager(MenuInput input)
    {
        if (ScreenId == "mod-settings")
        {
            return NavigateModSettings(input);
        }
        if (ScreenId == "mods-preview")
        {
            return NavigateModPreview(input);
        }
        if (ScreenId == "mods-library-details")
        {
            if (input == MenuInput.Cancel)
            {
                Back();
            }
            else
            {
                BuildView();
                float amount = input switch
                {
                    MenuInput.Up => -36 * TextScale,
                    MenuInput.Down => 36 * TextScale,
                    MenuInput.PageUp => -catalogDetailsScroll!.Bounds.Height * 0.85f,
                    MenuInput.PageDown => catalogDetailsScroll!.Bounds.Height * 0.85f,
                    _ => 0
                };
                catalogDetailsScroll!.ScrollBy(amount);
            }
            return true;
        }
        if (ScreenId == "mods-catalog" && catalogSearchEditing)
        {
            if (input is MenuInput.Cancel or MenuInput.Confirm or MenuInput.Tab or MenuInput.ReverseTab)
            {
                catalogSearchEditing = false;
                modFocusedControlId = input == MenuInput.ReverseTab ? "library-refresh" : input == MenuInput.Tab ? "library-category" : "commands";
                _view = null;
            }
            return true;
        }
        if (IsModSearchEditing && (input == MenuInput.Cancel || input == MenuInput.Confirm && CurrentControlDevice == ControlDevice.Keyboard))
        {
            Back();
            return true;
        }
        if (!IsModManager)
        {
            return false;
        }
        if (IsLibraryScreen && input == MenuInput.Cancel && catalogNarrow && catalogInspectorOpen)
        {
            catalogInspectorOpen = false;
            modFocusedControlId = "commands";
            _view = null;
            return true;
        }
        if (input is MenuInput.PageLeft or MenuInput.PageRight)
        {
            SwitchModTab(input == MenuInput.PageLeft ? -1 : 1);
            return true;
        }
        if (input == MenuInput.Actions)
        {
            if (IsLibraryScreen)
            {
                OpenLibraryDetails();
            }
            else
            {
                Push("mods-actions");
            }
            return true;
        }
        UiView view = BuildView();
        if (input is MenuInput.FocusLeft or MenuInput.FocusRight or MenuInput.Alternate)
        {
            string target = input == MenuInput.FocusLeft ? "commands" : "mod-inspector";
            bool showInspector = target == "mod-inspector";
            if (ScreenId == "mods" && modInspectorOpen != showInspector)
            {
                modInspectorOpen = showInspector;
                _view = null;
                view = BuildView();
            }
            if (IsLibraryScreen)
            {
                catalogInspectorOpen = showInspector;
                target = showInspector ? "library-primary" : "commands";
                modFocusedControlId = target;
                _view = null;
                view = BuildView();
            }
            UiElement? control = FocusableDescendants(view.Root).FirstOrDefault(element => element.Id == target);
            if (control is not null)
            {
                view.Focus(control);
            }
            return true;
        }
        if (input == MenuInput.ReverseTab)
        {
            UiElement[] controls = FocusableDescendants(view.Root).Where(element => element.Visible && element.Enabled && !element.Bounds.IsEmpty).ToArray();
            if (controls.Length > 0)
            {
                int index = Array.IndexOf(controls, view.FocusedElement);
                view.Focus(controls[(index + controls.Length - 1) % controls.Length]);
            }
            return true;
        }
        if (view.FocusedElement?.Id is "installed-tab" or "catalog-tab" or "downloads-tab" or "create-tab" or "mods-installed-tab" or "mods-create-tab" or "mods-catalog-tab" or "mods-downloads-tab" && input is MenuInput.Left or MenuInput.Right)
        {
            if (modLibrary is not null || ScreenId == "mods" && input == MenuInput.Right || ScreenId == "mods-create" && input == MenuInput.Left)
            {
                SwitchModTab(input == MenuInput.Left ? -1 : 1);
            }
            return true;
        }
        UiKey? key = input switch
        {
            MenuInput.Up => UiKey.Up,
            MenuInput.Down => UiKey.Down,
            MenuInput.Left => UiKey.Left,
            MenuInput.Right => UiKey.Right,
            MenuInput.PageUp => UiKey.PageUp,
            MenuInput.PageDown => UiKey.PageDown,
            MenuInput.Confirm => UiKey.Confirm,
            MenuInput.Tab => UiKey.Tab,
            _ => null
        };
        if (key is { } command)
        {
            view.Key(command);
            return true;
        }
        return false;
    }

    private void SwitchModTab() => SwitchModTab(1);

    private void SwitchModTab(int direction)
    {
        string[] destinations = modLibrary is null ? ["mods", "mods-create"] : ["mods", "mods-catalog", "mods-downloads", "mods-create"];
        int index = Math.Max(0, Array.IndexOf(destinations, ScreenId));
        string destination = destinations[(index + direction + destinations.Length) % destinations.Length];
        if (_history.TryPeek(out string? previous) && previous == destination)
        {
            Back();
        }
        else
        {
            Push(destination);
        }
    }
}
