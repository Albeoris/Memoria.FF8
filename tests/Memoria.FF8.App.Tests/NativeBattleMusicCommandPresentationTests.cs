using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleMusicCommandPresentationTests
{
    [Fact]
    public void Pending_play_pauses_before_selection_and_resumes_only_after_the_gain_completion()
    {
        List<string> calls = [];
        Dispatch(new(2, new(0, 1, true, true, false, 63, null), PauseOutput: true), calls);
        Assert.Equal(new[] { "pause", "stop0", "play0:1:63" }, calls);
        Dispatch(new(3, GainChannel: 0, Gain: 127, ResumeOutput: true), calls);
        Assert.Equal(new[] { "pause", "stop0", "play0:1:63", "gain0::127:0", "resume" }, calls);
    }

    [Fact]
    public void Same_song_retains_the_paused_device_and_its_cursor_until_completion()
    {
        List<string> calls = [];
        Dispatch(new(2, new(0, 1, false, false, false, 63, null), PauseOutput: true), calls);
        Assert.Equal(new[] { "pause" }, calls);
        Dispatch(new(3, GainChannel: ushort.MaxValue, Gain: 92, InitialGain: 0, EffectiveDuration: 60, ResumeOutput: true), calls);
        Assert.Equal(new[] { "pause", "gain65535:0:92:60", "resume" }, calls);
    }

    [Fact]
    public void Stop_reaches_both_slots_and_fade_does_not_resume_a_paused_backend()
    {
        List<string> calls = [];
        Dispatch(new(1), calls);
        Dispatch(new(4, GainChannel: 0, Gain: 0), calls);
        Assert.Equal(new[] { "stop0", "stop1", "gain0::0:0" }, calls);
    }

    [Fact]
    public void Unknown_command_has_no_device_effect_including_no_unconditional_resume()
    {
        List<string> calls = [];
        Dispatch(new(255), calls);
        Assert.Empty(calls);
    }

    [Fact]
    public void Invalid_gain_or_branch_refuses_before_the_first_device_operation()
    {
        List<string> calls = [];
        Assert.Throws<NotSupportedException>(() => Dispatch(new(3, GainChannel: 0, Gain: 128, ResumeOutput: true), calls));
        Assert.Throws<NotSupportedException>(() => Dispatch(new(2, new(0, 1, true, true, false, 63, null), ResumeOutput: true), calls));
        Assert.Empty(calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Field_return_stops_battle_sounds_before_cross_or_unsigned_channel_fade(bool fading)
    {
        NativeBattleMusicReturnReceipt receipt = fading ? new(0, 255, null, ushort.MaxValue, 0, 60, 0) : new(0, 17, new(0, 17, true, true, false, 63, null), null, null, 0, 0);
        List<string> calls = [];
        FieldAudioPresentation.DispatchNativeBattleMusicReturn(receipt, () => calls.Add("stop-sfx"), decision => calls.Add($"cross{decision.MusicId}"), (channel, duration, target) => calls.Add($"fade{channel}:{duration}:{target}"));
        Assert.Equal(fading ? new[] { "stop-sfx", "fade65535:60:0" } : new[] { "stop-sfx", "cross17" }, calls);
    }

    [Fact]
    public void Kept_field_music_still_stops_effects_and_unsupported_effect_restore_refuses_atomically()
    {
        NativeBattleMusicReturnReceipt receipt = new(0x10, null, null, null, null, 0, 0);
        List<string> calls = [];
        FieldAudioPresentation.DispatchNativeBattleMusicReturn(receipt, () => calls.Add("stop-sfx"), _ => Assert.Fail("Keep branch does not play music."), (_, _, _) => Assert.Fail("Keep branch does not fade music."));
        Assert.Equal(new[] { "stop-sfx" }, calls);
        calls.Clear();
        Assert.Throws<InvalidDataException>(() => FieldAudioPresentation.DispatchNativeBattleMusicReturn(receipt with { RestoredEffectMask = 1 }, () => calls.Add("stop-sfx"), _ => calls.Add("play"), (_, _, _) => calls.Add("fade")));
        Assert.Empty(calls);
    }

    private static void Dispatch(NativeBattleMusicCommandReceipt receipt, List<string> calls)
    {
        FieldAudioPresentation.DispatchNativeBattleMusicCommand(receipt, () => calls.Add("pause"), () => calls.Add("resume"), slot => calls.Add($"stop{slot}"), decision => FieldAudioPresentation.DispatchNativeMusicPlayback(decision, slot => calls.Add($"stop{slot}"), (slot, id, gain) => calls.Add($"play{slot}:{id}:{gain}"), (slot, gain) => calls.Add($"gain{slot}:{gain}"), () => Assert.Fail("Native501B60 does not select concert instruments.")), (channel, initial, gain, duration) => calls.Add($"gain{channel}:{initial}:{gain}:{duration}"));
    }
}
