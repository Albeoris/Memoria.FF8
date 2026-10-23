using System.Text.Json.Nodes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class GuardianEditorKeyboardInputTests
{
    [Theory]
    [InlineData(Keys.LeftControl, Keys.LeftShift)]
    [InlineData(Keys.RightControl, Keys.RightShift)]
    public void Column_chord_consumes_tab_until_every_key_is_released(Keys control, Keys shift)
    {
        KeyboardInput input = new() { GuardianEditorEnabled = true };
        input.Read(new KeyboardState(control, shift, Keys.Tab), true);
        Assert.True(input.UiSwitchColumnRequested);
        Assert.False(input.UiTabRequested);
        Assert.False(input.UiReverseTabRequested);
        input.Read(new KeyboardState(Keys.Tab), true);
        Assert.False(input.UiTabRequested);
        Assert.False(input.UiSwitchColumnRequested);
        input.Read(default, true);
        input.Read(new KeyboardState(Keys.Tab), true);
        Assert.True(input.UiTabRequested);
    }

    [Fact]
    public void Focus_traversal_has_distinct_forward_and_reverse_semantics()
    {
        KeyboardInput input = new() { GuardianEditorEnabled = true };
        input.Read(new KeyboardState(Keys.LeftShift, Keys.Tab), true);
        Assert.True(input.UiReverseTabRequested);
        Assert.False(input.UiTabRequested);
        input.Read(default, true);
        input.Read(new KeyboardState(Keys.Tab), true);
        Assert.True(input.UiTabRequested);
        Assert.False(input.UiReverseTabRequested);
    }

    [Fact]
    public void Column_action_is_scoped_to_editor_and_obeys_remapping()
    {
        KeyboardInput input = new();
        input.Read(new KeyboardState(Keys.LeftControl, Keys.Tab), true);
        Assert.True(input.UiTabRequested);
        Assert.False(input.UiSwitchColumnRequested);

        input = new() { GuardianEditorEnabled = true, Profile = ControlsProfile.CreateDefault().WithBinding(ControlAction.GuardianSwitchColumn, ControlDevice.Keyboard, new InputChord([Keys.K], [])) };
        input.Read(new KeyboardState(Keys.K), true);
        Assert.True(input.UiSwitchColumnRequested);
        Assert.False(input.UiTabRequested);
        input.Read(default, true);
        input.TextEntryEnabled = true;
        input.Read(new KeyboardState(Keys.K), true);
        Assert.False(input.UiSwitchColumnRequested);
    }

    [Fact]
    public void Controller_column_action_uses_stick_press_without_menu_or_pause()
    {
        KeyboardInput input = new() { GuardianEditorEnabled = true };
        input.Read(default, new GamePadState(Vector2.Zero, Vector2.Zero, 0, 0, Buttons.None), true);
        input.Read(default, new GamePadState(Vector2.Zero, Vector2.Zero, 0, 0, Buttons.RightStick), true);
        Assert.True(input.UiSwitchColumnRequested);
        Assert.False(input.GameMenuRequested);
        Assert.False(input.PauseRequested);
    }

    [Fact]
    public void Remapping_card_to_column_binding_requires_review_and_replaces_its_editor_command()
    {
        ControlsProfile profile = ControlsProfile.CreateDefault();
        InputChord chord = new([], [Buttons.RightStick]);
        Assert.Equal(new[] { ControlAction.GuardianSwitchColumn }, profile.FindConflicts(ControlAction.Card, ControlDevice.Controller, chord));
        Assert.Throws<InvalidOperationException>(() => profile.WithBinding(ControlAction.Card, ControlDevice.Controller, chord));
        profile = profile.WithBinding(ControlAction.Card, ControlDevice.Controller, chord, replaceConflicts: true);
        Assert.Empty(profile.GetBindings(ControlAction.GuardianSwitchColumn, ControlDevice.Controller));
        KeyboardInput input = new() { Profile = profile, GuardianEditorEnabled = true };
        input.Read(default, new GamePadState(Vector2.Zero, Vector2.Zero, 0, 0, Buttons.None), true);
        input.Read(default, new GamePadState(Vector2.Zero, Vector2.Zero, 0, 0, Buttons.RightStick), true);
        Assert.True(input.UiDetailRequested);
        Assert.False(input.UiSwitchColumnRequested);
    }

    [Fact]
    public void Search_text_owns_bound_characters_and_exit_disarms_held_confirm()
    {
        KeyboardInput input = new() { GuardianEditorEnabled = true, TextEntryEnabled = true };
        input.Read(new KeyboardState(Keys.V, Keys.S, Keys.Q), true);
        Assert.False(input.GameMenuRequested);
        Assert.False(input.UiDetailRequested);
        Assert.Equal(0, input.UiCharacterPage);
        input.Read(default, true);
        Assert.True(input.Read(new KeyboardState(Keys.Enter), true).Confirm);
        input.SuppressHeldUiInput();
        input.TextEntryEnabled = false;
        Assert.False(input.Read(new KeyboardState(Keys.Enter), true).Confirm);
        Assert.False(input.UiConfirmRequested);
        input.Read(default, true);
        Assert.True(input.Read(new KeyboardState(Keys.Enter), true).Confirm);
    }

    [Fact]
    public void Search_exit_disarms_controller_confirm_until_release()
    {
        KeyboardInput input = new() { GuardianEditorEnabled = true, TextEntryEnabled = true };
        GamePadState released = new(Vector2.Zero, Vector2.Zero, 0, 0, Buttons.None);
        GamePadState confirm = new(Vector2.Zero, Vector2.Zero, 0, 0, Buttons.A);
        input.Read(default, released, true);
        Assert.True(input.Read(default, confirm, true).Confirm);
        input.SuppressHeldUiInput();
        input.TextEntryEnabled = false;
        Assert.False(input.Read(default, confirm, true).Confirm);
        input.Read(default, released, true);
        Assert.True(input.Read(default, confirm, true).Confirm);
    }

    [Fact]
    public void Pane_GF_card_and_actions_remain_separate_semantic_commands()
    {
        foreach ((ControlAction action, Action<KeyboardInput> verify) in new (ControlAction, Action<KeyboardInput>)[]
        {
            (ControlAction.CameraLeft, input => Assert.Equal(-1, input.UiPaneFocus)),
            (ControlAction.CameraRight, input => Assert.Equal(1, input.UiPaneFocus)),
            (ControlAction.RotateLeft, input => Assert.Equal(-1, input.UiCharacterPage)),
            (ControlAction.RotateRight, input => Assert.Equal(1, input.UiCharacterPage)),
            (ControlAction.Card, input => Assert.True(input.UiDetailRequested)),
            (ControlAction.Menu, input => Assert.True(input.GameMenuRequested))
        })
        {
            KeyboardInput input = new() { GuardianEditorEnabled = true, Profile = ControlsProfile.CreateDefault().WithBinding(action, ControlDevice.Keyboard, new InputChord([Keys.K], [])) };
            input.Read(new KeyboardState(Keys.K), true);
            verify(input);
            Assert.False(input.UiSwitchColumnRequested);
        }
    }

    [Fact]
    public void Old_profiles_gain_only_nonconflicting_defaults_and_new_profiles_retain_unbinding()
    {
        string path = Path.Combine(Path.GetTempPath(), "memoria-editor-controls-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            ControlsProfile profile = ControlsProfile.CreateDefault().WithoutBinding(ControlAction.GuardianSwitchColumn, ControlDevice.Keyboard).WithoutBinding(ControlAction.GuardianSwitchColumn, ControlDevice.Controller);
            profile.Save(path);
            Assert.Empty(ControlsProfile.Load(path).GetBindings(ControlAction.GuardianSwitchColumn, ControlDevice.Keyboard));
            JsonNode document = JsonNode.Parse(File.ReadAllText(path))!;
            document["Version"] = 1;
            File.WriteAllText(path, document.ToJsonString());
            Assert.Equal(2, ControlsProfile.Load(path).GetBindings(ControlAction.GuardianSwitchColumn, ControlDevice.Keyboard).Count);

            profile.WithBinding(ControlAction.Card, ControlDevice.Controller, new InputChord([], [Buttons.RightStick])).Save(path);
            document = JsonNode.Parse(File.ReadAllText(path))!;
            document["Version"] = 1;
            File.WriteAllText(path, document.ToJsonString());
            ControlsProfile loaded = ControlsProfile.Load(path);
            Assert.Empty(loaded.GetBindings(ControlAction.GuardianSwitchColumn, ControlDevice.Controller));
            Assert.Equal(Buttons.RightStick, Assert.Single(loaded.GetBindings(ControlAction.Card, ControlDevice.Controller)).Buttons[0]);

            InputChord customChord = new([Keys.LeftControl, Keys.Tab], []);
            profile.WithBinding(ControlAction.QuickSave, ControlDevice.Keyboard, customChord).Save(path);
            document = JsonNode.Parse(File.ReadAllText(path))!;
            document["Version"] = 1;
            File.WriteAllText(path, document.ToJsonString());
            loaded = ControlsProfile.Load(path);
            Assert.Equal(customChord.DisplayName, Assert.Single(loaded.GetBindings(ControlAction.QuickSave, ControlDevice.Keyboard)).DisplayName);
            Assert.Single(loaded.GetBindings(ControlAction.GuardianSwitchColumn, ControlDevice.Keyboard));
            loaded.Save(path);
            Assert.Equal(2, JsonNode.Parse(File.ReadAllText(path))!["Version"]!.GetValue<int>());
        }
        finally
        {
            File.Delete(path);
        }
    }
}
