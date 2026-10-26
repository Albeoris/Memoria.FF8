using System.Reflection;

namespace Memoria.FF8.App.Tests;

public sealed class FocusAudioTests
{
    [Fact]
    public void PausedRampsExcludeBackgroundTimeAndResumeFromTheirPriorGain()
    {
        ManualTimeProvider time = new();
        FieldAudioPresentation.Ramp ramp = new(0, 100, 1000, time);
        time.Advance(250);
        Assert.Equal(25, ramp.Value);
        ramp.Pause();
        time.Advance(5000);
        ramp.Pause();
        Assert.Equal(25, ramp.Value);
        Assert.False(ramp.Finished);
        ramp.Resume();
        time.Advance(250);
        ramp.Resume();
        Assert.Equal(50, ramp.Value);
        time.Advance(500);
        Assert.True(ramp.Finished);
        Assert.Equal(100, ramp.Value);
    }

    [Fact]
    public void FocusPausesBothMusicRampsWithoutLoadingGameResources()
    {
        ManualTimeProvider time = new();
        FieldAudioPresentation.Ramp midi = new(100, 0, 1000, time);
        FieldAudioPresentation.Ramp stream = new(0, 100, 1000, time);
        using FieldAudioPresentation output = new(0, "", "");
        SetField(output, "midiRamp", midi);
        SetField(output, "streamRamp", stream);
        time.Advance(250);
        output.SetFocusPaused(true);
        time.Advance(5000);
        output.SetFocusPaused(true);
        output.Update();
        Assert.Equal(75, midi.Value);
        Assert.Equal(25, stream.Value);
        Assert.Null(output.Error);
        output.SetFocusPaused(false);
        time.Advance(250);
        output.SetFocusPaused(false);
        Assert.Equal(50, midi.Value);
        Assert.Equal(50, stream.Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PortableDevicePausePreservesCursorAndPlaybackMode(bool loop)
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("MEMORIA_FF8_AUDIO_DEVICE_TESTS") == "1", "Set MEMORIA_FF8_AUDIO_DEVICE_TESTS=1 to exercise an actual playback device.");
        using PcmAudioOutput output = new(0);
        using PcmAudioOutput.Buffer buffer = output.Create(new byte[44100 * 2 * 5], 44100, 1);
        buffer.Play(loop);
        Assert.True(SpinWait.SpinUntil(() => buffer.PositionBytes > 0, TimeSpan.FromSeconds(2)), "DirectSound did not advance its playback cursor.");
        buffer.Pause();
        int position = buffer.PositionBytes;
        Assert.False(buffer.IsPlaying);
        await Task.Delay(30, TestContext.Current.CancellationToken);
        buffer.Pause();
        Assert.Equal(position, buffer.PositionBytes);
        buffer.Resume();
        Assert.True(buffer.IsPlaying);
        buffer.Resume();
        buffer.Stop();
        buffer.Pause();
        buffer.Resume();
        Assert.False(buffer.IsPlaying);
    }

    [Fact]
    public void PortableDeviceFocusResumeDoesNotRestartACompletedBuffer()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("MEMORIA_FF8_AUDIO_DEVICE_TESTS") == "1", "Set MEMORIA_FF8_AUDIO_DEVICE_TESTS=1 to exercise an actual playback device.");
        using PcmAudioOutput output = new(0);
        using PcmAudioOutput.Buffer buffer = output.Create(new byte[882], 44100, 1);
        buffer.Play(false);
        Assert.True(SpinWait.SpinUntil(() => !buffer.IsPlaying, TimeSpan.FromSeconds(2)), "The short DirectSound buffer did not finish.");
        buffer.Pause();
        buffer.Resume();
        Assert.False(buffer.IsPlaying);
    }

    private static void SetField(object target, string name, object value)
    {
        typeof(FieldAudioPresentation).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => timestamp;
        public void Advance(int milliseconds)
        {
            timestamp += milliseconds * TimeSpan.TicksPerMillisecond;
        }
    }

}
