using System.Text.Json.Nodes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class ReservedKeyboardInputTests
{
    [Theory]
    [InlineData(Keys.Enter, ControlAction.Confirm, 0x40)]
    [InlineData(Keys.Escape, ControlAction.Cancel, 0x10)]
    [InlineData(Keys.Up, ControlAction.MoveUp, 0x1000)]
    [InlineData(Keys.Down, ControlAction.MoveDown, 0x4000)]
    [InlineData(Keys.Left, ControlAction.MoveLeft, 0x8000)]
    [InlineData(Keys.Right, ControlAction.MoveRight, 0x2000)]
    public void Permanent_keys_survive_custom_rebinding_and_clearing(Keys key, ControlAction action, int mask)
    {
        ControlsProfile profile = ControlsProfile.CreateDefault().WithBinding(action, ControlDevice.Keyboard, new InputChord([Keys.K], []));
        Assert.Equal("K", Assert.Single(profile.GetBindings(action, ControlDevice.Keyboard)).DisplayName);
        KeyboardInput custom = new() { Profile = profile };
        custom.Read(new KeyboardState(Keys.K), true);
        Assert.Equal((ushort)mask, custom.HeldFieldButtons);
        KeyboardInput permanent = new() { Profile = profile.WithoutBinding(action, ControlDevice.Keyboard) };
        permanent.Read(new KeyboardState(key), true);
        Assert.Equal((ushort)mask, permanent.HeldFieldButtons);
        Assert.Equal(key.ToString(), permanent.BindingHint(action));
        Assert.Equal(key, Assert.Single(ControlsProfile.GetPermanentBindings(action, ControlDevice.Keyboard)).Keys[0]);
    }

    [Theory]
    [InlineData(Keys.Enter, ControlAction.Confirm)]
    [InlineData(Keys.Escape, ControlAction.Cancel)]
    [InlineData(Keys.Up, ControlAction.MoveUp)]
    [InlineData(Keys.Down, ControlAction.MoveDown)]
    [InlineData(Keys.Left, ControlAction.MoveLeft)]
    [InlineData(Keys.Right, ControlAction.MoveRight)]
    public void Reserved_keys_reject_other_actions_even_in_chords_and_replacement(Keys key, ControlAction owner)
    {
        ControlsProfile profile = ControlsProfile.CreateDefault();
        foreach (bool replace in new[] { false, true })
        {
            Assert.Throws<InvalidOperationException>(() => profile.WithBinding(ControlAction.QuickSave, ControlDevice.Keyboard, new InputChord([key], []), replace));
            Assert.Throws<InvalidOperationException>(() => profile.WithBinding(ControlAction.QuickSave, ControlDevice.Keyboard, new InputChord([Keys.LeftControl, key], []), replace));
        }
        Assert.Equal(key, Assert.Single(profile.WithBinding(owner, ControlDevice.Keyboard, new InputChord([key], [])).GetBindings(owner, ControlDevice.Keyboard)).Keys[0]);
        profile = profile.WithBinding(owner, ControlDevice.Keyboard, new InputChord([Keys.LeftControl, key], []));
        KeyboardInput input = new() { Profile = profile };
        input.Read(new KeyboardState(Keys.LeftControl, key), true);
        Assert.NotEqual((ushort)0, input.HeldFieldButtons);
        input.Read(new KeyboardState(key), true);
        Assert.NotEqual((ushort)0, input.HeldFieldButtons);
    }

    [Fact]
    public void Permanent_keys_respect_focus_profile_changes_and_controller_connection()
    {
        KeyboardInput input = new();
        GamePadState pad = new(Vector2.Zero, Vector2.Zero, 0, 0, Buttons.None);
        Assert.True(input.Read(new KeyboardState(Keys.Enter), pad, true).Confirm);
        input.Read(default, pad, false);
        Assert.False(input.Read(new KeyboardState(Keys.Escape), pad, true).Cancel);
        Assert.False(input.CancelHeld);
        input.Read(default, pad, true);
        Assert.True(input.Read(new KeyboardState(Keys.Escape), pad, true).Cancel);
        input.Profile = input.Profile.WithBinding(ControlAction.Cancel, ControlDevice.Keyboard, new InputChord([Keys.K], []));
        Assert.False(input.Read(new KeyboardState(Keys.Escape), pad, true).Cancel);
        Assert.False(input.CancelHeld);
        input.Read(default, pad, true);
        Assert.True(input.Read(new KeyboardState(Keys.Escape), pad, true).Cancel);
        Assert.Equal("Escape", input.CancelBindingHint);
    }

    [Fact]
    public void Loading_removes_obsolete_reserved_assignments_and_preserves_custom_controls()
    {
        string path = Path.Combine(Path.GetTempPath(), "memoria-reserved-controls-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            ControlsProfile.CreateDefault().WithBinding(ControlAction.Confirm, ControlDevice.Keyboard, new InputChord([Keys.K], [])).Save(path);
            JsonNode document = JsonNode.Parse(File.ReadAllText(path))!;
            JsonArray bindings = document["Bindings"]!.AsArray();
            bindings.Add(JsonNode.Parse("""{"Action":"QuickSave","Device":"Keyboard","Chord":{"Keys":["LeftControl","Enter"],"Buttons":[]}}"""));
            File.WriteAllText(path, document.ToJsonString());
            ControlsProfile loaded = ControlsProfile.Load(path);
            Assert.Equal("K", Assert.Single(loaded.GetBindings(ControlAction.Confirm, ControlDevice.Keyboard)).DisplayName);
            Assert.Equal("F5", Assert.Single(loaded.GetBindings(ControlAction.QuickSave, ControlDevice.Keyboard)).DisplayName);
            KeyboardInput input = new() { Profile = loaded };
            Assert.True(input.Read(new KeyboardState(Keys.LeftControl, Keys.Enter), true).Confirm);
            loaded.Save(path);
            Assert.DoesNotContain("LeftControl", File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
