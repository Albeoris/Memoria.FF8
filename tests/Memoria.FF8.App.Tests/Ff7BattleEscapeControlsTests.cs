using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7BattleEscapeControlsTests
{
    [Fact]
    public void DefaultEscapeBindingsRequireBothActionsAndStopOnRelease()
    {
        KeyboardInput input = new();
        input.Read(default, true);
        input.Read(new KeyboardState(Keys.D), true);
        Assert.False(Ff7Scene.IsBattleEscapeHeld(input.HeldFieldButtons));
        input.Read(new KeyboardState(Keys.F), true);
        Assert.False(Ff7Scene.IsBattleEscapeHeld(input.HeldFieldButtons));
        input.Read(new KeyboardState(Keys.D, Keys.F), true);
        Assert.True(Ff7Scene.IsBattleEscapeHeld(input.HeldFieldButtons));
        input.Read(new KeyboardState(Keys.D, Keys.F), true);
        Assert.True(Ff7Scene.IsBattleEscapeHeld(input.HeldFieldButtons));
        input.Read(default, true);
        Assert.False(Ff7Scene.IsBattleEscapeHeld(input.HeldFieldButtons));
    }

    [Fact]
    public void RemappedEscapeActionsUseConfiguredKeys()
    {
        ControlsProfile profile = ControlsProfile.CreateDefault().WithBinding(ControlAction.CameraLeft, ControlDevice.Keyboard, new([Keys.J], []), replaceConflicts: true).WithBinding(ControlAction.CameraRight, ControlDevice.Keyboard, new([Keys.K], []), replaceConflicts: true);
        KeyboardInput input = new() { Profile = profile };
        input.Read(default, true);
        input.Read(new KeyboardState(Keys.D, Keys.F), true);
        Assert.False(Ff7Scene.IsBattleEscapeHeld(input.HeldFieldButtons));
        input.Read(new KeyboardState(Keys.J, Keys.K), true);
        Assert.True(Ff7Scene.IsBattleEscapeHeld(input.HeldFieldButtons));
        Assert.Equal((ushort)0x03, Ff7ControlButtons.FromShared(input.HeldFieldButtons));
    }

    [Fact]
    public void ControllerTriggersEscapeWhileRotationShouldersKeepTheirNativeSlots()
    {
        KeyboardInput input = new();
        input.Read(default, Pad(0, 0), true);
        input.Read(default, Pad(1, 0), true);
        Assert.False(Ff7Scene.IsBattleEscapeHeld(input.HeldFieldButtons));
        input.Read(default, Pad(1, 1), true);
        Assert.True(Ff7Scene.IsBattleEscapeHeld(input.HeldFieldButtons));
        Assert.Equal((ushort)0x03, Ff7ControlButtons.FromShared(input.HeldFieldButtons));
        input.Read(default, Pad(0, 0), true);
        input.Read(default, Pad(0, 0, Buttons.LeftShoulder, Buttons.RightShoulder), true);
        Assert.False(Ff7Scene.IsBattleEscapeHeld(input.HeldFieldButtons));
        Assert.Equal((ushort)0x0C, Ff7ControlButtons.FromShared(input.HeldFieldButtons));
    }

    private static GamePadState Pad(float leftTrigger, float rightTrigger, params Buttons[] buttons)
    {
        Buttons held = Buttons.None;
        foreach (Buttons button in buttons)
        {
            held |= button;
        }
        if (leftTrigger > 0)
        {
            held |= Buttons.LeftTrigger;
        }
        if (rightTrigger > 0)
        {
            held |= Buttons.RightTrigger;
        }
        return new(Vector2.Zero, Vector2.Zero, leftTrigger, rightTrigger, held);
    }
}
