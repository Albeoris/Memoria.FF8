using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class BattleComponentGuardianBoostTests
{
    [Fact]
    public void Boost_uses_the_remapped_original_square_edge()
    {
        KeyboardInput keyboard = new();
        keyboard.Profile = keyboard.Profile.WithBinding(ControlAction.Card, ControlDevice.Keyboard, new InputChord([Keys.T], []));
        keyboard.Read(default, true);
        keyboard.Read(new KeyboardState(Keys.T), true);
        Assert.True(keyboard.GuardianBoostRequested);
        Assert.Equal((ushort)0x80, keyboard.HeldFieldButtons);
        Assert.False(keyboard.GunbladeTriggerRequested);
        keyboard.Read(new KeyboardState(Keys.T), true);
        Assert.False(keyboard.GuardianBoostRequested);
    }
}
