using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class SaveBankInputTests
{
    [Fact]
    public void Transfer_uses_remapped_card_action_and_requires_release()
    {
        KeyboardInput input = new() { Profile = ControlsProfile.CreateDefault().WithBinding(ControlAction.Card, ControlDevice.Keyboard, new InputChord([Keys.O], [])) };
        input.Read(default(KeyboardState), true);
        Assert.True(input.Read(new KeyboardState(Keys.O), true).CardChallenge);
        Assert.False(input.Read(new KeyboardState(Keys.O), true).CardChallenge);
        input.Read(default(KeyboardState), true);
        Assert.True(input.Read(new KeyboardState(Keys.O), true).CardChallenge);
        input.Read(new KeyboardState(Keys.O), false);
        Assert.False(input.Read(new KeyboardState(Keys.O), true).CardChallenge);
    }

    [Fact]
    public void Controller_transfer_uses_remapped_card_action()
    {
        KeyboardInput input = new() { Profile = ControlsProfile.CreateDefault().WithBinding(ControlAction.Card, ControlDevice.Controller, new InputChord([], [Buttons.RightStick]), replaceConflicts: true) };
        input.Read(default, Pad(), true);
        Assert.True(input.Read(default, Pad(Buttons.RightStick), true).CardChallenge);
        Assert.False(input.Read(default, Pad(Buttons.RightStick), true).CardChallenge);
        input.Read(default, Pad(), true);
        Assert.True(input.Read(default, Pad(Buttons.RightStick), true).CardChallenge);
    }

    private static GamePadState Pad(params Buttons[] buttons)
    {
        return new GamePadState(default, default, 0, 0, buttons);
    }
}
