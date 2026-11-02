using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class NewGameOptionsInputTests
{
    [Theory]
    [InlineData(Keys.D, -1, 0)]
    [InlineData(Keys.F, 1, 0)]
    [InlineData(Keys.H, 0, -1)]
    [InlineData(Keys.G, 0, 1)]
    public void Keyboard_escape_and_rotation_buttons_expose_distinct_page_edges(Keys key, int escapePage, int rotationPage)
    {
        KeyboardInput keyboard = new();
        var input = keyboard.Read(new KeyboardState(key), true);
        Assert.Equal(escapePage, keyboard.UiPaneFocus);
        Assert.Equal(rotationPage, keyboard.UiCharacterPage);
        Assert.False(input.Cancel);
        keyboard.Read(new KeyboardState(key), true);
        Assert.Equal(0, keyboard.UiPaneFocus);
        Assert.Equal(0, keyboard.UiCharacterPage);
        keyboard.Read(default, true);
        keyboard.Read(new KeyboardState(key), true);
        Assert.Equal(escapePage, keyboard.UiPaneFocus);
        Assert.Equal(rotationPage, keyboard.UiCharacterPage);
    }

    [Theory]
    [InlineData(Buttons.LeftTrigger, -1, 0)]
    [InlineData(Buttons.RightTrigger, 1, 0)]
    [InlineData(Buttons.LeftShoulder, 0, -1)]
    [InlineData(Buttons.RightShoulder, 0, 1)]
    public void Controller_escape_and_rotation_buttons_expose_distinct_page_edges(Buttons button, int escapePage, int rotationPage)
    {
        KeyboardInput keyboard = new();
        GamePadState released = new(Vector2.Zero, Vector2.Zero, 0, 0, Buttons.None);
        GamePadState pressed = new(Vector2.Zero, Vector2.Zero, button == Buttons.LeftTrigger ? 1 : 0, button == Buttons.RightTrigger ? 1 : 0, button);
        keyboard.Read(default, released, true);
        var input = keyboard.Read(default, pressed, true);
        Assert.Equal(escapePage, keyboard.UiPaneFocus);
        Assert.Equal(rotationPage, keyboard.UiCharacterPage);
        Assert.False(input.Cancel);
        keyboard.Read(default, pressed, true);
        Assert.Equal(0, keyboard.UiPaneFocus);
        Assert.Equal(0, keyboard.UiCharacterPage);
    }

    [Fact]
    public void Remapped_escape_pages_do_not_treat_cancel_as_escape()
    {
        KeyboardInput keyboard = new()
        {
            Profile = ControlsProfile.CreateDefault()
                .WithBinding(ControlAction.CameraLeft, ControlDevice.Keyboard, new InputChord([Keys.Q], []))
                .WithBinding(ControlAction.CameraRight, ControlDevice.Keyboard, new InputChord([Keys.W], []))
        };
        keyboard.Read(new KeyboardState(Keys.D, Keys.F), true);
        Assert.Equal(0, keyboard.UiPaneFocus);
        keyboard.Read(new KeyboardState(Keys.Q), true);
        Assert.Equal(-1, keyboard.UiPaneFocus);
        keyboard.Read(new KeyboardState(Keys.W), true);
        Assert.Equal(1, keyboard.UiPaneFocus);
        var input = keyboard.Read(new KeyboardState(Keys.C), true);
        Assert.True(input.Cancel);
        Assert.Equal(0, keyboard.UiPaneFocus);
        Assert.Equal(0, keyboard.UiCharacterPage);
    }
}
