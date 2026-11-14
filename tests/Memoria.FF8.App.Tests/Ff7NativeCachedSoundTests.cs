using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF7.Runtime;
using Memoria.FF8.Audio;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7NativeCachedSoundTests
{
    private const string Fingerprint = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    [Fact]
    public void Distinct_cached_samples_overlap_and_replaying_the_same_id_restarts_only_that_buffer()
    {
        PcmMixer mixer = new();
        using PcmAudioOutput output = new(mixer);
        using Ff7SoundChannels channels = new();
        byte[] first = Pcm(1000);
        byte[] second = Pcm(2000);
        channels.PlayCached(output, first, 44100, 1, 0, 0, 371, fingerprint: Fingerprint);
        channels.PlayCached(output, second, 44100, 1, 0, 0, 124, fingerprint: Fingerprint);
        short[] rendered = new short[8];
        mixer.Render(rendered);
        Assert.All(rendered, value => Assert.Equal((short)3000, value));
        Assert.Equal(new[] { 124, 371 }, channels.Capture(0).Where(position => position.Cached).Select(position => position.SoundId));
        channels.PlayCached(output, first, 44100, 1, 0, 0, 371, fingerprint: Fingerprint);
        Assert.Equal(0, channels.Capture(0).Single(position => position.SoundId == 371).PositionFrames);
        Assert.Equal(4, channels.Capture(0).Single(position => position.SoundId == 124).PositionFrames);
    }

    [Fact]
    public void Cache_release_receipts_dispose_only_direct_buffers_and_preserve_resident_duplicates()
    {
        PcmMixer mixer = new();
        using PcmAudioOutput output = new(mixer);
        using Ff7SoundChannels channels = new();
        channels.PlayCached(output, Pcm(1000), 44100, 1, 0, 0, 371, fingerprint: Fingerprint);
        channels.PlayCached(output, Pcm(2000), 44100, 1, 0, 0, 124, fingerprint: Fingerprint);
        channels.Play(output, Pcm(3000), 44100, 1, false, 0, 0, 250, fingerprint: Fingerprint, channel: 1);
        channels.Play(output, Pcm(4000), 44100, 1, false, 0, 0, 371, fingerprint: Fingerprint, channel: 2);
        Assert.True(Ff7Scene.ReleaseCachedBattleSound(new(1, 371, 64) { ReleaseQueueBank = 15 }, channels));
        Assert.True(Ff7Scene.ReleaseCachedBattleSound(new(2, 124, 64) { ReleaseQueueBank = 15 }, channels));
        Assert.True(Ff7Scene.ReleaseCachedBattleSound(new(3, 250, 64) { ReleaseQueueBank = 15 }, channels));
        short[] rendered = new short[2];
        mixer.Render(rendered);
        Assert.All(rendered, value => Assert.Equal((short)7000, value));
        Assert.All(channels.Capture(0), position => Assert.False(position.Cached));
        Assert.Equal(new[] { 250, 371 }, channels.Capture(0).Select(position => position.SoundId));
        Assert.False(Ff7Scene.ReleaseCachedBattleSound(new(4, 250, 64), channels));
    }

    [Fact]
    public void Cached_and_legacy_channel_positions_keep_distinct_owners_and_exact_pcm_after_json_restore()
    {
        PcmMixer mixer = new();
        using PcmAudioOutput output = new(mixer);
        using Ff7SoundChannels channels = new();
        Dictionary<int, byte[]> samples = new() { [371] = Pcm(1000), [124] = Pcm(2000), [250] = Pcm(3000) };
        channels.Play(output, samples[250], 44100, 1, false, 0, 0, 250, fingerprint: Fingerprint);
        foreach (int id in new[] { 371, 124 })
        {
            channels.PlayCached(output, samples[id], 44100, 1, 0, 0, id, fingerprint: Fingerprint);
        }
        mixer.Render(new short[14]);
        Ff7AudioPresentationState captured = new() { Sounds = channels.Capture(0) };
        captured.Validate();
        Ff7AudioPresentationState saved = JsonSerializer.Deserialize<Ff7AudioPresentationState>(JsonSerializer.Serialize(captured))!;
        saved.Validate();
        Assert.Equal(2, saved.Sounds.Count(position => position.Cached && position.Channel == 0));
        Ff7SoundVoicePosition legacy = saved.Sounds.Single(position => !position.Cached);
        Assert.DoesNotContain("\"Cached\"", JsonSerializer.Serialize(legacy));
        new Ff7AudioPresentationState { Sounds = [JsonSerializer.Deserialize<Ff7SoundVoicePosition>(JsonSerializer.Serialize(legacy))!] }.Validate();
        PcmMixer coldMixer = new();
        using PcmAudioOutput coldOutput = new(coldMixer);
        using Ff7SoundChannels cold = new();
        foreach (Ff7SoundVoicePosition position in saved.Sounds)
        {
            Assert.True(cold.RestoreVoice(coldOutput, samples[position.SoundId], 44100, 1, position.SoundId, position.Cached, false, 0, 0, Fingerprint, position));
        }
        Assert.Equal(JsonSerializer.Serialize(captured), JsonSerializer.Serialize(new Ff7AudioPresentationState { Sounds = cold.Capture(0) }));
        short[] expected = new short[16];
        short[] actual = new short[16];
        mixer.Render(expected);
        coldMixer.Render(actual);
        Assert.Equal(expected, actual);
        Assert.Equal(JsonSerializer.Serialize(channels.Capture(0)), JsonSerializer.Serialize(cold.Capture(0)));
    }

    [Fact]
    public void Cached_buffers_share_pause_tempo_preferences_and_disposal_without_becoming_resident_channels()
    {
        PcmMixer mixer = new();
        using PcmAudioOutput output = new(mixer);
        using Ff7SoundChannels channels = new();
        channels.PlayCached(output, Pcm(1000), 44100, 1, 0, 0, 371, fingerprint: Fingerprint);
        channels.StopNativeChannel(1);
        channels.SetGain(1, -10000);
        channels.Pause(true);
        short[] rendered = new short[2];
        mixer.Render(rendered);
        Assert.All(rendered, value => Assert.Equal((short)0, value));
        channels.Pause(false);
        channels.SetTempo(2);
        mixer.Render(rendered);
        Assert.All(rendered, value => Assert.Equal((short)1000, value));
        Assert.Equal(2, Assert.Single(channels.Capture(0)).PositionFrames);
        channels.RefreshGains(_ => -10000);
        mixer.Render(rendered);
        Assert.All(rendered, value => Assert.Equal((short)0, value));
        channels.StopAll();
        Assert.Empty(channels.Capture(0));
    }

    private static byte[] Pcm(short value)
    {
        byte[] pcm = new byte[256];
        for (int offset = 0; offset < pcm.Length; offset += sizeof(short))
        {
            BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(offset), value);
        }
        return pcm;
    }
}
