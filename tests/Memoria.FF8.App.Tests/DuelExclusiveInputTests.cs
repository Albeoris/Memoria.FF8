using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class DuelExclusiveInputTests
{
    [Fact]
    public void Duel_routes_native_keyboard_buttons_without_host_shortcuts_or_menu_edges()
    {
        KeyboardInput input = new() { NativeButtonsOwnInput = true, RecoveryNavigationEnabled = true };
        KeyboardState keys = new(Keys.X, Keys.C, Keys.V, Keys.S, Keys.H, Keys.G, Keys.D, Keys.F, Keys.J, Keys.A, Keys.Up, Keys.F1, Keys.F2, Keys.F3, Keys.F4, Keys.F5, Keys.F6, Keys.F7, Keys.F9, Keys.F10, Keys.Enter, Keys.Escape);
        Assert.Equal(default(InputFrame), input.Read(keys, true));
        Assert.Equal((ushort)0x19FF, input.HeldFieldButtons);
        Assert.Equal(input.HeldFieldButtons, input.HeldControlButtons);
        AssertNoHostActions(input);

        input.NativeButtonsOwnInput = false;
        InputFrame held = input.Read(keys, true);
        Assert.Equal(default(InputFrame), held);
        AssertNoHostActions(input);
        input.Read(new KeyboardState(), true);
        input.Read(new KeyboardState(Keys.V, Keys.F1), true);
        Assert.True(input.GameMenuRequested);
        Assert.True(input.SpeedToggleRequested);
    }

    [Fact]
    public void Duel_routes_controller_buttons_and_consumes_edges_until_release()
    {
        KeyboardInput input = new();
        input.Read(new KeyboardState(), new GamePadState(Vector2.Zero, Vector2.Zero, 0, 0, Buttons.None), true);
        input.NativeButtonsOwnInput = true;
        GamePadState pad = new(Vector2.UnitX, Vector2.UnitY, 0, 0, [Buttons.A, Buttons.B, Buttons.Y, Buttons.Start, Buttons.LeftShoulder, Buttons.RightShoulder]);
        Assert.Equal(default(InputFrame), input.Read(new KeyboardState(), pad, true));
        Assert.NotEqual((ushort)0, input.HeldFieldButtons);
        AssertNoHostActions(input);
        input.NativeButtonsOwnInput = false;
        InputFrame held = input.Read(new KeyboardState(), pad, true);
        Assert.Equal(default(InputFrame), held);
        Assert.False(input.GameMenuRequested);
        Assert.False(input.PauseRequested);
    }

    private static void AssertNoHostActions(KeyboardInput input)
    {
        Assert.False(input.GameMenuRequested);
        Assert.False(input.PauseRequested);
        Assert.False(input.SpeedToggleRequested);
        Assert.False(input.DebugRoomRequested);
        Assert.False(input.BattleDebugRequested);
        Assert.False(input.BattleInstantVictoryRequested);
        Assert.False(input.BattleAdvanceTurnRequested);
        Assert.False(input.GunbladeTriggerRequested);
        Assert.False(input.GuardianBoostRequested);
        Assert.False(input.GuardianFormSkipRequested);
        Assert.False(input.UiConfirmRequested);
        Assert.False(input.UiCancelRequested);
        Assert.False(input.UiDetailRequested);
        Assert.False(input.CancelHeld);
        Assert.False(input.UiTabRequested);
        Assert.False(input.UiReverseTabRequested);
        Assert.False(input.UiSwitchColumnRequested);
        Assert.Equal(0, input.UiPaneFocus);
        Assert.Equal(0, input.UiCharacterPage);
    }
}
