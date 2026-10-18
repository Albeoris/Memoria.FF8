using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class NamingKeyboardInputTests
{
    [Fact]
    public void Naming_commands_accept_mapped_confirm_and_suppress_global_shortcuts()
    {
        KeyboardInput keyboard = new() { NamingNavigationEnabled = true };

        InputFrame frame = keyboard.Read(new KeyboardState(Keys.X, Keys.F1, Keys.F2, Keys.F3, Keys.F4, Keys.F5, Keys.F6, Keys.F9, Keys.F10, Keys.V, Keys.A, Keys.Space), true);

        Assert.True(frame.Confirm);
        Assert.True(keyboard.UiConfirmRequested);
        Assert.False(keyboard.SpeedToggleRequested);
        Assert.False(keyboard.DebugRoomRequested);
        Assert.False(keyboard.BattleInstantVictoryRequested);
        Assert.False(keyboard.BattleDebugRequested);
        Assert.False(keyboard.BattleAdvanceTurnRequested);
        Assert.False(keyboard.GameMenuRequested);
        Assert.False(keyboard.PauseRequested);
        Assert.False(frame.Save);
        Assert.False(frame.QuickLoad);
        Assert.False(frame.SaveMenu);
        Assert.False(frame.LoadMenu);
    }

    [Fact]
    public void Physical_typing_suppresses_mapped_keyboard_actions_and_keeps_navigation()
    {
        KeyboardInput keyboard = new() { NamingNavigationEnabled = true, TextEntryEnabled = true };

        InputFrame frame = keyboard.Read(new KeyboardState(Keys.X, Keys.C, Keys.Up, Keys.Right), true);

        Assert.False(frame.Confirm);
        Assert.False(frame.Cancel);
        Assert.Equal(-1, frame.MenuVertical);
        Assert.Equal(1, frame.MenuHorizontal);
    }

    [Theory]
    [InlineData(Keys.Up, 0, -1)]
    [InlineData(Keys.Down, 0, 1)]
    [InlineData(Keys.Left, -1, 0)]
    [InlineData(Keys.Right, 1, 0)]
    public void Reserved_arrows_only_navigate(Keys arrow, int horizontal, int vertical)
    {
        ControlsProfile profile = ControlsProfile.CreateDefault();
        Assert.Throws<InvalidOperationException>(() => profile.WithBinding(ControlAction.Confirm, ControlDevice.Keyboard, new InputChord([arrow], []), true));
        KeyboardInput keyboard = new() { NamingNavigationEnabled = true, Profile = profile };

        InputFrame frame = keyboard.Read(new KeyboardState(arrow), true);

        Assert.False(frame.Confirm);
        Assert.False(keyboard.UiConfirmRequested);
        Assert.Equal(horizontal, frame.MenuHorizontal);
        Assert.Equal(vertical, frame.MenuVertical);
        Assert.Equal(0, keyboard.HeldFieldButtons);
    }

    [Fact]
    public void Naming_commands_keep_enter_escape_tab_and_page_fallbacks()
    {
        KeyboardInput keyboard = new() { NamingNavigationEnabled = true };

        InputFrame frame = keyboard.Read(new KeyboardState(Keys.Enter, Keys.Escape, Keys.Tab, Keys.PageDown), true);

        Assert.True(frame.Confirm);
        Assert.True(frame.Cancel);
        Assert.True(keyboard.UiTabRequested);
        Assert.Equal(1, frame.MenuPage);
    }

    [Fact]
    public void Text_entry_without_naming_navigation_keeps_existing_keyboard_filter()
    {
        KeyboardInput keyboard = new() { TextEntryEnabled = true };

        InputFrame frame = keyboard.Read(new KeyboardState(Keys.X, Keys.C, Keys.Enter, Keys.Up), true);

        Assert.True(frame.Confirm);
        Assert.False(keyboard.UiConfirmRequested);
        Assert.False(frame.Cancel);
        Assert.Equal(-1, frame.MenuVertical);
    }

    [Fact]
    public void Activating_physical_keyboard_suppresses_activation_key_text_until_release()
    {
        KeyboardInput keyboard = new() { NamingNavigationEnabled = true };
        Assert.True(keyboard.Read(new KeyboardState(Keys.X), true).Confirm);

        keyboard.SuppressHeldNameInput();
        keyboard.TextEntryEnabled = true;

        Assert.False(keyboard.AllowsNameText(Keys.X));
        Assert.False(keyboard.AllowsNameText(Keys.Up));
        Assert.True(keyboard.AllowsNameText(Keys.Y));
        keyboard.Read(new KeyboardState(Keys.X), true);
        Assert.False(keyboard.AllowsNameText(Keys.X));
        keyboard.Read(new KeyboardState(), true);
        Assert.True(keyboard.AllowsNameText(Keys.X));
    }

    [Fact]
    public void Leaving_physical_keyboard_does_not_activate_a_still_held_confirm_key()
    {
        KeyboardInput keyboard = new() { NamingNavigationEnabled = true, TextEntryEnabled = true };
        Assert.False(keyboard.Read(new KeyboardState(Keys.X), true).Confirm);

        keyboard.SuppressHeldNameInput();
        keyboard.TextEntryEnabled = false;

        Assert.False(keyboard.Read(new KeyboardState(Keys.X), true).Confirm);
        keyboard.Read(new KeyboardState(), true);
        Assert.True(keyboard.Read(new KeyboardState(Keys.X), true).Confirm);
    }
}
