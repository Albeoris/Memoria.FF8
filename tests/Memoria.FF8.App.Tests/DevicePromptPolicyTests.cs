using Memoria.FF8.GameUi;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class DevicePromptPolicyTests
{
    [Fact]
    public void Keyboard_prompts_never_fall_back_to_controller_bindings()
    {
        KeyboardInput input = new() { Profile = ControlsProfile.CreateDefault().WithoutBinding(ControlAction.Card, ControlDevice.Keyboard) };
        Assert.NotEmpty(input.Profile.GetBindings(ControlAction.Card, ControlDevice.Controller));
        Assert.Equal("Unbound", input.BindingHint(ControlAction.Card));
        Assert.Equal("Unbound", input.BindingPrompt(ControlAction.Card));
    }

    [Fact]
    public void Controller_prompts_fall_back_to_keyboard_when_unassigned()
    {
        KeyboardInput input = new();
        string keyboardHint = input.BindingHint(ControlAction.SpeedToggle);
        string keyboardPrompt = input.BindingPrompt(ControlAction.SpeedToggle);
        input.Read(default, Pad(), true);
        input.Read(default, Pad(Buttons.A), true);
        Assert.Equal(ControlDevice.Controller, input.LastInputDevice);
        Assert.Equal(keyboardHint, input.BindingHint(ControlAction.SpeedToggle));
        Assert.Equal(keyboardPrompt, input.BindingPrompt(ControlAction.SpeedToggle));
    }

    [Fact]
    public void Held_sticks_do_not_take_prompts_from_keyboard_and_each_stick_rearms_independently()
    {
        KeyboardInput input = new();
        input.Read(default, Pad(), true);
        GamePadState left = Sticks(new Vector2(0.8f, 0), Vector2.Zero);
        input.Read(default, left, true);
        Assert.Equal(ControlDevice.Controller, input.LastInputDevice);
        input.Read(new KeyboardState(Keys.X), left, true);
        Assert.Equal(ControlDevice.Keyboard, input.LastInputDevice);
        input.Read(default, Sticks(new Vector2(0.9f, 0.1f), Vector2.Zero), true);
        Assert.Equal(ControlDevice.Keyboard, input.LastInputDevice);
        GamePadState both = Sticks(new Vector2(0.9f, 0.1f), new Vector2(0, 0.8f));
        input.Read(default, both, true);
        Assert.Equal(ControlDevice.Controller, input.LastInputDevice);
        input.Read(new KeyboardState(Keys.X), both, true);
        input.Read(default, both, true);
        Assert.Equal(ControlDevice.Keyboard, input.LastInputDevice);
        input.Read(default, Pad(), true);
        input.Read(default, left, true);
        Assert.Equal(ControlDevice.Controller, input.LastInputDevice);
    }

    [Fact]
    public void Held_buttons_do_not_take_prompts_back_but_a_new_button_does()
    {
        KeyboardInput input = new();
        input.Read(default, Pad(), true);
        input.Read(default, Pad(Buttons.A), true);
        input.Read(new KeyboardState(Keys.X), Pad(Buttons.A), true);
        input.Read(default, Pad(Buttons.A), true);
        Assert.Equal(ControlDevice.Keyboard, input.LastInputDevice);
        input.Read(default, Pad(Buttons.A, Buttons.B), true);
        Assert.Equal(ControlDevice.Controller, input.LastInputDevice);
        input.Read(default, default, true);
        Assert.Equal(ControlDevice.Keyboard, input.LastInputDevice);
    }

    [Fact]
    public void Tutorial_prompts_follow_the_same_asymmetric_fallback_policy()
    {
        ControlsProfile profile = ControlsProfile.CreateDefault().WithoutBinding(ControlAction.Card, ControlDevice.Keyboard);
        ControlDevice device = ControlDevice.Keyboard;
        MenuController menus = new();
        menus.ConnectReadOnlyTutorialControls(() => profile, () => ControllerPresentation.Generic, () => device);
        Assert.Equal("Unbound", ButtonPromptText.PlainText(menus.FormatNativeTutorialText("{Key Cards}")));
        device = ControlDevice.Controller;
        Assert.NotEqual("Unbound", ButtonPromptText.PlainText(menus.FormatNativeTutorialText("{Key Cards}")));
        profile = ControlsProfile.CreateDefault().WithoutBinding(ControlAction.Card, ControlDevice.Controller);
        Assert.Equal("S", ButtonPromptText.PlainText(menus.FormatNativeTutorialText("{Key Cards}")));
    }

    private static GamePadState Pad(params Buttons[] buttons) => new(Vector2.Zero, Vector2.Zero, 0, 0, buttons);

    private static GamePadState Sticks(Vector2 left, Vector2 right) => new(left, right, 0, 0, Buttons.None);
}
