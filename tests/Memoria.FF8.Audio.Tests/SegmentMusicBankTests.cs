using System.Buffers.Binary;
using System.Security.Cryptography;
using Memoria.FF8.Audio;

namespace Memoria.FF8.Tests;

public sealed class SegmentMusicBankTests
{
    [Fact]
    public async Task FaultySegmentCannotInterruptOtherMixerSources()
    {
        string path = TestResources.ArtifactFile("segment-music-malformed-controller.sgt");
        CancellationToken token = TestContext.Current.CancellationToken;
        await File.WriteAllBytesAsync(path, AudioFixtures.SegmentBytes(controller: 128), token);
        SegmentMusicSource source = await new SegmentMusicBank(AudioFixtures.BankBytes()).PrepareAsync([path], PcmMixer.DefaultSampleRate, 0, token: token);
        PcmMixer mixer = new();
        mixer.Add(source, new PcmMixBus());
        using PcmMixer.Voice effect = mixer.Create([0x34, 0x12], PcmMixer.DefaultSampleRate, 1, new PcmMixBus());
        effect.Play(true);

        short[] samples = new short[4];
        mixer.Render(samples);
        Assert.Equal(new short[] { 0x1234, 0x1234, 0x1234, 0x1234 }, samples);
        Assert.IsType<IndexOutOfRangeException>(source.Error);
        Assert.False(source.IsPlaying);
        Assert.Null(source.CapturePosition());
        Assert.True(effect.IsPlaying);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LegacyFingerprintAndAudibleCursorRestoreTheNextMixedSamples(bool concert)
    {
        byte[] dls = AudioFixtures.BankBytes();
        byte[] first = AudioFixtures.SegmentBytes(repeats: uint.MaxValue, loopEnd: 500);
        byte[] second = AudioFixtures.SegmentBytes(repeats: uint.MaxValue, loopEnd: 600);
        string path = TestResources.ArtifactFile($"segment-music-{concert}-first.sgt");
        string nextPath = TestResources.ArtifactFile($"segment-music-{concert}-second.sgt");
        CancellationToken token = TestContext.Current.CancellationToken;
        await File.WriteAllBytesAsync(path, first, token);
        if (concert)
        {
            await File.WriteAllBytesAsync(nextPath, second, token);
        }
        string[] paths = concert ? [path, nextPath] : [path];
        SegmentMusicBank bank = new(dls);
        SegmentMusicSource live = await bank.PrepareAsync(paths, PcmMixer.DefaultSampleRate, 0, concert, token: token);
        PcmMixer mixer = new();
        mixer.Add(live);
        mixer.Render(new short[2000]);
        var queued = new short[120];
        mixer.Render(queued);
        live.Pause();
        mixer.Render(new short[80]);
        MusicPlaybackPosition position = live.CapturePosition(100)!;

        byte[] count = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(count, paths.Length);
        using IncrementalHash legacy = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        legacy.AppendData("sgt-dls-sample-clock-v1"u8);
        legacy.AppendData(SHA256.HashData(dls));
        legacy.AppendData(count);
        legacy.AppendData(SHA256.HashData(first));
        if (concert)
        {
            legacy.AppendData(SHA256.HashData(second));
        }
        Assert.Equal(Convert.ToHexString(legacy.GetHashAndReset()), position.ContentSha256);
        Assert.Equal(1000, position.Position.Frames);
        Assert.True(position.Position.Paused);

        SegmentMusicSource restored = await bank.PrepareAsync(paths, PcmMixer.DefaultSampleRate, 0, concert, saved: position, token: token);
        mixer.Remove(live);
        mixer.Add(restored);
        Assert.Equal(position, restored.CapturePosition());
        Assert.False(restored.IsPlaying);
        restored.Resume();
        Assert.True(restored.IsPlaying);
        var rendered = new short[queued.Length];
        mixer.Render(rendered);
        Assert.Contains(queued, sample => sample != 0);
        Assert.Equal(queued, rendered);

        string[] changed = concert ? [nextPath, path] : paths;
        MusicPlaybackPosition mismatched = concert ? position : position with { ContentSha256 = new string('0', 64) };
        SegmentMusicSource fallback = await bank.PrepareAsync(changed, PcmMixer.DefaultSampleRate, 0, concert, saved: mismatched, token: token);
        Assert.Equal(0, fallback.CapturePosition()!.Position.Frames);
        Assert.True(fallback.IsPlaying);
    }
}
