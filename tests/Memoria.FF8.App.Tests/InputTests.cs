using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class InputTests
{
    [Fact]
    public void Enter_and_space_share_one_mapped_confirm_edge()
    {
        KeyboardInput keyboard = new();
        Assert.True(keyboard.Read(new KeyboardState(Keys.Enter), true).Confirm);
        Assert.False(keyboard.Read(new KeyboardState(Keys.Enter, Keys.Space), true).Confirm);
        Assert.False(keyboard.Read(new KeyboardState(Keys.Space), true).Confirm);
        Assert.Equal((ushort)0x40, keyboard.HeldFieldButtons);
        keyboard.Read(new KeyboardState(), true);
        Assert.True(keyboard.Read(new KeyboardState(Keys.Space), true).Confirm);
    }

    [Fact]
    public void Field_button_word_preserves_holds_and_clears_when_unfocused()
    {
        KeyboardInput keyboard = new();
        keyboard.Read(new KeyboardState(Keys.Up, Keys.Right, Keys.Enter, Keys.Escape), true);
        Assert.Equal((ushort)0x3050, keyboard.HeldFieldButtons);
        Runtime.InputFrame repeated = keyboard.Read(new KeyboardState(Keys.Up, Keys.Right, Keys.Enter, Keys.Escape), true);
        Assert.Equal((ushort)0x3050, keyboard.HeldFieldButtons);
        Assert.False(repeated.Confirm);
        Assert.False(repeated.Cancel);
        keyboard.Read(new KeyboardState(Keys.Up, Keys.Right, Keys.Enter, Keys.Escape), false);
        Assert.Equal((ushort)0, keyboard.HeldFieldButtons);
        keyboard.Read(new KeyboardState(Keys.Down, Keys.Left, Keys.Space), true);
        Assert.Equal((ushort)0xC040, keyboard.HeldFieldButtons);
        keyboard.Read(new KeyboardState(), true);
        Assert.Equal((ushort)0, keyboard.HeldFieldButtons);
    }

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
