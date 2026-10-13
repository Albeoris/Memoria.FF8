using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class InputTests
{
    [Fact]
    public void Confirm_and_cancel_rearm_only_after_release()
    {
        KeyboardInput keyboard = new();
        Assert.True(keyboard.Read(new KeyboardState(Keys.Enter), true).Confirm);
        Assert.False(keyboard.Read(new KeyboardState(Keys.Enter), true).Confirm);
        keyboard.Read(new KeyboardState(), true);
        Assert.True(keyboard.Read(new KeyboardState(Keys.Space), true).Confirm);
        Assert.False(keyboard.Read(new KeyboardState(Keys.Space), true).Confirm);
        keyboard.Read(new KeyboardState(), true);
        Runtime.InputFrame cancel = keyboard.Read(new KeyboardState(Keys.Escape), true);
        Assert.True(cancel.Cancel);
        Assert.False(cancel.Confirm);
        Assert.False(keyboard.Read(new KeyboardState(Keys.Escape), true).Cancel);
    }

    [Fact]
    public void Gameplay_bindings_preserve_held_movement_edge_actions_and_focus_safety()
    {
        var keyboard = new KeyboardInput();
        var down = new KeyboardState(Keys.Down, Keys.Enter, Keys.F5);
        Runtime.InputFrame first = keyboard.Read(down, true);
        Assert.Equal(1, first.Vertical);
        Assert.Equal(1, first.MenuVertical);
        Assert.True(first.Confirm);
        Assert.True(first.Save);
        Runtime.InputFrame held = keyboard.Read(down, true);
        Assert.Equal(1, held.Vertical);
        Assert.Equal(0, held.MenuVertical);
        Assert.False(held.Confirm);
        Assert.False(held.Save);
        Runtime.InputFrame inactive = keyboard.Read(new KeyboardState(Keys.Right, Keys.Space), false);
        Assert.Equal(0, inactive.Horizontal);
        Assert.False(inactive.Skip);
        Runtime.InputFrame reactivated = keyboard.Read(new KeyboardState(Keys.Right, Keys.Space), true);
        Assert.Equal(1, reactivated.Horizontal);
        Assert.False(reactivated.Skip);
        keyboard.Read(new KeyboardState(), true);
        Runtime.InputFrame skip = keyboard.Read(new KeyboardState(Keys.Escape), true);
        Assert.True(skip.Skip);
        Assert.True(skip.Cancel);
    }
}
