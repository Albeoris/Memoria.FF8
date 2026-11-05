using System.Reflection;
using Memoria.FF8.Assets;
using Memoria.FF8.Audio;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class SoundSpeedTests
{
    [Theory]
    [InlineData(0.5)]
    [InlineData(0.75)]
    [InlineData(10)]
    [InlineData(30)]
    public void SoundClockIntegratesSpeedChangesAndExcludesPauses(double speed)
    {
        ManualTime time = new();
        FieldAudioPresentation.SoundClock clock = new(time);
        time.Advance(100);
        clock.SetSpeed(speed);
        time.Advance(100);
        Assert.Equal(100 + 100 * speed, clock.Elapsed.TotalMilliseconds);
        clock.Stop();
        time.Advance(2000);
        clock.SetSpeed(1);
        clock.Start();
        time.Advance(100);
        Assert.Equal(200 + 100 * speed, clock.Elapsed.TotalMilliseconds);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(2.5)]
    [InlineData(30)]
    public void SoundRampPreservesGainWhenSpeedChanges(double speed)
    {
        ManualTime time = new();
        FieldAudioPresentation.Ramp ramp = new(0, 100, 1000, time);
        time.Advance(100);
        ramp.SetSpeed(speed);
        Assert.Equal(10, ramp.Value);
        time.Advance(600 / speed);
        Assert.Equal(70, ramp.Value);
        ramp.Pause();
        time.Advance(1000);
        Assert.Equal(70, ramp.Value);
        ramp.Resume();
        time.Advance(300 / speed);
        Assert.True(ramp.Finished);
    }

    [Theory]
    [InlineData(false, 0.5)]
    [InlineData(true, 0.5)]
    [InlineData(false, 2.5)]
    [InlineData(true, 2.5)]
    [InlineData(false, 30)]
    [InlineData(true, 30)]
    public void InstalledSoundUsesSelectedTempoForNewAndExistingVoices(bool changeWhilePlaying, double speed)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(directory), "Set MEMORIA_FF8_GAME_DIR to an installed game.");
        PcmMixer mixer = new();
        using FieldAudioPresentation output = new(directory!, "") { OutputFactory = () => new PcmAudioOutput(mixer) };
        if (!changeWhilePlaying)
        {
            output.SetSoundSpeed(speed);
        }
        GlobalSoundResource resource = GameWorld.CreateMutedGlobalSoundResource(GlobalAudioFormat.Read(directory!, 69))!;
        output.Execute(new GlobalSoundRequest(10103, 1, 1, 1, 128, 64, 0, 100, resource));
        Assert.Null(output.Error);
        var voices = (System.Collections.IDictionary)typeof(FieldAudioPresentation).GetField("voices", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(output)!;
        object voice = voices[1]!;
        PcmAudioOutput.Buffer primary = (PcmAudioOutput.Buffer)voice.GetType().GetProperty("Primary")!.GetValue(voice)!;
        if (changeWhilePlaying)
        {
            mixer.Render(new short[4410 * 2]);
            output.SetSoundSpeed(speed);
        }
        PcmAudio pcm = PcmAudioDecoder.ReadSample(directory!, 69);
        int remainingSourceFrames = (pcm.Bytes.Length - primary.PositionBytes) / (pcm.Channels * 2);
        int wallFrames = (int)Math.Ceiling(remainingSourceFrames * 44100d / pcm.SampleRate / speed);
        mixer.Render(new short[(wallFrames + 2) * 2]);
        Assert.False(primary.IsPlaying);
        Assert.Equal(pcm.Bytes.Length, primary.PositionBytes);
        Assert.Null(output.Error);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(2.5)]
    [InlineData(30)]
    public void InstalledSecondaryLoopStartsOnAcceleratedSoundClock(double speed)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(directory), "Set MEMORIA_FF8_GAME_DIR to an installed game.");
        PcmMixer mixer = new();
        ManualTime time = new();
        using FieldAudioPresentation output = new(directory!, "") { OutputFactory = () => new PcmAudioOutput(mixer), SoundTimeProvider = time };
        output.SetSoundSpeed(speed);
        GlobalSoundResource resource = GameWorld.CreateMutedGlobalSoundResource(GlobalAudioFormat.Read(directory!, 2253))!;
        Assert.True(resource.LoopPolicy.LoopStartPcmByte > 0);
        output.Execute(new GlobalSoundRequest(10103, 1, 1, 1, 128, 64, 0, 100, resource));
        var voices = (System.Collections.IDictionary)typeof(FieldAudioPresentation).GetField("voices", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(output)!;
        object voice = voices[1]!;
        PcmAudioOutput.Buffer primary = (PcmAudioOutput.Buffer)voice.GetType().GetProperty("Primary")!.GetValue(voice)!;
        PcmAudioOutput.Buffer loop = (PcmAudioOutput.Buffer)voice.GetType().GetProperty("Loop")!.GetValue(voice)!;
        int delay = resource.LoopPolicy.LoopTransitionDelayMilliseconds!.Value;
        time.Advance(Math.Max(0, delay / speed - 1));
        output.Update();
        Assert.False(loop.IsPlaying);
        time.Advance(3);
        output.Update();
        Assert.True(loop.IsPlaying);
        Assert.False(primary.IsPlaying);
        Assert.Null(output.Error);
    }

    private sealed class ManualTime : TimeProvider
    {
        private long timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => timestamp;
        public void Advance(double milliseconds)
        {
            timestamp += (long)Math.Round(milliseconds * TimeSpan.TicksPerMillisecond);
        }
    }
}
