using System.Buffers.Binary;
using Memoria.FF8.Assets;
using Memoria.FF8.Audio;
using Memoria.FF8.Events;
using Memoria.FF8.Mods;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class PsfMusicPresentationTests
{
    [Fact]
    public async Task PendingStreamUsesCurrentGainAndFocusAndStopDisposesIt()
    {
        NativeMusicCatalog native = new(TestResources.GameDirectory);
        string name = ExternalMusicResolver.TrackName(native.MusicFileResolved(3));
        ModCatalogSnapshot catalog = Catalog([name]);
        TaskCompletionSource<Stream> decoded = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TrackingStream stream = new(Pcm(1000));
        PcmMixer mixer = new();
        int attached = 0;
        using FieldAudioPresentation output = new(TestResources.GameDirectory, "", () => catalog)
        {
            PsfStreamOverride = (_, _) =>
            {
                started.SetResult();
                return decoded.Task;
            },
            OutputFactory = () =>
            {
                attached++;
                return new PcmAudioOutput(mixer);
            }
        };
        output.PlayMusic(3, 0, 127, -1, identityResolved: true);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        output.SetOutputPreferences(0, 100);
        output.SetFocusPaused(true);
        decoded.SetResult(stream);
        await PumpUntil(output, () => attached == 1);
        short[] frame = new short[2];
        mixer.Render(frame);
        Assert.Equal(new short[2], frame);
        output.SetFocusPaused(false);
        mixer.Render(frame);
        Assert.Equal(new short[2], frame);
        output.SetOutputPreferences(100, 100);
        mixer.Render(frame);
        Assert.InRange(frame[0], (short)1, (short)3200);
        Assert.Equal(frame[0], frame[1]);
        output.StopAll();
        Assert.True(stream.Disposed);
        mixer.Render(frame);
        Assert.Equal(new short[2], frame);
        Assert.Null(output.Error);
    }

    [Fact]
    public async Task ConcertPreparesAllSelectedStemsBeforeTheirFirstSharedFrame()
    {
        const uint mask = 2 | 4;
        string[] names = NativeConcertSelection.Tracks(mask).Select(ExternalMusicResolver.TrackName).ToArray();
        ModCatalogSnapshot catalog = Catalog(names);
        TrackingStream first = new(Pcm(1000));
        TrackingStream second = new(Pcm(2000));
        TaskCompletionSource<Stream> delayed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource requestedSecond = new(TaskCreationOptions.RunContinuationsAsynchronously);
        PcmMixer mixer = new();
        int attached = 0;
        using FieldAudioPresentation output = new(TestResources.GameDirectory, "", () => catalog)
        {
            PsfStreamOverride = (selection, _) =>
            {
                if (selection.File.RelativePath.Contains(names[0], StringComparison.Ordinal))
                {
                    return Task.FromResult<Stream>(first);
                }
                requestedSecond.TrySetResult();
                return delayed.Task;
            },
            OutputFactory = () =>
            {
                attached++;
                return new PcmAudioOutput(mixer);
            }
        };
        NativeMusicPlayRequest request = new(1, 634, 0, 0, 0, 0, "CHOICEMUSIC", true, 43, null, null, null, null, null, 0, GlobalSoundBackendStatus.Muted, ChoiceMask: mask)
        {
            Playback = new(0, 43, false, true, true, null, null)
        };
        output.Execute(request);
        await requestedSecond.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(0, attached);
        short[] frame = new short[2];
        mixer.Render(frame);
        Assert.Equal(new short[2], frame);
        delayed.SetResult(second);
        await PumpUntil(output, () => attached == 1);
        mixer.Render(frame);
        Assert.InRange(frame[0], (short)1, (short)9600);
        Assert.Equal(frame[0], frame[1]);
        Assert.Equal(43, output.PrimaryMusicId);
        output.StopAll();
        Assert.True(first.Disposed);
        Assert.True(second.Disposed);
    }

    [Fact]
    public async Task CancelledPreparationClosesTheLateStreamWithoutCreatingADevice()
    {
        NativeMusicCatalog native = new(TestResources.GameDirectory);
        ModCatalogSnapshot catalog = Catalog([ExternalMusicResolver.TrackName(native.MusicFileResolved(3))]);
        TaskCompletionSource<Stream> decoded = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TrackingStream stream = new(Pcm(1000));
        using FieldAudioPresentation output = new(TestResources.GameDirectory, "", () => catalog)
        {
            PsfStreamOverride = (_, _) =>
            {
                started.SetResult();
                return decoded.Task;
            },
            OutputFactory = () => throw new InvalidOperationException("Cancelled preparation must not create an output.")
        };
        output.PlayMusic(3, 0, 127, -1, identityResolved: true);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        output.StopAll();
        decoded.SetResult(stream);
        await PumpUntil(output, () => stream.Disposed);
        Assert.Equal(-1, output.PrimaryMusicId);
    }

    private static ModCatalogSnapshot Catalog(string[] names)
    {
        ModPackage package = new("example", "Example", ModFormat.Junction, true, true, ModEffectiveStatus.Active, "example")
        {
            JunctionMetadata = new() { UseExternalMusic = true, ExternalMusicExtension = "minipsf" }
        };
        ModFile[] files = names.Select(name => new ModFile(package.Id, ModFormat.Junction, $"music/{name}.minipsf", 1, () => [1])).ToArray();
        return new(1, [package], files);
    }

    private static byte[] Pcm(short sample)
    {
        byte[] bytes = new byte[44100 * 4 * 2];
        for (int index = 0; index < bytes.Length; index += 2)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index), sample);
        }
        return bytes;
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

    private sealed class TrackingStream(byte[] bytes) : MemoryStream(bytes, false)
    {
        public bool Disposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
