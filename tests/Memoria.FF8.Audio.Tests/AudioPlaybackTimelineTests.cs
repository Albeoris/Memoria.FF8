using Memoria.FF8.Audio;

namespace Memoria.FF8.Tests;

public sealed class AudioPlaybackTimelineTests
{
    [Fact]
    public void Queued_output_tracks_real_source_positions_across_pause_and_loop()
    {
        PcmMixer mixer = new(100);
        using PcmMixer.Voice voice = mixer.Create(new byte[20], 100, 1);
        voice.Play(true);
        mixer.Render(new short[16]);
        Assert.Equal(16, voice.PositionBytesBeforeQueuedFrames(0));
        voice.Pause();
        mixer.Render(new short[8]);
        Assert.Equal(16, voice.PositionBytesBeforeQueuedFrames(2));
        Assert.Equal(12, voice.PositionBytesBeforeQueuedFrames(6));
        voice.Resume();
        mixer.Render(new short[8]);
        Assert.Equal(4, voice.PositionBytesBeforeQueuedFrames(0));
        Assert.Equal(18, voice.PositionBytesBeforeQueuedFrames(3));
        voice.Position(6);
        Assert.Equal(6, voice.PositionBytesBeforeQueuedFrames(0));
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(1.75)]
    [InlineData(4)]
    public void Queued_cursor_uses_actual_source_frames_across_variable_speed_changes(double speed)
    {
        PcmMixer mixer = new();
        using PcmMixer.Voice voice = mixer.Create(new byte[44100 * 2], 44100, 1);
        voice.Play(true);
        mixer.Render(new short[160]);
        voice.Pause();
        mixer.Render(new short[40]);
        voice.Resume();
        voice.Tempo(speed);
        mixer.Render(new short[200]);
        Assert.Equal((int)(80 + 100 * speed) * 2, voice.PositionBytesBeforeQueuedFrames(0));
        Assert.Equal((int)(80 + 50 * speed) * 2, voice.PositionBytesBeforeQueuedFrames(50));
        Assert.Equal(80 * 2, voice.PositionBytesBeforeQueuedFrames(120));
        Assert.Equal(70 * 2, voice.PositionBytesBeforeQueuedFrames(130));
        voice.Tempo(1);
        mixer.Render(new short[20]);
        Assert.Equal((int)(80 + 90 * speed) * 2, voice.PositionBytesBeforeQueuedFrames(20));
    }

    [Fact]
    public async Task Exact_stream_frame_seek_does_not_round_the_saved_position()
    {
        using PcmStreamSource source = await PcmStreamSource.CreateAtFrameAsync(new MemoryStream(new byte[12000]), 1001, TestContext.Current.CancellationToken);
        Assert.Equal(1001, source.PositionFrames);
        source.RenderFrame(out _, out _);
        Assert.Equal(1002, source.PositionFrames);
    }

    [Fact]
    public async Task Stream_cursor_history_preserves_offset_pause_and_silent_output()
    {
        using PcmStreamSource source = await PcmStreamSource.CreateAsync(new MemoryStream(new byte[80]), 2d / 44100, TestContext.Current.CancellationToken);
        source.RenderFrame(out _, out _);
        source.RenderFrame(out _, out _);
        source.Pause(true);
        source.RenderFrame(out _, out _);
        source.RecordSilentFrame();
        Assert.Equal(4, source.PositionBeforeQueuedFrames(0));
        Assert.Equal(3, source.PositionBeforeQueuedFrames(3));
        Assert.Equal(2, source.PositionBeforeQueuedFrames(4));
    }
}
