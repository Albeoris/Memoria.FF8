using System.Diagnostics;
using Memoria.FF8.Audio;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class DirectMusicPlaybackTests
{
    [Theory]
    [InlineData(0U)]
    [InlineData(2U | 4U)]
    [InlineData(0xFF0U)]
    public async Task NativeSegmentAndConcertRestoreOnTheSharedMixer(uint concertMask)
    {
        string directory = TestResources.GameDirectory;
        PcmMixer mixer = new();
        using PcmAudioOutput sound = new(mixer);
        using FieldAudioPresentation output = new(directory, "") { OutputFactory = () => sound, OwnsSoundOutput = false };
        output.SetFocusPaused(true);
        Play(output);
        output.SetMusicGain(0, null, 0, 0);
        await PumpUntil(output, () => Position(output) is not null);
        GameplaySaveAudioPosition paused = Position(output)!;
        Assert.True(paused.Paused);
        Assert.Equal(0, paused.Position);
        Assert.False(output.IsMidiPlaying);
        output.SetFocusPaused(false);
        short[] samples = new short[44100];
        mixer.Render(samples);
        Assert.All(samples, sample => Assert.Equal(0, sample));
        output.SetMusicGain(0, null, 127, 0);
        sound.Music.Gain = 0;
        mixer.Render(samples);
        Assert.All(samples, sample => Assert.Equal(0, sample));
        sound.Music.Gain = 1;
        GameplaySaveAudioPresentation saved = output.CapturePlaybackPositions()!;
        GameplaySaveAudioPosition position = Position(output)!;
        Assert.Equal("sgt-dls-v1", position.Backend);
        Assert.Equal("rendered-frames", position.Unit);
        Assert.Equal(samples.Length, position.Position);
        Assert.Equal(concertMask == 0xFF0, position.StartPoint > 0);
        Assert.Equal(64, position.Fingerprint.Length);
        mixer.Render(samples);
        Assert.Contains(samples, sample => sample != 0);
        output.StopAll();
        output.SetPlaybackResumePositions(saved);
        Play(output);
        await PumpUntil(output, () => Position(output) is not null);
        Assert.Equal(position, Position(output));
        short[] restored = new short[samples.Length];
        mixer.Render(restored);
        Assert.Equal(samples, restored);

        void Play(FieldAudioPresentation presentation)
        {
            if (concertMask == 0)
            {
                presentation.PlayMusic(5, 0, 127, -1);
                return;
            }
            presentation.Execute(new NativeMusicPlayRequest(1, 634, 0, 0, 0, 0, "CHOICEMUSIC", true, 43, null, null, null, null, null, 0, GlobalSoundBackendStatus.Muted, ChoiceMask: concertMask)
            {
                Playback = new(0, 43, false, true, true, null, null)
            });
        }
    }

    [Fact]
    public async Task NativeSegmentReplacementPreservesPendingStreamAndOtherBuses()
    {
        string directory = TestResources.GameDirectory;
        PcmMixer mixer = new();
        using PcmAudioOutput sound = new(mixer);
        TaskCompletionSource<PcmAudio> stream = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using FieldAudioPresentation output = new(directory, "")
        {
            OutputFactory = () => sound,
            OwnsSoundOutput = false,
            DecodeOverride = (_, _, _) => stream.Task
        };
        using PcmAudioOutput.Buffer effect = sound.Create([0x34, 0x12], 44100, 1);
        effect.Play(true);
        output.PlayMusic(3, 1, 127, -1);
        output.PlayMusic(5, 0, 127, -1);
        output.PlayMusic(26, 0, 127, -1);
        Assert.False(output.IsMidiPlaying);
        await PumpUntil(output, () => output.IsMidiPlaying);
        Assert.Equal(26, output.PrimaryMusicId);
        Assert.Equal(26, Assert.Single(output.CapturePlaybackPositions()!.Music).MusicId);
        stream.SetResult(new PcmAudio([0x78, 0x56], 44100, 1));
        await PumpUntil(output, () => output.CapturePlaybackPositions()!.Music.Length == 2);
        output.Execute(new NativeMusicStop(1, 0, 0, 0, 0, 0, 0, 0, 0, true, GlobalSoundBackendStatus.Muted));
        Assert.False(output.IsMidiPlaying);
        Assert.Equal(3, Assert.Single(output.CapturePlaybackPositions()!.Music).MusicId);
        Assert.True(effect.IsPlaying);
    }

    private static GameplaySaveAudioPosition? Position(FieldAudioPresentation output)
    {
        return output.CapturePlaybackPositions()?.Music.SingleOrDefault()?.Positions.Single();
    }

    private static async Task PumpUntil(FieldAudioPresentation output, Func<bool> condition)
    {
        Stopwatch deadline = Stopwatch.StartNew();
        while (!condition() && deadline.Elapsed < TimeSpan.FromSeconds(5))
        {
            output.Update();
            Assert.Null(output.Error);
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
        Assert.True(condition());
    }
}
