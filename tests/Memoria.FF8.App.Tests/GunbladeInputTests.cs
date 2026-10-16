using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class GunbladeInputTests
{
    [Fact]
    public void GunbladeUsesOneEdgeOfTheExistingRemappableR1Action()
    {
        KeyboardInput input = new();
        input.Read(new KeyboardState(Keys.G), true);
        Assert.True(input.GunbladeTriggerRequested);
        Assert.Equal((ushort)8, input.HeldFieldButtons);
        input.Read(new KeyboardState(Keys.G), true);
        Assert.False(input.GunbladeTriggerRequested);
        input.Read(default, true);
        input.Read(new KeyboardState(Keys.G), true);
        Assert.True(input.GunbladeTriggerRequested);
        input.Read(default, true);
        input.Profile = input.Profile.WithBinding(ControlAction.RotateRight, ControlDevice.Keyboard, new InputChord([Keys.T], []));
        input.Read(default, true);
        input.Read(new KeyboardState(Keys.G), true);
        Assert.False(input.GunbladeTriggerRequested);
        input.Read(new KeyboardState(Keys.T), true);
        Assert.True(input.GunbladeTriggerRequested);
    }

    [Fact]
    public void ControllerRightShoulderUsesTheSameTriggerAndDoesNotRepeatWhileHeld()
    {
        KeyboardInput input = new();
        input.Read(default, new GamePadState(Vector2.Zero, Vector2.Zero, 0, 0, Array.Empty<Buttons>()), true);
        input.Read(default, new GamePadState(Vector2.Zero, Vector2.Zero, 0, 0, Buttons.RightShoulder), true);
        Assert.True(input.GunbladeTriggerRequested);
        input.Read(default, new GamePadState(Vector2.Zero, Vector2.Zero, 0, 0, Buttons.RightShoulder), true);
        Assert.False(input.GunbladeTriggerRequested);
    }

    [Fact]
    public void HostGunbladeModeIsExplicitAndReachableFromBattleSettings()
    {
        UiPreferences preferences = new();
        Assert.Equal(BattleGunbladeMode.Automatic, preferences.GunbladeMode);
        preferences = preferences.WithSetting("Gunblade trigger", "Manual");
        Assert.Equal(BattleGunbladeMode.Manual, preferences.GunbladeMode);
        Assert.Throws<ArgumentException>(() => preferences.WithSetting("Gunblade trigger", "Legacy"));
        MenuController menu = new();
        menu.ConnectPreferences(() => preferences, _ => { });
        menu.Open("debug-battle");
        Assert.Contains(menu.Entries, entry => entry.Label == "Gunblade trigger   Manual");
    }
}
