using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    internal bool IsModSearchEditing => IsOpen && ScreenId == "mods-search";
    internal bool IsModManager => IsOpen && ScreenId is "mods" or "mods-create";

    private bool NavigateModManager(MenuInput input)
    {
        if (IsModSearchEditing && (input == MenuInput.Cancel || input == MenuInput.Confirm && CurrentControlDevice == ControlDevice.Keyboard))
        {
            Back();
            return true;
        }
        if (!IsModManager)
        {
            return false;
        }
        if (input is MenuInput.PageLeft or MenuInput.PageRight)
        {
            SwitchModTab();
            return true;
        }
        if (input == MenuInput.Actions)
        {
            Push("mods-actions");
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
        if (view.FocusedElement?.Id is "installed-tab" or "create-tab" or "mods-installed-tab" or "mods-create-tab" && input is MenuInput.Left or MenuInput.Right)
        {
            if (ScreenId == "mods" && input == MenuInput.Right || ScreenId == "mods-create" && input == MenuInput.Left)
            {
                SwitchModTab();
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

    private void SwitchModTab()
    {
        string destination = ScreenId == "mods" ? "mods-create" : "mods";
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
