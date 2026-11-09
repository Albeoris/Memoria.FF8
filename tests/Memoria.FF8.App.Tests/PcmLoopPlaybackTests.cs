using Memoria.FF8.Audio;

namespace Memoria.FF8.App.Tests;

public sealed class PcmLoopPlaybackTests
{
    [Fact]
    public void Intro_hands_over_once_at_the_original_deadline_and_preserves_loop_overshoot()
    {
        using PcmAudioOutput output = new(new PcmMixer(1000));
        PcmAudioOutput.Buffer primary = output.Create(new byte[4000], 1000, 2);
        PcmAudioOutput.Buffer loop = output.Create(new byte[400], 1000, 2);
        primary.Play(false);
        using PcmLoopPlayback playback = new(primary, loop, 75, 1, 4000, 400);
        playback.UpdateAt(0.99);
        Assert.False(playback.LoopStarted);
        Assert.True(primary.IsPlaying);
        playback.UpdateAt(1.125);
        Assert.True(playback.LoopStarted);
        Assert.False(primary.IsPlaying);
        Assert.True(loop.IsPlaying);
        Assert.Equal(100, loop.PositionBytes);
        playback.UpdateAt(20);
        Assert.Equal(100, loop.PositionBytes);
        playback.SetGain(-2400);
        Assert.Equal(-2400, primary.GainValue);
        Assert.Equal(-2400, loop.GainValue);
    }

    [Fact]
    public void Pause_prevents_transition_and_resumes_the_existing_buffers()
    {
        using PcmAudioOutput output = new(new PcmMixer(1000));
        PcmAudioOutput.Buffer primary = output.Create(new byte[4000], 1000, 2);
        PcmAudioOutput.Buffer loop = output.Create(new byte[400], 1000, 2);
        primary.Play(false);
        using PcmLoopPlayback playback = new(primary, loop, 100, 1, 4000, 400);
        playback.Pause(true);
        playback.UpdateAt(2);
        Assert.False(playback.LoopStarted);
        Assert.False(primary.IsPlaying);
        playback.Pause(false);
        Assert.True(primary.IsPlaying);
        Assert.False(loop.IsPlaying);
        playback.UpdateAt(1.125);
        Assert.True(playback.LoopStarted);
    }

    [Fact]
    public void Cold_restored_loop_retains_its_position_and_owns_both_buffers_until_disposal()
    {
        using PcmAudioOutput output = new(new PcmMixer(1000));
        PcmAudioOutput.Buffer primary = output.Create(new byte[4000], 1000, 2);
        PcmAudioOutput.Buffer loop = output.Create(new byte[400], 1000, 2);
        loop.Position(80);
        loop.Play(true);
        PcmLoopPlayback playback = new(primary, loop, 100, 0, 4000, 400, true);
        playback.UpdateAt(10);
        Assert.Equal(80, loop.PositionBytes);
        Assert.False(primary.IsPlaying);
        Assert.True(loop.IsPlaying);
        playback.Dispose();
        Assert.True(primary.IsDisposed);
        Assert.True(loop.IsDisposed);
    }
}
