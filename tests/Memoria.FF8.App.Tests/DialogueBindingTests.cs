using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class DialogueBindingTests
{
    private const string Template = "{Key Triangle}4{Line}{Key Square}3 {Key Circle}1{Line}{Key Cross}2";

    private static string Format(KeyboardInput input, string template = Template)
    {
        return DialogueTextFormatter.Format(template, buttonBindingHint: input.NativeButtonBindingHint);
    }

    [Fact]
    public void Remapped_keyboard_actions_and_chords_replace_native_face_button_tags()
    {
        KeyboardInput input = new();
        Assert.Equal("C4\nS3 V1\nX2", Format(input));
        input.Profile = input.Profile.WithBinding(ControlAction.Cancel, ControlDevice.Keyboard, new([Keys.LeftControl, Keys.K], []));
        Assert.Equal(input.BindingHint(ControlAction.Cancel) + "4\nS3 V1\nX2", Format(input));
        Assert.DoesNotContain("C4", Format(input));
        Assert.Equal("H/G/D/F/J", Format(input, "{Key L1}/{Key R1}/{Key EscapeKey1}/{Key EscapeKey2}/{Key Select}"));
        Assert.Equal(input.BindingHint(ControlAction.Cancel) + "/V/X/S", Format(input, "{Key Menu}/{Key Cancel}/{Key Confirm}/{Key Cards}"));
    }

    [Fact]
    public void Last_device_and_controller_remapping_determine_face_button_hints()
    {
        KeyboardInput input = new() { ControllerPresentation = ControllerPresentation.PlayStation };
        GamePadState neutral = new(Vector2.Zero, Vector2.Zero, 0, 0, Array.Empty<Buttons>());
        GamePadState confirm = new(Vector2.Zero, Vector2.Zero, 0, 0, Buttons.A);
        input.Read(default, neutral, true);
        input.Read(default, confirm, true);
        Assert.Equal("\u25CB4\n\u25A13 \u25B31\n\u00D72", Format(input));
        input.Profile = input.Profile.WithBinding(ControlAction.Confirm, ControlDevice.Controller, new([], [Buttons.RightStick]));
        Assert.EndsWith("R32", Format(input));
        input.Profile = input.Profile.WithControllerSettings(0.2f, 0.2f, 0.5f, ControllerPresentation.Xbox);
        Assert.Equal("[B]4\n[X]3 [Y]1\nRS2", Format(input));
        input.Read(default, neutral, true);
        input.Read(new(Keys.X), neutral, true);
        Assert.Equal("C4\nS3 V1\nX2", Format(input));
    }

    [Fact]
    public void Tutorial_text_uses_the_current_profile_for_physical_and_action_tags()
    {
        ControlsProfile profile = ControlsProfile.CreateDefault().WithBinding(ControlAction.Cancel, ControlDevice.Keyboard, new([Keys.K], []));
        MenuController menu = new();
        menu.ConnectControls(() => profile, value => profile = value);
        Assert.Equal("K4\nS3 V1\nX2", menu.FormatNativeTutorialText(Template));
        Assert.Equal("K/V/X/S/A", menu.FormatNativeTutorialText("{Key Menu}/{Key Cancel}/{Key Confirm}/{Key Cards}/{Key EndKey}"));
    }

    [Theory]
    [InlineData(0x20, 0x0001)]
    [InlineData(0x21, 0x0002)]
    [InlineData(0x22, 0x0004)]
    [InlineData(0x23, 0x0008)]
    [InlineData(0x24, 0x0010)]
    [InlineData(0x25, 0x0020)]
    [InlineData(0x26, 0x0040)]
    [InlineData(0x27, 0x0080)]
    [InlineData(0x28, 0x0100)]
    [InlineData(0x2B, 0x0800)]
    [InlineData(0x2C, 0x1000)]
    [InlineData(0x2D, 0x2000)]
    [InlineData(0x2E, 0x4000)]
    [InlineData(0x2F, 0x8000)]
    [InlineData(0x30, 0x0001)]
    [InlineData(0x31, 0x0002)]
    [InlineData(0x32, 0x0004)]
    [InlineData(0x33, 0x0008)]
    [InlineData(0x34, 0x0010)]
    [InlineData(0x35, 0x0020)]
    [InlineData(0x36, 0x0040)]
    [InlineData(0x37, 0x0080)]
    [InlineData(0x38, 0x0100)]
    [InlineData(0x3B, 0x0800)]
    [InlineData(0x3C, 0x1000)]
    [InlineData(0x3D, 0x2000)]
    [InlineData(0x3E, 0x4000)]
    [InlineData(0x3F, 0x8000)]
    public void Logical_and_fixed_native_key_indices_resolve_the_same_button_mask(int key, int buttons)
    {
        Assert.Equal($"{buttons:X4}", DialogueTextFormatter.Format($"{{Key {key}}}", buttonBindingHint: value => $"{value:X4}"));
    }

    [Theory]
    [InlineData(0x29)]
    [InlineData(0x2A)]
    [InlineData(0x39)]
    [InlineData(0x3A)]
    public void Native_key_indices_without_a_host_action_remain_unresolved(int key)
    {
        string text = $"{{Key {key}}}";
        Assert.Equal(text, DialogueTextFormatter.Format(text, buttonBindingHint: _ => throw new InvalidOperationException("Unbound native indices must not reach the host resolver.")));
    }

    [Fact]
    public void Open_dialogue_rebuilds_from_changed_binding_text_and_retains_its_page()
    {
        KeyboardInput input = new();
        DialoguePageCache cache = new();
        DialogueState dialogue = new(0, 1, "First{Page}" + Template, 8, 8);
        (IReadOnlyList<DialogueVisualPage> Pages, DialoguePager Pager) Get()
        {
            string expanded = DialogueTextFormatter.ExpandButtonBindings(dialogue.Text, input.NativeButtonBindingHint);
            return cache.Get(dialogue, null, "Arial", 8, expanded, false, () =>
            {
                DialogueDocument document = DialogueTextFormatter.Parse(dialogue.Text, buttonBindingHint: input.NativeButtonBindingHint);
                return DialogueLayout.Create(document, dialogue, null, 10, text => text.Length);
            }, retainPosition: true);
        }
        var first = Get();
        Assert.True(first.Pager.Advance(first.Pages.Count));
        input.Profile = input.Profile.WithBinding(ControlAction.Cancel, ControlDevice.Keyboard, new([Keys.K], []));
        var changed = Get();
        Assert.NotSame(first.Pages, changed.Pages);
        Assert.Equal(1, changed.Pager.PageIndex);
        Assert.Contains(changed.Pages[1].Lines.SelectMany(line => line.Runs), run => run.Run.Text.Contains("K4", StringComparison.Ordinal));
        Assert.Same(changed.Pages, Get().Pages);
    }
}
