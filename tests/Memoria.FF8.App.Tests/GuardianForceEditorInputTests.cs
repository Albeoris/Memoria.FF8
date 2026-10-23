using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class GuardianForceEditorInputTests
{
    [Fact]
    public void PaneFocusUsesRemappedEscapeActionsWithEdgesAndHarmlessSimultaneousInputs()
    {
        KeyboardInput input = new()
        {
            Profile = ControlsProfile.CreateDefault().WithBinding(ControlAction.CameraLeft, ControlDevice.Keyboard, new([Keys.K], [])).WithBinding(ControlAction.CameraRight, ControlDevice.Keyboard, new([Keys.L], []))
        };
        input.Read(default, true);
        input.Read(new KeyboardState(Keys.K, Keys.L), true);
        Assert.Equal(-1, input.UiPaneFocus);
        Assert.Equal(0, input.UiCharacterPage);
        input.Read(new KeyboardState(Keys.K, Keys.L), true);
        Assert.Equal(0, input.UiPaneFocus);
        input.Read(default, true);
        input.Read(new KeyboardState(Keys.L), true);
        Assert.Equal(1, input.UiPaneFocus);
        input.SuppressHeldUiInput();
        input.Read(new KeyboardState(Keys.L), true);
        Assert.Equal(0, input.UiPaneFocus);
        input.Read(default, true);
        input.Read(new KeyboardState(Keys.L), true);
        Assert.Equal(1, input.UiPaneFocus);
    }

    [Fact]
    public void ControllerEscapeTriggersFocusWithoutRepeatingAfterDialogReturn()
    {
        KeyboardInput input = new();
        GamePadState neutral = new(Vector2.Zero, Vector2.Zero, 0, 0, Buttons.None);
        GamePadState both = new(Vector2.Zero, Vector2.Zero, 1, 1, Buttons.LeftTrigger | Buttons.RightTrigger);
        input.Read(default, neutral, true);
        input.Read(default, both, true);
        Assert.Equal(-1, input.UiPaneFocus);
        Assert.Equal(0, input.UiCharacterPage);
        input.SuppressHeldUiInput();
        input.Read(default, both, true);
        Assert.Equal(0, input.UiPaneFocus);
        input.Read(default, neutral, true);
        input.Read(default, new GamePadState(Vector2.Zero, Vector2.Zero, 0, 1, Buttons.RightTrigger), true);
        Assert.Equal(1, input.UiPaneFocus);
    }
}
