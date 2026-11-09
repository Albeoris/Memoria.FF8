namespace Memoria.FF8;

public sealed class PcmLoopTempoTests
{
    [Fact]
    public void Sound_tempo_applies_to_existing_and_new_voices_while_music_keeps_its_position_rate()
    {
        Audio.PcmMixer mixer = new(1000);
        using PcmAudioOutput output = new(mixer);
        using PcmAudioOutput.Buffer music = output.Create(new byte[12000], 1000, 1);
        music.Play(false);
        using Ff7SoundChannels sounds = new();
        sounds.Play(output, new byte[12000], 1000, 1, false, 0, 0, 1, fingerprint: new string('0', 64));
        sounds.SetTempo(2);
        mixer.Render(new short[200]);
        Assert.Equal(200, sounds.Capture(0).Single().PositionFrames);
        Assert.Equal(200, music.PositionBytes);
        sounds.SetTempo(1);
        mixer.Render(new short[200]);
        Assert.Equal(300, sounds.Capture(0).Single().PositionFrames);
        sounds.SetTempo(6);
        sounds.Play(output, new byte[12000], 1000, 1, false, 0, 0, 2, fingerprint: new string('0', 64));
        mixer.Render(new short[200]);
        Assert.Equal(600, sounds.Capture(0).Single().PositionFrames);
        Assert.Equal(600, music.PositionBytes);
    }

    [Fact]
    public void Intro_deadline_accumulates_each_tempo_interval_and_preserves_repeat_phase()
    {
        using PcmAudioOutput output = new(new Audio.PcmMixer(1000));
        PcmAudioOutput.Buffer primary = output.Create(new byte[4000], 1000, 2);
        PcmAudioOutput.Buffer repeat = output.Create(new byte[400], 1000, 2);
        primary.Play(false);
        using PcmLoopPlayback playback = new(primary, repeat, 127, 1, 4000, 400);
        playback.SetTempoAt(6, 0);
        playback.UpdateAt(0.125);
        Assert.False(playback.LoopStarted);
        playback.SetTempoAt(2, 0.125);
        playback.UpdateAt(0.3125);
        Assert.True(playback.LoopStarted);
        Assert.False(primary.IsPlaying);
        Assert.True(repeat.IsPlaying);
        Assert.Equal(100, repeat.PositionBytes);
        playback.SetTempoAt(1, 0.3125);
        Assert.Equal(100, repeat.PositionBytes);
    }

    [Fact]
    public void Default_tempo_retains_the_existing_intro_deadline()
    {
        using PcmAudioOutput output = new(new Audio.PcmMixer(1000));
        PcmAudioOutput.Buffer primary = output.Create(new byte[4000], 1000, 2);
        PcmAudioOutput.Buffer repeat = output.Create(new byte[400], 1000, 2);
        primary.Play(false);
        using PcmLoopPlayback playback = new(primary, repeat, 127, 1, 4000, 400);
        playback.UpdateAt(0.75);
        Assert.False(playback.LoopStarted);
        playback.UpdateAt(1.125);
        Assert.Equal(100, repeat.PositionBytes);
        Assert.True(playback.LoopStarted);
    }
}
