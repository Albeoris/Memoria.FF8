using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class ControlsCaptureIsolationTests
{
    [Fact]
    public void Returning_from_controller_settings_to_keyboard_keeps_keyboard_labels_and_capture()
    {
        ControlsProfile profile = ControlsProfile.CreateDefault();
        ControlDevice lastDevice = ControlDevice.Controller;
        MenuController menus = new();
        menus.ConnectControls(() => profile, next => profile = next, inputDevice: () => lastDevice);
        menus.Open("config-controls");
        menus.SelectEntry("config-controls", "config-controller");
        menus.Navigate(MenuInput.Confirm);
        menus.SelectEntry("config-controller", nameof(ControlAction.Menu));
        UiList controllerList = Assert.IsType<UiList>(menus.BuildView().FocusedElement);
        Assert.Contains(controllerList.SecondaryItems, text => text.Contains("North button", StringComparison.Ordinal));
        menus.Navigate(MenuInput.Cancel);
        menus.SelectEntry("config-controls", "config-keyboard");
        menus.Navigate(MenuInput.Confirm);
        UiList keyboardList = Assert.IsType<UiList>(menus.BuildView().FocusedElement);
        Assert.DoesNotContain(keyboardList.SecondaryItems, text => text.Contains("button", StringComparison.Ordinal));
        menus.SelectEntry("config-keyboard", nameof(ControlAction.Menu));
        menus.Navigate(MenuInput.Confirm);
        Assert.Contains(Descendants(menus.BuildView().Root).OfType<UiLabel>(), label => label.Text.EndsWith("(Keyboard)", StringComparison.Ordinal));
        menus.UpdateControlsCapture(default, default, true);
        lastDevice = ControlDevice.Keyboard;
        menus.BuildView();
        menus.UpdateControlsCapture(new(Keys.NumPad8), default, true);
        lastDevice = ControlDevice.Controller;
        menus.BuildView();
        menus.UpdateControlsCapture(default, default, true);
        Assert.Equal("config-keyboard", menus.ScreenId);
        Assert.Equal(Keys.NumPad8, Assert.Single(profile.GetBindings(ControlAction.Menu, ControlDevice.Keyboard)).Keys.Single());
        Assert.Equal(Buttons.Y, Assert.Single(profile.GetBindings(ControlAction.Menu, ControlDevice.Controller)).Buttons.Single());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Keyboard_remapping_finishes_with_or_without_an_unrelated_held_controller_button(bool controllerHeld, bool heldBeforeCapture)
    {
        ControlsProfile profile = ControlsProfile.CreateDefault();
        MenuController menus = new();
        menus.ConnectControls(() => profile, next => profile = next, inputDevice: () => ControlDevice.Controller);
        menus.Open("config-keyboard");
        foreach ((ControlAction action, Keys key) in new (ControlAction, Keys)[]
        {
            (ControlAction.MoveUp, Keys.W),
            (ControlAction.MoveDown, Keys.S),
            (ControlAction.MoveLeft, Keys.A),
            (ControlAction.MoveRight, Keys.D),
            (ControlAction.Confirm, Keys.NumPad4),
            (ControlAction.Cancel, Keys.NumPad5),
            (ControlAction.Menu, Keys.NumPad8)
        })
        {
            menus.SelectEntry("config-keyboard", action.ToString());
            menus.Navigate(MenuInput.Confirm);
            GamePadState held = controllerHeld ? new(Vector2.Zero, Vector2.Zero, 0, 0, Buttons.X) : default;
            menus.UpdateControlsCapture(default, heldBeforeCapture ? held : default, true);
            menus.UpdateControlsCapture(new(key), held, true);
            menus.UpdateControlsCapture(default, held, true);
            if (menus.ScreenId == "config-controls-conflict")
            {
                menus.Navigate(MenuInput.Down);
                menus.Navigate(MenuInput.Confirm);
            }
            Assert.False(menus.IsCapturingControls);
            Assert.Equal("config-keyboard", menus.ScreenId);
            Assert.Equal(key, Assert.Single(profile.GetBindings(action, ControlDevice.Keyboard)).Keys.Single());
            Assert.True(menus.UpdateControlsCapture(default, held, true));
            Assert.False(menus.UpdateControlsCapture(default, held, true));
        }
        UiList list = Assert.IsType<UiList>(menus.BuildView().FocusedElement);
        Assert.Contains("N8", list.SecondaryItems[menus.SelectedIndex]);
        Assert.Equal(Buttons.Y, Assert.Single(profile.GetBindings(ControlAction.Menu, ControlDevice.Controller)).Buttons.Single());
    }

    [Fact]
    public void Controller_capture_does_not_wait_for_unrelated_keyboard_keys()
    {
        ControlsProfile profile = ControlsProfile.CreateDefault();
        MenuController menus = new();
        menus.ConnectControls(() => profile, next => profile = next);
        menus.Open("config-controller");
        menus.SelectEntry("config-controller", nameof(ControlAction.QuickSave));
        menus.Navigate(MenuInput.Confirm);
        KeyboardState held = new(Keys.LeftShift);
        menus.UpdateControlsCapture(held, default, true);
        menus.UpdateControlsCapture(held, new(Vector2.Zero, Vector2.Zero, 0, 0, Buttons.LeftStick), true);
        menus.UpdateControlsCapture(held, new(Vector2.Zero, Vector2.Zero, 0, 0, Buttons.None), true);
        Assert.False(menus.IsCapturingControls);
        Assert.Equal(Buttons.LeftStick, Assert.Single(profile.GetBindings(ControlAction.QuickSave, ControlDevice.Controller)).Buttons.Single());
    }

    [Theory]
    [InlineData("config-keyboard")]
    [InlineData("config-controller")]
    public void Escape_cancels_before_the_selected_device_is_neutral(string screen)
    {
        ControlsProfile profile = ControlsProfile.CreateDefault();
        MenuController menus = new();
        menus.ConnectControls(() => profile, next => profile = next);
        menus.Open(screen);
        menus.SelectEntry(screen, nameof(ControlAction.Menu));
        menus.Navigate(MenuInput.Confirm);
        GamePadState held = new(Vector2.Zero, Vector2.Zero, 0, 0, Buttons.A);
        menus.UpdateControlsCapture(new(Keys.NumPad4), held, true);
        menus.UpdateControlsCapture(new(Keys.NumPad4, Keys.Escape), held, true);
        Assert.False(menus.IsCapturingControls);
        Assert.Equal(screen, menus.ScreenId);
        Assert.Equal(Keys.V, Assert.Single(profile.GetBindings(ControlAction.Menu, ControlDevice.Keyboard)).Keys.Single());
    }

    [Fact]
    public void Keyboard_controls_help_does_not_substitute_controller_buttons_for_unbound_details()
    {
        ControlsProfile profile = ControlsProfile.CreateDefault().WithBinding(ControlAction.MoveDown, ControlDevice.Keyboard, new([Keys.S], []), replaceConflicts: true);
        MenuController menus = new();
        menus.ConnectControls(() => profile, next => profile = next, inputDevice: () => ControlDevice.Controller);
        menus.Open("config-keyboard");
        string[] text = Descendants(menus.BuildView().Root).OfType<UiLabel>().Select(label => label.Text).ToArray();
        Assert.DoesNotContain(text, value => value.Contains("West button", StringComparison.Ordinal));
        Assert.Contains(text, value => value.Contains("unbound", StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<UiElement> Descendants(UiElement root)
    {
        yield return root;
        foreach (UiElement child in root.Children)
        {
            foreach (UiElement descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }
}
