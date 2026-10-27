using System.Buffers.Binary;
using Memoria.FF8.Audio;
using Memoria.FF8.Events;
using Memoria.FF8.Mods;
using Memoria.FF8.Resources;

namespace Memoria.FF8.App.Tests;

public sealed class PendingAudioDecodeTests
{
    [Fact]
    public async Task ReplacedStreamUsesOnlyTheLatestOwnerAndCurrentGainWhenDecodeFinishes()
    {
        string game = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(game), "An explicitly configured original music catalog is required.");
        var old = new TaskCompletionSource<PcmAudio>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var mixer = new PcmMixer();
        int requests = 0;
        int buffers = 0;
        using FieldAudioPresentation output = new(game, "")
        {
            OutputFactory = () =>
            {
                buffers++;
                return new PcmAudioOutput(mixer);
            },
            DecodeOverride = (_, _, token) =>
            {
                if (Interlocked.Increment(ref requests) == 1)
                {
                    started.SetResult(token);
                    return old.Task;
                }
                return Task.FromResult(Pcm(2000));
            }
        };
        output.PlayMusic(3, 0, 127, -1);
        CancellationToken original = await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        output.PlayMusic(6, 0, 127, -1);
        Assert.True(original.IsCancellationRequested);
        output.SetMusicGain(0, null, 64, 0);
        output.SetOutputPreferences(0, 100);
        output.SetFocusPaused(true);
        old.SetResult(Pcm(1000));
        await PumpUntil(output, () => buffers == 1);
        Assert.Equal(6, output.PrimaryMusicId);
        Assert.Equal(64, output.PrimaryMusicVolume);
        short[] block = new short[2];
        mixer.Render(block);
        Assert.Equal(new short[2], block);
        output.SetFocusPaused(false);
        mixer.Render(block);
        Assert.Equal(new short[2], block);
        output.SetOutputPreferences(100, 100);
        mixer.Render(block);
        Assert.InRange(block[0], (short)1, (short)1999);
        Assert.Equal(block[0], block[1]);
        output.StopAll();
        mixer.Render(block);
        Assert.Equal(new short[2], block);
        Assert.Equal(-1, output.PrimaryMusicId);
    }

    [Fact]
    public async Task StoppedPendingStreamCannotBeResurrectedDuringShutdown()
    {
        string game = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(game), "An explicitly configured original music catalog is required.");
        var result = new TaskCompletionSource<PcmAudio>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        int buffers = 0;
        using FieldAudioPresentation output = new(game, "")
        {
            OutputFactory = () =>
            {
                buffers++;
                return new PcmAudioOutput(new PcmMixer());
            },
            DecodeOverride = (_, _, token) =>
            {
                started.SetResult(token);
                return result.Task;
            }
        };
        output.PlayMusic(3, 0, 127, -1);
        CancellationToken token = await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        output.StopAll();
        Assert.True(token.IsCancellationRequested);
        result.SetResult(Pcm(1000));
        output.Dispose();
        Assert.Equal(0, buffers);
        Assert.Equal(-1, output.PrimaryMusicId);
    }

    [Fact]
    public async Task DelayedVoiceAppliesCurrentMuteAndPauseBeforeItCanPlay()
    {
        var decoded = new TaskCompletionSource<PcmAudio>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var mixer = new PcmMixer();
        using FieldAudioPresentation output = new("", "")
        {
            OutputFactory = () => new PcmAudioOutput(mixer),
            DecodeOverride = (_, _, _) =>
            {
                started.SetResult();
                return decoded.Task;
            }
        };
        output.SetFocusPaused(true);
        Assert.True(output.PlayDialogueVoice(0, Resource(), 1));
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.False(output.IsDialogueVoicePlaying(0));
        output.SetOutputPreferences(100, 100, 0);
        decoded.SetResult(Pcm(1000));
        await PumpUntil(output, () => output.DialogueVoiceCount == 1);
        Assert.False(output.IsDialogueVoicePlaying(0));
        short[] block = new short[2];
        mixer.Render(block);
        Assert.Equal(new short[2], block);
        output.SetVoicePresentationPaused(true);
        output.SetFocusPaused(false);
        Assert.False(output.IsDialogueVoicePlaying(0));
        output.SetVoicePresentationPaused(false);
        Assert.True(output.IsDialogueVoicePlaying(0));
        mixer.Render(block);
        Assert.Equal(new short[2], block);
        output.SetOutputPreferences(100, 100, 100);
        mixer.Render(block);
        Assert.Equal(new short[] { 1000, 1000 }, block);
    }

    [Fact]
    public async Task VoiceReplacementAndStopInvalidateTheOriginalCompletion()
    {
        var old = new TaskCompletionSource<PcmAudio>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var mixer = new PcmMixer();
        int request = 0;
        using FieldAudioPresentation output = new("", "")
        {
            OutputFactory = () => new PcmAudioOutput(mixer),
            DecodeOverride = (_, _, token) =>
            {
                if (Interlocked.Increment(ref request) == 1)
                {
                    started.SetResult(token);
                    return old.Task;
                }
                return Task.FromResult(Pcm(2000));
            }
        };
        output.PlayDialogueVoice(0, Resource(), 1);
        CancellationToken original = await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        output.PlayDialogueVoice(0, Resource(), 1);
        Assert.True(original.IsCancellationRequested);
        old.SetResult(Pcm(1000));
        await PumpUntil(output, () => output.DialogueVoiceCount == 1);
        short[] block = new short[2];
        mixer.Render(block);
        Assert.Equal(new short[] { 2000, 2000 }, block);
        output.StopDialogueVoice(0);
        output.Update();
        mixer.Render(block);
        Assert.Equal(new short[2], block);
        Assert.Equal(0, output.DialogueVoiceCount);
    }

    [Fact]
    public async Task PendingVoiceCannotAutoAdvanceBeforePlaybackOrAfterDecodeFailure()
    {
        var source = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        List<Exception> failures = [];
        using var queue = new AudioDecodeQueue(failures.Add);
        bool active = false;
        FieldVoicePresentation? presentation = null;
        presentation = new FieldVoicePresentation((_, _, _, _, _) => new ExternalVoiceSelection(new ResourceKey("voice/test.ogg"), "synthetic", "voice/test.ogg", 1), (channel, _, _) =>
        {
            queue.Queue(channel, _ => source.Task, _ => active = true, exception =>
            {
                failures.Add(exception);
                presentation!.PlaybackFailed(channel);
            });
            return true;
        }, channel =>
        {
            queue.Cancel(channel);
            active = false;
        }, channel => queue.IsPending(channel) || active);
        DialogueState dialogue = new(0, 3, "Text", 0, 0, NativeWindow: new(1, 512, 0));
        object owner = new();
        presentation.Synchronize("scene", owner, [new(dialogue, 0)]);
        presentation.Synchronize("scene", owner, [new(dialogue, 0)]);
        Assert.False(presentation.CanAutoAdvance(0));
        source.SetResult(1);
        await WaitUntil(() => queue.ReadyCount == 1);
        presentation.Synchronize("scene", owner, [new(dialogue, 0)]);
        Assert.False(presentation.CanAutoAdvance(0));
        queue.Pump();
        presentation.Synchronize("scene", owner, [new(dialogue, 0)]);
        Assert.False(presentation.CanAutoAdvance(0));
        active = false;
        presentation.Synchronize("scene", owner, [new(dialogue, 0)]);
        Assert.True(presentation.CanAutoAdvance(0));
        source = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        presentation.Synchronize("scene", owner, [new(dialogue, 1)]);
        source.SetException(new InvalidDataException("synthetic decoder failure"));
        await WaitUntil(() => queue.ReadyCount == 1);
        queue.Pump();
        presentation.Synchronize("scene", owner, [new(dialogue, 1)]);
        Assert.Single(failures);
        Assert.False(presentation.CanAutoAdvance(0));
    }

    [Fact]
    public void ManagedSpuWaveNormalizationPreservesSamplesAndDuration()
    {
        byte[] format = new byte[16];
        BinaryPrimitives.WriteUInt16LittleEndian(format, 1);
        BinaryPrimitives.WriteUInt16LittleEndian(format.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(format.AsSpan(4), 22050);
        BinaryPrimitives.WriteUInt32LittleEndian(format.AsSpan(8), 44100);
        BinaryPrimitives.WriteUInt16LittleEndian(format.AsSpan(12), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(format.AsSpan(14), 16);
        using var bytes = new MemoryStream();
        using (var writer = new BinaryWriter(bytes, System.Text.Encoding.UTF8, true))
        {
            writer.Write("RIFF"u8);
            writer.Write(40);
            writer.Write("WAVEfmt "u8);
            writer.Write(16);
            writer.Write(format);
            writer.Write("data"u8);
            writer.Write(4);
            writer.Write((short)100);
            writer.Write((short)200);
        }
        PcmAudio pcm = PcmAudioDecoder.ReadWave(bytes.ToArray());
        Assert.Equal(44100, pcm.SampleRate);
        Assert.Equal(2, pcm.Channels);
        Assert.Equal(16, pcm.Bytes.Length);
        Assert.Equal(new short[] { 100, 100, 150, 150, 200, 200, 200, 200 }, Enumerable.Range(0, 8).Select(i => BinaryPrimitives.ReadInt16LittleEndian(pcm.Bytes.AsSpan(i * 2))));
    }

    private static ResourceContent Resource()
    {
        return new ResourceContent(new ResourceKey("voice/test.ogg"), "audio", 1, "synthetic", () => new MemoryStream([1]));
    }

    private static PcmAudio Pcm(short sample)
    {
        byte[] bytes = new byte[44100 * 4];
        for (int i = 0; i < bytes.Length; i += 2)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(i), sample);
        }
        return new PcmAudio(bytes, 44100, 2);
    }

    private static async Task PumpUntil(FieldAudioPresentation output, Func<bool> condition)
    {
        await WaitUntil(() =>
        {
            output.Update();
            return condition();
        });
        Assert.Null(output.Error);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(5, deadline.Token);
        }
    }
}
