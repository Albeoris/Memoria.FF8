using System.Text.Json.Nodes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class MovieCancelInputTests
{
    [Fact]
    public void Default_cancel_stays_held_after_its_action_edge_and_clears_on_release()
    {
        KeyboardInput input = new();

        Assert.False(input.CancelHeld);
        Assert.True(input.Read(new KeyboardState(Keys.C), true).Cancel);
        Assert.True(input.CancelHeld);
        Assert.Equal("C", input.CancelBindingHint);
        Assert.False(input.Read(new KeyboardState(Keys.C), true).Cancel);
        Assert.True(input.CancelHeld);
        Assert.Equal("C", input.CancelBindingHint);
        input.Read(default, true);
        Assert.False(input.CancelHeld);
        Assert.True(input.Read(new KeyboardState(Keys.C), true).Cancel);
        Assert.True(input.CancelHeld);
    }

    [Fact]
    public void Focus_loss_disarms_a_held_cancel_until_release()
    {
        KeyboardInput input = new();

        input.Read(new KeyboardState(Keys.C), true);
        Assert.True(input.CancelHeld);
        input.Read(new KeyboardState(Keys.C), false);
        Assert.False(input.CancelHeld);
        Assert.False(input.Read(new KeyboardState(Keys.C), true).Cancel);
        Assert.False(input.CancelHeld);
        input.Read(new KeyboardState(Keys.C), true);
        Assert.False(input.CancelHeld);
        input.Read(default, true);
        Assert.True(input.Read(new KeyboardState(Keys.C), true).Cancel);
        Assert.True(input.CancelHeld);
    }

    [Fact]
    public void Remapped_cancel_uses_the_current_key_and_binding_hint()
    {
        KeyboardInput input = new() { Profile = ControlsProfile.CreateDefault().WithBinding(ControlAction.Cancel, ControlDevice.Keyboard, new InputChord([Keys.K], [])) };

        Assert.False(input.Read(new KeyboardState(Keys.C), true).Cancel);
        Assert.False(input.CancelHeld);
        Assert.True(input.Read(new KeyboardState(Keys.K), true).Cancel);
        Assert.True(input.CancelHeld);
        Assert.Equal("K", input.CancelBindingHint);
        Assert.False(input.Read(new KeyboardState(Keys.K), true).Cancel);
        Assert.True(input.CancelHeld);
    }

    [Fact]
    public void A_shortcut_chord_consumes_the_simple_cancel_key_until_release()
    {
        KeyboardInput input = new() { Profile = ControlsProfile.CreateDefault().WithBinding(ControlAction.SpeedToggle, ControlDevice.Keyboard, new InputChord([Keys.LeftControl, Keys.C], [])) };

        Assert.False(input.Read(new KeyboardState(Keys.LeftControl, Keys.C), true).Cancel);
        Assert.True(input.SpeedToggleRequested);
        Assert.False(input.CancelHeld);
        Assert.False(input.Read(new KeyboardState(Keys.C), true).Cancel);
        Assert.False(input.CancelHeld);
        input.Read(default, true);
        Assert.True(input.Read(new KeyboardState(Keys.C), true).Cancel);
        Assert.True(input.CancelHeld);
        Assert.Equal("C", input.CancelBindingHint);
    }

    [Fact]
    public void Remapped_cancel_chord_is_continuous_and_consumes_its_simple_key()
    {
        ControlsProfile profile = ControlsProfile.CreateDefault().WithBinding(ControlAction.Cancel, ControlDevice.Keyboard, new InputChord([Keys.LeftControl, Keys.K], [])).WithBinding(ControlAction.QuickSave, ControlDevice.Keyboard, new InputChord([Keys.K], []));
        KeyboardInput input = new() { Profile = profile };

        Runtime.InputFrame pressed = input.Read(new KeyboardState(Keys.LeftControl, Keys.K), true);
        Assert.True(pressed.Cancel);
        Assert.False(pressed.Save);
        Assert.True(input.CancelHeld);
        Assert.Equal("Left Ctrl + K", input.CancelBindingHint);
        Runtime.InputFrame held = input.Read(new KeyboardState(Keys.LeftControl, Keys.K), true);
        Assert.False(held.Cancel);
        Assert.False(held.Save);
        Assert.True(input.CancelHeld);
        Assert.False(input.Read(new KeyboardState(Keys.K), true).Save);
        Assert.False(input.CancelHeld);
        input.Read(default, true);
        Assert.True(input.Read(new KeyboardState(Keys.K), true).Save);
        Assert.False(input.CancelHeld);
    }

    [Fact]
    public void Controller_cancel_is_continuous_after_connection_disarm_and_obeys_focus()
    {
        KeyboardInput input = new();

        Assert.False(input.Read(default, Pad(Buttons.B), true).Cancel);
        Assert.False(input.CancelHeld);
        input.Read(default, Pad(Buttons.B), true);
        Assert.False(input.CancelHeld);
        input.Read(default, Pad(), true);
        Assert.True(input.Read(default, Pad(Buttons.B), true).Cancel);
        Assert.True(input.CancelHeld);
        Assert.Equal("East button", input.CancelBindingHint);
        Assert.False(input.Read(default, Pad(Buttons.B), true).Cancel);
        Assert.True(input.CancelHeld);
        input.Read(default, Pad(Buttons.B), false);
        Assert.False(input.CancelHeld);
        input.Read(default, Pad(Buttons.B), true);
        Assert.False(input.CancelHeld);
        input.Read(default, Pad(), true);
        input.Read(default, Pad(Buttons.B), true);
        Assert.True(input.CancelHeld);
        input.Read(default, default, true);
        Assert.False(input.CancelHeld);
        input.Read(default, Pad(Buttons.B), true);
        Assert.False(input.CancelHeld);
    }

    [Theory]
    [InlineData(ControllerPresentation.Generic, "East button")]
    [InlineData(ControllerPresentation.Xbox, "[B]")]
    [InlineData(ControllerPresentation.PlayStation, "\u25CB")]
    public void Controller_cancel_hint_uses_connected_presentation_and_generation_changes_disarm_the_hold(ControllerPresentation presentation, string hint)
    {
        KeyboardInput input = new() { ControllerPresentation = presentation, ControllerGeneration = 1 };

        input.Read(default, Pad(), true);
        Assert.True(input.Read(default, Pad(Buttons.B), true).Cancel);
        Assert.True(input.CancelHeld);
        Assert.Equal(hint, input.CancelBindingHint);
        Assert.False(input.Read(default, Pad(Buttons.B), true).Cancel);
        Assert.True(input.CancelHeld);

        input.ControllerGeneration = 2;
        Assert.False(input.Read(default, Pad(Buttons.B), true).Cancel);
        Assert.False(input.CancelHeld);
        input.Read(default, Pad(Buttons.B), true);
        Assert.False(input.CancelHeld);
        input.Read(default, Pad(), true);
        Assert.True(input.Read(default, Pad(Buttons.B), true).Cancel);
        Assert.True(input.CancelHeld);
        Assert.Equal(hint, input.CancelBindingHint);
    }

    [Theory]
    [InlineData(Keys.Space, false)]
    [InlineData(Keys.Escape, false)]
    [InlineData(Keys.Space, true)]
    [InlineData(Keys.Escape, true)]
    public void Space_and_escape_follow_their_native_action_without_an_immediate_skip(Keys key, bool recoveryNavigation)
    {
        KeyboardInput input = new() { RecoveryNavigationEnabled = recoveryNavigation };

        Assert.False(input.Read(new KeyboardState(key), true).Skip);
        Assert.Equal(key == Keys.Escape, input.CancelHeld);
        Assert.False(input.Read(new KeyboardState(key), true).Skip);
        Assert.Equal(key == Keys.Escape, input.CancelHeld);
    }

    [Fact]
    public void Default_controls_exclude_a_separate_movie_skip_action_and_bindings()
    {
        ControlsProfile profile = ControlsProfile.CreateDefault();

        Assert.DoesNotContain(ControlsProfile.Definitions, definition => definition.Action == ControlAction.SkipMovie);
        Assert.Empty(profile.GetBindings(ControlAction.SkipMovie, ControlDevice.Keyboard));
        Assert.Empty(profile.GetBindings(ControlAction.SkipMovie, ControlDevice.Controller));
    }

    [Fact]
    public void Loading_an_old_profile_discards_skip_bindings_and_preserves_remapped_cancel()
    {
        string path = Path.Combine(Path.GetTempPath(), "memoria-movie-controls-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            ControlsProfile.CreateDefault().WithBinding(ControlAction.Cancel, ControlDevice.Keyboard, new InputChord([Keys.K], [])).Save(path);
            JsonNode document = JsonNode.Parse(File.ReadAllText(path))!;
            JsonArray bindings = document["Bindings"]!.AsArray();
            bindings.Add(JsonNode.Parse("""{"Action":"SkipMovie","Device":"Keyboard","Chord":{"Keys":["Escape"],"Buttons":[]}}"""));
            bindings.Add(JsonNode.Parse("""{"Action":"SkipMovie","Device":"Keyboard","Chord":{"Keys":["Space"],"Buttons":[]}}"""));
            File.WriteAllText(path, document.ToJsonString());

            ControlsProfile profile = ControlsProfile.Load(path);
            Assert.Empty(profile.GetBindings(ControlAction.SkipMovie, ControlDevice.Keyboard));
            Assert.Equal("K", Assert.Single(profile.GetBindings(ControlAction.Cancel, ControlDevice.Keyboard)).DisplayName);
            Assert.Equal("East button", Assert.Single(profile.GetBindings(ControlAction.Cancel, ControlDevice.Controller)).DisplayName);
            KeyboardInput input = new() { Profile = profile };
            foreach (Keys key in new[] { Keys.Escape, Keys.Space, Keys.C })
            {
                Assert.False(input.Read(new KeyboardState(key), true).Skip);
                Assert.Equal(key == Keys.Escape, input.CancelHeld);
            }
            Assert.True(input.Read(new KeyboardState(Keys.K), true).Cancel);
            Assert.True(input.CancelHeld);
            Assert.Equal("K", input.CancelBindingHint);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static GamePadState Pad(params Buttons[] buttons)
    {
        return new GamePadState(Vector2.Zero, Vector2.Zero, 0, 0, buttons);
    }
}
