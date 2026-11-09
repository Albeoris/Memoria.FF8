using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Audio;

namespace Memoria.FF8;

public sealed class Ff7GainPublicationTests
{
    [Fact]
    public void ExistingStreamFollowsNativeChannelOneRampWhileNewResidentUiAndColdKeepTheirOwners()
    {
        Ff7FieldResources field = new("gain-publication", null!, null!, null!, [], [], null!, null!, null!);
        Ff7FieldHost host = new(field, (_, _) => 1);
        host.ConfigureNativeSoundResources(_ => new(44100, false));
        byte[] pcm = new byte[44100 * 8 * sizeof(short)];
        for (int offset = 0; offset < pcm.Length; offset += 2)
        {
            BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(offset), 1000);
        }
        string fingerprint = new('A', 64);
        PcmMixer mixer = new();
        using PcmAudioOutput output = new(mixer);
        using Ff7SoundChannels sounds = new();
        host.NativeSoundClockAdvanced = (elapsed, paused) =>
        {
            if (!paused)
            {
                mixer.Render(new short[checked((int)(elapsed.TotalSeconds * 44100)) * 2]);
            }
        };
        sounds.Play(output, pcm, 44100, 1, true, 0, 0, 59, fingerprint: fingerprint);
        host.Execute(new(1, 0, 7, 16, new FieldAudioCommand(0xA4, 255, 0)));
        host.AdvanceAudioClock(TimeSpan.FromSeconds(1));
        sounds.RefreshNativeGains(host.Process.SoundChannelGainPublications, host.Process.SoundChannelGainVersions, Gain);
        Assert.Equal(127, Assert.Single(sounds.Capture(0)).NativeVolume);
        host.AdvanceAudioClock(TimeSpan.FromSeconds(1));
        sounds.RefreshNativeGains(host.Process.SoundChannelGainPublications, host.Process.SoundChannelGainVersions, Gain);
        Assert.Equal(98, Assert.Single(sounds.Capture(0)).NativeVolume);
        host.AdvanceAudioClock(TimeSpan.FromSeconds(4));
        Assert.True(host.Process.SoundChannelStoppedByFade[0]);
        sounds.StopResidentChannel(1);
        sounds.RefreshNativeGains(host.Process.SoundChannelGainPublications, host.Process.SoundChannelGainVersions, Gain);
        Assert.Equal(0, Assert.Single(sounds.Capture(0)).NativeVolume);
        Assert.Equal(-10000, Assert.Single(sounds.Capture(0)).Gain);
        host.Execute(new(2, 0, 7, 16, new FieldAudioCommand(0xA1, 64)));
        sounds.Play(output, pcm, 44100, 1, false, 0, Gain(64), 60, true, 0, pcm.Length, fingerprint, channel: 2, stopStream: false, nativeVolume: 64, nativeGainVersion: host.Process.SoundChannelGainVersions[1]);
        sounds.RefreshGains(_ => -10000);
        Assert.All(sounds.Capture(0), voice => Assert.Equal(-10000, voice.Gain));
        sounds.RefreshGains(Gain);
        Ff7SoundVoicePosition[] saved = sounds.Capture(0);
        Assert.Equal(0, saved.Single(voice => voice.Channel == 0).NativeVolume);
        Assert.Equal(64, saved.Single(voice => voice.Channel == 2).NativeVolume);
        PcmMixer coldMixer = new();
        using PcmAudioOutput coldOutput = new(coldMixer);
        using Ff7SoundChannels cold = new();
        foreach (Ff7SoundVoicePosition voice in JsonSerializer.Deserialize<Ff7SoundVoicePosition[]>(JsonSerializer.Serialize(saved))!)
        {
            Assert.True(cold.RestoreVoice(coldOutput, pcm, 44100, 1, voice.SoundId, voice.Channel == 0, voice.Loop, 0, voice.Loop ? pcm.Length : 0, fingerprint, voice));
        }
        Ff7FieldProcess process = JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!;
        cold.RefreshNativeGains(process.SoundChannelGainPublications, process.SoundChannelGainVersions, Gain);
        cold.RefreshGains(Gain);
        Assert.Equal(JsonSerializer.Serialize(saved), JsonSerializer.Serialize(cold.Capture(0)));
        short[] frame = new short[2];
        coldMixer.Render(frame);
        Assert.Equal(new short[] { 1000, 1000 }, frame);
    }

    private static int Gain(int volume) => volume == 0 ? -10000 : 0;
}
