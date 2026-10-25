using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class WindowModeHotkeyTests
{
    [Theory]
    [InlineData(Keys.LeftAlt)]
    [InlineData(Keys.RightAlt)]
    public void Shortcut_toggles_once_and_consumes_enter_until_released(Keys alt)
    {
        WindowModeHotkey hotkey = new();
        KeyboardState chord = new(alt, Keys.Enter);
        Assert.True(hotkey.Read(chord, true));
        Assert.False(hotkey.Read(chord, true));
        Assert.True(hotkey.Filter(chord).IsKeyUp(Keys.Enter));
        Assert.True(hotkey.Filter(chord).IsKeyDown(alt));
        Assert.False(hotkey.Read(new(Keys.Enter), true));
        Assert.True(hotkey.Filter(new(Keys.Enter)).IsKeyUp(Keys.Enter));
        hotkey.Read(default, true);
        Assert.True(hotkey.Read(chord, true));
        hotkey.Read(default, true);
        Assert.False(hotkey.Read(new(Keys.Enter), true));
        Assert.True(hotkey.Filter(new(Keys.Enter)).IsKeyDown(Keys.Enter));
    }

    [Fact]
    public void Focus_restore_and_adding_alt_to_held_enter_do_not_toggle()
    {
        WindowModeHotkey hotkey = new();
        KeyboardState chord = new(Keys.LeftAlt, Keys.Enter);
        Assert.False(hotkey.Read(chord, false));
        Assert.False(hotkey.Read(chord, true));
        hotkey.Read(default, true);
        Assert.False(hotkey.Read(new(Keys.Enter), true));
        Assert.False(hotkey.Read(chord, true));
        hotkey.Read(default, true);
        Assert.True(hotkey.Read(chord, true));
    }
}
