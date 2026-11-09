using System.Buffers.Binary;
using Memoria.FF8.Assets;
using Memoria.FF8.Audio;
using Memoria.FF8.Events;
using Memoria.FF8.Mods;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class PcmConcertPresentationTests
{
    [Fact]
    public async Task GuitarUsesItsOggReplacementAndSkipsOnlySilentPreviewFrames()
    {
        const uint mask = 0xFF0;
        ModCatalogSnapshot catalog = Catalog(mask);
        PcmMixer mixer = new();
        int attached = 0;
        using FieldAudioPresentation output = new(TestResources.GameDirectory, "", () => catalog)
        {
            DecodeOverride = (_, _, _) => Task.FromResult(Pcm(0, 0, 1000, 2000, 3000)),
            OutputFactory = () =>
            {
                attached++;
                return new PcmAudioOutput(mixer);
            }
        };
        output.Execute(Request(mask));
        await PumpUntil(output, () => attached == 1);
        short[] frame = new short[2];
        mixer.Render(frame);
        Assert.Equal(new short[] { 1000, -1000 }, frame);
        Assert.Equal(43, output.PrimaryMusicId);
        Assert.Null(output.Error);

        GameplaySaveAudioPresentation saved = Assert.IsType<GameplaySaveAudioPresentation>(output.CapturePlaybackPositions());
        GameplaySaveAudioTrack track = Assert.Single(saved.Music!);
        GameplaySaveAudioPosition position = Assert.Single(track.Positions);
        Assert.Equal(3, position.Position);
        output.StopAll();
        output.SetPlaybackResumePositions(saved);
        output.ResumeMusic(NativeMusicPlaybackState.FromColdProcess().SetGain46BB40(0, 127).Concert46B990(mask));
        await PumpUntil(output, () => output.CapturePlaybackPositions()?.Music?.Any(track => track.MusicId == 43) == true);
        mixer.Render(frame);
        Assert.Equal(new short[] { 2000, -2000 }, frame);
    }

    [Fact]
    public async Task CombinedConcertWaitsForAllDecodesAndPreservesRelativeEntrancesAndLoops()
    {
        const uint mask = 2 | 4;
        ModCatalogSnapshot catalog = Catalog(mask);
        TaskCompletionSource<PcmAudio> second = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource requestedSecond = new(TaskCreationOptions.RunContinuationsAsynchronously);
        PcmMixer mixer = new();
        int attached = 0;
        using FieldAudioPresentation output = new(TestResources.GameDirectory, "", () => catalog)
        {
            DecodeOverride = (_, bytes, _) =>
            {
                if (bytes![^1] == 0)
                {
                    return Task.FromResult(Pcm(0, 1000, 2000, 3000));
                }
                requestedSecond.TrySetResult();
                return second.Task;
            },
            OutputFactory = () =>
            {
                attached++;
                return new PcmAudioOutput(mixer);
            }
        };
        output.Execute(Request(mask));
        await requestedSecond.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(0, attached);
        second.SetResult(Pcm(0, 0, 4000, 5000));
        await PumpUntil(output, () => attached == 1);
        short[] frames = new short[12];
        mixer.Render(frames);
        Assert.Equal(new short[] { 0, 0, 1000, -1000, 6000, -6000, 8000, -8000, 0, 0, 1000, -1000 }, frames);
        GameplaySaveAudioPresentation saved = Assert.IsType<GameplaySaveAudioPresentation>(output.CapturePlaybackPositions());
        GameplaySaveAudioTrack track = Assert.Single(saved.Music);
        track.Positions[1] = track.Positions[1] with { Fingerprint = new string('F', 64) };
        output.StopAll();
        output.SetPlaybackResumePositions(saved);
        output.ResumeMusic(NativeMusicPlaybackState.FromColdProcess().SetGain46BB40(0, 127).Concert46B990(mask));
        await PumpUntil(output, () => output.CapturePlaybackPositions()?.Music?.Any(item => item.MusicId == 43) == true);
        short[] first = new short[2];
        mixer.Render(first);
        Assert.Equal(new short[2], first);
    }

    [Fact]
    public async Task PendingConcertUsesCurrentGainAndFocusAndStopDetachesIt()
    {
        const uint mask = 0xFF0;
        ModCatalogSnapshot catalog = Catalog(mask);
        TaskCompletionSource<PcmAudio> decoded = new(TaskCreationOptions.RunContinuationsAsynchronously);
        PcmMixer mixer = new();
        int attached = 0;
        using FieldAudioPresentation output = new(TestResources.GameDirectory, "", () => catalog)
        {
            DecodeOverride = (_, _, _) => decoded.Task,
            OutputFactory = () =>
            {
                attached++;
                return new PcmAudioOutput(mixer);
            }
        };
        output.Execute(Request(mask));
        output.SetOutputPreferences(0, 100);
        output.SetFocusPaused(true);
        decoded.SetResult(Pcm(1000, 2000, 3000));
        await PumpUntil(output, () => attached == 1);
        short[] frame = new short[2];
        mixer.Render(frame);
        Assert.Equal(new short[2], frame);
        output.SetOutputPreferences(100, 100);
        mixer.Render(frame);
        Assert.Equal(new short[2], frame);
        output.SetFocusPaused(false);
        mixer.Render(frame);
        Assert.Equal(new short[] { 1000, -1000 }, frame);
        output.SetMusicGain(0, null, 0, 0);
        mixer.Render(frame);
        Assert.Equal(new short[2], frame);
        output.SetMusicGain(0, null, 127, 0);
        mixer.Render(frame);
        Assert.Equal(new short[] { 3000, -3000 }, frame);
        output.StopAll();
        mixer.Render(frame);
        Assert.Equal(new short[2], frame);
        Assert.Null(output.Error);
    }

    [Fact]
    public async Task StoppingPendingConcertDoesNotAttachItsLateDecodedSource()
    {
        const uint mask = 0xFF0;
        ModCatalogSnapshot catalog = Catalog(mask);
        TaskCompletionSource<PcmAudio> decoded = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource requested = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using FieldAudioPresentation output = new(TestResources.GameDirectory, "", () => catalog)
        {
            DecodeOverride = (_, _, _) =>
            {
                requested.TrySetResult();
                return decoded.Task;
            },
            OutputFactory = () => throw new InvalidOperationException("A cancelled concert must not create a device.")
        };
        output.Execute(Request(mask));
        await requested.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        output.StopAll();
        decoded.SetResult(Pcm(1000));
        for (int index = 0; index < 10; index++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
            output.Update();
        }
        Assert.Equal(-1, output.PrimaryMusicId);
        Assert.Null(output.Error);
    }

    private static NativeMusicPlayRequest Request(uint mask)
    {
        return new(1, 634, 0, 0, 0, 0, "CHOICEMUSIC", true, 43, null, null, null, null, null, 0, GlobalSoundBackendStatus.Muted, ChoiceMask: mask)
        {
            Playback = new(0, 43, false, true, true, null, null)
        };
    }

    private static ModCatalogSnapshot Catalog(uint mask)
    {
        ModPackage package = new("example", "Example", ModFormat.Junction, true, true, ModEffectiveStatus.Active, "example")
        {
            JunctionMetadata = new() { UseExternalMusic = true, ExternalMusicExtension = "ogg" }
        };
        ModFile[] files = NativeConcertSelection.Tracks(mask).Select((name, index) => new ModFile(package.Id, ModFormat.Junction, $"music/{ExternalMusicResolver.TrackName(name)}.ogg", 1, () => [.. ExternalMusicPcmTests.Ogg(44100), checked((byte)index)])).ToArray();
        return new(1, [package], files);
    }

    private static PcmAudio Pcm(params short[] samples)
    {
        byte[] bytes = new byte[samples.Length * 4];
        for (int index = 0; index < samples.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 4), samples[index]);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 4 + 2), checked((short)-samples[index]));
        }
        return new(bytes, 44100, 2);
    }

    private static async Task PumpUntil(FieldAudioPresentation output, Func<bool> condition)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            output.Update();
            Assert.Null(output.Error);
            await Task.Delay(5, timeout.Token);
        }
    }
}
