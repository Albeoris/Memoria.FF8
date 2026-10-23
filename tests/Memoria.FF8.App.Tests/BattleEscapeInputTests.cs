using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class BattleEscapeInputTests
{
    [Fact]
    public void Default_keyboard_escape_requires_both_buttons_and_persists_while_held()
    {
        KeyboardInput input = new();
        input.Read(new KeyboardState(Keys.D), true);
        Assert.Equal((ushort)1, input.HeldControlButtons);
        input.Read(new KeyboardState(Keys.F), true);
        Assert.Equal((ushort)2, input.HeldControlButtons);
        for (int frame = 0; frame < 65; frame++)
        {
            input.Read(new KeyboardState(Keys.D, Keys.F), true);
            Assert.Equal((ushort)3, input.HeldControlButtons);
        }
        input.Read(default, true);
        Assert.Equal((ushort)0, input.HeldControlButtons);
    }

    [Fact]
    public void Escape_uses_remapped_original_actions()
    {
        KeyboardInput input = new()
        {
            Profile = ControlsProfile.CreateDefault()
                .WithBinding(ControlAction.CameraLeft, ControlDevice.Keyboard, new InputChord([Keys.Q], []))
                .WithBinding(ControlAction.CameraRight, ControlDevice.Keyboard, new InputChord([Keys.W], []))
        };
        input.Read(new KeyboardState(Keys.D, Keys.F), true);
        Assert.Equal((ushort)0, input.HeldControlButtons);
        input.Read(new KeyboardState(Keys.Q, Keys.W), true);
        Assert.Equal((ushort)3, input.HeldControlButtons);
    }

    [Fact]
    public void Controller_triggers_and_focus_loss_obey_the_same_held_mask_and_release_safety()
    {
        KeyboardInput input = new();
        GamePadState released = new(Vector2.Zero, Vector2.Zero, 0, 0, Buttons.None);
        GamePadState held = new(Vector2.Zero, Vector2.Zero, 1, 1, Buttons.LeftTrigger | Buttons.RightTrigger);
        input.Read(default, released, true);
        input.Read(default, held, true);
        Assert.Equal((ushort)3, input.HeldControlButtons);
        input.Read(default, held, false);
        Assert.Equal((ushort)0, input.HeldControlButtons);
        input.Read(default, held, true);
        Assert.Equal((ushort)0, input.HeldControlButtons);
        input.Read(default, released, true);
        input.Read(default, held, true);
        Assert.Equal((ushort)3, input.HeldControlButtons);
    }
}
