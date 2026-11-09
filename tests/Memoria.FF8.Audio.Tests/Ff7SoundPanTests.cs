using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF7.Runtime;
using Memoria.FF8.Audio;

namespace Memoria.FF8;

public sealed class Ff7SoundPanTests
{
    [Fact]
    public void NativePanPublicationsPreserveAuthoredStreamUntilAnActualWriterAndSurviveColdRestore()
    {
        byte[] pcm = Pcm();
        string fingerprint = new('A', 64);
        int[] pans = [64, 64, 64, 64, 64];
        long[] versions = new long[5];
        int[] table = Table();
        PcmMixer mixer = new();
        using PcmAudioOutput output = new(mixer);
        using Ff7SoundChannels sounds = new();
        sounds.Play(output, pcm, 44100, 2, true, 6000, 0, 59, fingerprint: fingerprint);
        sounds.RefreshPans(pans, versions, table);
        Assert.Equal(6000, Assert.Single(sounds.Capture(0)).Pan);
        versions[0] = 1;
        sounds.RefreshPans(pans, versions, table);
        Assert.Equal(0, Assert.Single(sounds.Capture(0)).Pan);
        sounds.Play(output, pcm, 44100, 2, true, -6000, 0, 59, fingerprint: fingerprint, nativePanVersion: 1);
        sounds.RefreshPans(pans, versions, table);
        Ff7SoundVoicePosition saved = Assert.Single(sounds.Capture(0));
        Assert.Equal(-6000, saved.Pan);
        saved = JsonSerializer.Deserialize<Ff7SoundVoicePosition>(JsonSerializer.Serialize(saved))!;
        PcmMixer coldMixer = new();
        using PcmAudioOutput coldOutput = new(coldMixer);
        using Ff7SoundChannels cold = new();
        Assert.True(cold.RestoreVoice(coldOutput, pcm, 44100, 2, 59, true, false, 0, 0, fingerprint, saved));
        cold.RefreshPans(pans, versions, table);
        Assert.Equal(-6000, Assert.Single(cold.Capture(0)).Pan);
        versions[0] = 2;
        foreach (Ff7SoundChannels current in new[] { sounds, cold })
        {
            current.RefreshPans(pans, versions, table);
            Assert.Equal(0, Assert.Single(current.Capture(0)).Pan);
            Assert.Equal(2L, Assert.Single(current.Capture(0)).NativePanVersion);
        }
    }

    [Fact]
    public void NativePrimaryPanLeavesRepeatCenteredIncludingHandoffAndColdState()
    {
        double time = 0;
        byte[] pcm = Pcm();
        string fingerprint = new('B', 64);
        PcmMixer mixer = new();
        using PcmAudioOutput output = new(mixer);
        using Ff7SoundChannels sounds = new(() => time);
        sounds.Play(output, pcm, 44100, 2, false, 5000, 0, 60, true, 8, pcm.Length, fingerprint, nativeLoopDelaySeconds: 1);
        int[] pans = [100, 64, 64, 64, 64];
        long[] versions = [1, 0, 0, 0, 0];
        sounds.RefreshPans(pans, versions, Table());
        Ff7SoundVoicePosition saved = Assert.Single(sounds.Capture(0));
        Assert.Equal(3600, saved.Pan);
        Assert.Equal(0, saved.SecondaryPan);
        double coldTime = 0;
        PcmMixer coldMixer = new();
        using PcmAudioOutput coldOutput = new(coldMixer);
        using Ff7SoundChannels cold = new(() => coldTime);
        Assert.True(cold.RestoreVoice(coldOutput, pcm, 44100, 2, 60, false, true, 8, pcm.Length, fingerprint, saved));
        time = coldTime = 1;
        sounds.RetireFinished(false);
        cold.RetireFinished(false);
        short[] hotFrame = new short[2];
        short[] coldFrame = new short[2];
        mixer.Render(hotFrame);
        coldMixer.Render(coldFrame);
        Assert.Equal(new short[] { 1000, 1000 }, hotFrame);
        Assert.Equal(hotFrame, coldFrame);
        Assert.Equal(0, Assert.Single(cold.Capture(0)).SecondaryPan);
    }

    [Fact]
    public void PanCallbackPublishesToSelectedPrimaryAndGlobalStreamWithoutTouchingAnotherResident()
    {
        byte[] pcm = Pcm();
        string fingerprint = new('C', 64);
        PcmMixer mixer = new();
        using PcmAudioOutput output = new(mixer);
        using Ff7SoundChannels sounds = new();
        sounds.Play(output, pcm, 44100, 2, true, 2000, 0, 59, fingerprint: fingerprint);
        sounds.Play(output, pcm, 44100, 2, false, -3000, 0, 60, fingerprint: fingerprint, channel: 2, stopStream: false);
        sounds.Play(output, pcm, 44100, 2, false, -4000, 0, 61, fingerprint: fingerprint, stopStream: false);
        sounds.RefreshPans([104, 64, 64, 64, 64], [1, 0, 0, 0, 0], Table());
        Ff7SoundVoicePosition[] captured = sounds.Capture(0);
        Assert.Equal(4000, captured.Single(voice => voice.Channel == 0).Pan);
        Assert.Equal(4000, captured.Single(voice => voice.Channel == 1).Pan);
        Assert.Equal(-3000, captured.Single(voice => voice.Channel == 2).Pan);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void SelectedPanPublicationLeavesGlobalStreamAndOtherPrimaryUnchanged(int channel)
    {
        byte[] pcm = Pcm();
        string fingerprint = new('D', 64);
        PcmMixer mixer = new();
        using PcmAudioOutput output = new(mixer);
        using Ff7SoundChannels sounds = new();
        sounds.Play(output, pcm, 44100, 2, true, 2000, 0, 59, fingerprint: fingerprint);
        sounds.Play(output, pcm, 44100, 2, false, -3000, 0, 60, fingerprint: fingerprint, channel: channel, stopStream: false);
        sounds.Play(output, pcm, 44100, 2, false, -4000, 0, 61, fingerprint: fingerprint, stopStream: false);
        int[] pans = [64, 64, 64, 64, 64];
        long[] versions = new long[5];
        pans[channel - 1] = 63;
        versions[channel - 1] = 1;
        sounds.RefreshPans(pans, versions, Table());
        Ff7SoundVoicePosition[] voices = sounds.Capture(0);
        Assert.Equal(-100, voices.Single(voice => voice.Channel == channel).Pan);
        Assert.Equal(2000, voices.Single(voice => voice.Channel == 0).Pan);
        Assert.Equal(-4000, voices.Single(voice => voice.Channel == 1).Pan);
    }

    private static int[] Table() => Enumerable.Range(0, 128).Select(index => (index - 64) * 100).ToArray();

    private static byte[] Pcm()
    {
        byte[] pcm = new byte[128];
        for (int offset = 0; offset < pcm.Length; offset += 2)
        {
            BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(offset), 1000);
        }
        return pcm;
    }
}
