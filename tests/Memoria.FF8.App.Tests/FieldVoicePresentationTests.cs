using System.Buffers.Binary;
using Memoria.FF8.Events;
using Memoria.FF8.Mods;
using Memoria.FF8.Resources;
using Memoria.FF8.GameUi;

namespace Memoria.FF8.App.Tests;

public sealed class FieldVoicePresentationTests
{
    [Fact]
    public void Opening_reveal_and_confirmability_changes_do_not_replay_a_clip()
    {
        Harness test = new();
        DialogueState dialogue = Dialogue() with { CanConfirm = false, NativeWindow = new(1, 0, 512) };
        test.Update(dialogue);
        Assert.Empty(test.Played);
        dialogue.NativeWindow.Progress = 512;
        test.Update(dialogue);
        test.Update(dialogue with { CanConfirm = true });
        Assert.Single(test.Played);
    }

    [Fact]
    public void Authored_page_replaces_audio_but_visual_wrapping_does_not()
    {
        Harness test = new();
        DialogueState dialogue = Dialogue();
        test.Update(dialogue);
        test.Update(dialogue);
        test.Update(dialogue, page: 1);
        Assert.Equal(new[] { 0, 1 }, test.Played.Select(item => item.Page));
        string text = string.Join("{Line}", Enumerable.Range(0, 26).Select(index => "Line " + index)) + "{Next}End";
        var pages = DialogueLayout.Create(DialogueTextFormatter.Parse(text), dialogue, null, 10, value => value.Length * 10);
        Assert.Equal(new[] { 0, 0, 1 }, pages.Select(page => page.SourcePageIndex));
    }

    [Fact]
    public void Missing_page_keeps_previous_audio_without_auto_confirm()
    {
        Harness test = new();
        DialogueState dialogue = Dialogue();
        test.Update(dialogue);
        int stops = test.Stopped.Count;
        test.Missing = true;
        test.Update(dialogue, 1);
        Assert.Equal(stops, test.Stopped.Count);
        test.Playing = false;
        test.Update(dialogue, 1);
        Assert.False(test.Presentation.CanAutoAdvance(0));
    }

    [Fact]
    public void Choice_change_uses_native_line_and_never_auto_chooses()
    {
        Harness test = new();
        DialogueState dialogue = Dialogue() with { FirstChoice = 4, LastChoice = 6 };
        test.Update(dialogue, choice: 4);
        test.Update(dialogue, choice: 4);
        test.Update(dialogue, choice: 6);
        Assert.Equal(new int?[] { null, 4, 6 }, test.Played.Select(item => item.Choice));
        test.Playing = false;
        test.Update(dialogue, choice: 6);
        Assert.False(test.Presentation.CanAutoAdvance(0));
    }

    [Fact]
    public void Completion_is_retained_until_authored_page_changes_but_failed_output_cannot()
    {
        Harness test = new();
        DialogueState dialogue = Dialogue();
        test.Update(dialogue);
        test.Playing = false;
        test.Update(dialogue);
        Assert.True(test.Presentation.CanAutoAdvance(0));
        Assert.True(test.Presentation.CanAutoAdvance(0));
        test.Playing = true;
        test.Update(dialogue, page: 1);
        Assert.False(test.Presentation.CanAutoAdvance(0));
        test.OutputAccepted = false;
        test.Update(Dialogue());
        Assert.False(test.Presentation.CanAutoAdvance(0));
    }

    [Fact]
    public void Completed_voice_remains_available_through_visual_continuations_until_authored_boundary()
    {
        Harness test = new();
        DialogueState dialogue = Dialogue();
        string text = string.Join("{Line}", Enumerable.Range(0, 26).Select(index => "Line " + index)) + "{Next}End";
        var pages = DialogueLayout.Create(DialogueTextFormatter.Parse(text), dialogue, null, 10, value => value.Length * 10);
        DialoguePager pager = new();
        test.Update(dialogue);
        test.Playing = false;
        test.Update(dialogue);
        Assert.True(test.Presentation.CanAutoAdvance(0));
        Assert.False(pager.IsPageRevealed(pages[0]));
        pager.UpdateReveal(TimeSpan.FromSeconds(60), pages[0], 3);
        Assert.True(pager.IsPageRevealed(pages[0]));
        Assert.True(pager.Advance(pages.Count));
        test.Update(dialogue, pages[pager.PageIndex].SourcePageIndex);
        Assert.True(test.Presentation.CanAutoAdvance(0));
        Assert.Single(test.Played);
        Assert.False(pager.IsPageRevealed(pages[1]));
        pager.UpdateReveal(TimeSpan.FromSeconds(60), pages[1], 3);
        Assert.True(pager.IsPageRevealed(pages[1]));
        Assert.True(pager.Advance(pages.Count));
        test.Playing = true;
        test.Update(dialogue, pages[pager.PageIndex].SourcePageIndex);
        Assert.False(test.Presentation.CanAutoAdvance(0));
        Assert.Equal(2, test.Played.Count);
    }

    [Fact]
    public void Overlapping_windows_cannot_auto_confirm_another_message()
    {
        Harness test = new();
        var first = Dialogue();
        var second = Dialogue() with { Channel = 1 };
        test.Presentation.Synchronize("scene", test.Owner, [new(first, 0), new(second, 0)]);
        test.Playing = false;
        test.Presentation.Synchronize("scene", test.Owner, [new(first, 0), new(second, 0)]);
        Assert.False(test.Presentation.CanAutoAdvance(0));
        Assert.False(test.Presentation.CanAutoAdvance(1));
    }

    [Fact]
    public void Closing_field_departure_and_new_load_cancel_old_audio()
    {
        Harness test = new();
        DialogueState dialogue = Dialogue();
        test.Update(dialogue);
        test.Presentation.Synchronize("scene", test.Owner, []);
        Assert.Equal(0, test.Stopped.Last());
        test.Update(dialogue);
        int count = test.Played.Count;
        test.Presentation.Synchronize("scene", new object(), [new(dialogue, 0)]);
        Assert.Equal(count + 1, test.Played.Count);
        test.Presentation.Synchronize(null, null, []);
        Assert.False(test.Presentation.CanAutoAdvance(0));
    }

    [Fact]
    public void Closing_animation_stops_audio_before_window_disappears()
    {
        Harness test = new();
        DialogueState dialogue = Dialogue();
        test.Update(dialogue);
        dialogue = dialogue with { NativeWindow = dialogue.NativeWindow! with { Delta = -512 } };
        test.Update(dialogue);
        Assert.Equal(0, test.Stopped.Last());
        Assert.False(test.Presentation.CanAutoAdvance(0));
    }

    [Fact]
    public void Voice_gain_scales_signed_pcm_with_saturation_and_preserves_zero()
    {
        byte[] pcm = new byte[8];
        short[] original = [1000, -1000, 30000, -30000];
        for (int i = 0; i < original.Length; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(i * 2), original[i]);
        }
        FieldAudioPresentation.ApplyVoiceGain(pcm, 2.75);
        Assert.Equal(new short[] { 2750, -2750, short.MaxValue, short.MinValue }, Enumerable.Range(0, 4).Select(i => BinaryPrimitives.ReadInt16LittleEndian(pcm.AsSpan(i * 2))));
        FieldAudioPresentation.ApplyVoiceGain(pcm, 0);
        Assert.All(pcm, value => Assert.Equal(0, value));
        Assert.Throws<InvalidDataException>(() => FieldAudioPresentation.ApplyVoiceGain(pcm, double.NaN));
    }

    [Fact]
    public void Voice_volume_is_an_independent_validated_preference()
    {
        UiPreferences preferences = new UiPreferences().WithInstallationSetting("Voice volume", "27%", activeGame: true);
        Assert.Equal(27, preferences.VoiceVolume);
        Assert.Equal(100, preferences.SoundVolume);
        Assert.Equal(100, preferences.MusicVolume);
        Assert.Throws<ArgumentException>(() => preferences.WithInstallationSetting("Voice volume", "101%", activeGame: true));
    }

    private static DialogueState Dialogue() => new(0, 3, "Text", 0, 0, NativeWindow: new(1, 512, 0));

    private sealed class Harness
    {
        public object Owner { get; } = new();
        public List<(int Page, int? Choice)> Played { get; } = [];
        public List<int> Stopped { get; } = [];
        public bool Playing { get; set; } = true;
        public bool Missing { get; set; }
        public bool OutputAccepted { get; set; } = true;
        public FieldVoicePresentation Presentation { get; }

        public Harness()
        {
            (int Page, int? Choice) requested = default;
            Presentation = new FieldVoicePresentation((field, channel, message, page, choice) =>
            {
                requested = (page, choice);
                return Missing ? null : new ExternalVoiceSelection(new ResourceKey("voice/scene/3.ogg"), "package", "voice/scene/3.ogg", 2.75);
            }, (channel, key, gain) =>
            {
                Played.Add(requested);
                return OutputAccepted;
            }, Stopped.Add, channel => Playing);
        }

        public void Update(DialogueState dialogue, int page = 0, int? choice = null) => Presentation.Synchronize("scene", Owner, [new(dialogue, page, choice)]);
    }
}
