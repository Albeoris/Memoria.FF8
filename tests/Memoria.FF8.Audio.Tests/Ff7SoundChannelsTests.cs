using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF7.Runtime;
using Memoria.FF8.Audio;
using Memoria.FF7.Resources;

namespace Memoria.FF8;

public sealed class Ff7SoundChannelsTests
{
    [Fact]
    public void ResidentReplacementAndZeroStopPreserveMusic()
    {
        PcmMixer mixer = new();
        using PcmAudioOutput output = new(mixer);
        using PcmAudioOutput.Buffer music = output.Create(Tone(1000), 44100, 1);
        music.Play(true);
        using Ff7SoundChannels sounds = new();
        sounds.Play(output, Tone(600), 44100, 1, false, 0, 0);
        AssertFrame(mixer, 1600);
        sounds.Play(output, Tone(300), 44100, 1, false, 0, 0);
        AssertFrame(mixer, 1300);
        sounds.StopAll();
        AssertFrame(mixer, 1000);
        Assert.True(music.IsPlaying);
        sounds.Pause(false);
        AssertFrame(mixer, 1000);
    }

    [Fact]
    public void StreamOwnerOverlapsResidentUntilNextSingleSoundOrZeroStop()
    {
        PcmMixer mixer = new();
        using PcmAudioOutput output = new(mixer);
        using Ff7SoundChannels sounds = new();
        sounds.Play(output, Tone(600), 44100, 1, false, 0, 0);
        sounds.Play(output, Tone(300), 44100, 1, true, 0, 0);
        AssertFrame(mixer, 900);
        sounds.Play(output, Tone(200), 44100, 1, true, 0, 0);
        AssertFrame(mixer, 800);
        sounds.Play(output, Tone(100), 44100, 1, false, 0, 0);
        AssertFrame(mixer, 100);
        sounds.Play(output, Tone(50), 44100, 1, true, 0, 0);
        sounds.Pause(true);
        sounds.RetireFinished(true);
        AssertFrame(mixer, 0);
        sounds.Pause(false);
        AssertFrame(mixer, 150);
        sounds.StopAll();
        AssertFrame(mixer, 0);
    }

    [Fact]
    public void WholeResidentLoopRepeatsAndSameActiveSampleDoesNotRestart()
    {
        PcmMixer mixer = new();
        using PcmAudioOutput output = new(mixer);
        using Ff7SoundChannels sounds = new();
        byte[] pcm = Sequence(100, 200, 300);
        sounds.Play(output, pcm, 44100, 1, false, 0, 0, 9, true, 0, pcm.Length);
        AssertFrame(mixer, 100);
        AssertFrame(mixer, 200);
        AssertFrame(mixer, 300);
        AssertFrame(mixer, 100);
        sounds.Play(output, pcm, 44100, 1, false, 0, -10000, 9, true, 0, pcm.Length);
        AssertFrame(mixer, 200);
        sounds.StopAll();
        AssertFrame(mixer, 0);
    }

    [Fact]
    public void IntroHandsOffAtLoopStartAndReplacementStopsBothBuffers()
    {
        PcmMixer mixer = new();
        using PcmAudioOutput output = new(mixer);
        using Ff7SoundChannels sounds = new();
        byte[] pcm = Sequence(100, 200, 300, 400, 500, 600);
        sounds.Play(output, pcm, 44100, 1, false, 0, 0, 10, true, 4, 10);
        AssertFrame(mixer, 100);
        AssertFrame(mixer, 200);
        sounds.RetireFinished(false, 2 / 44100d);
        AssertFrame(mixer, 300);
        AssertFrame(mixer, 400);
        AssertFrame(mixer, 500);
        AssertFrame(mixer, 300);
        sounds.Pause(true);
        AssertFrame(mixer, 0);
        sounds.Pause(false);
        AssertFrame(mixer, 400);
        sounds.Play(output, Tone(77), 44100, 1, false, 0, 0);
        AssertFrame(mixer, 77);
        sounds.StopAll();
        sounds.Pause(false);
        AssertFrame(mixer, 0);
        Assert.Throws<InvalidDataException>(() => sounds.Play(output, pcm, 44100, 1, false, 0, 0, 10, true, 3, 10));
    }

    [Fact]
    public void InstalledReachedLoop236UsesOriginalPcmIntroAndEveryLoopSample()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(directory), "Set MEMORIA_FF7_GAME_DIR to check the reached resident loop.");
        Ff7SoundSample sample = Ff7SoundResources.Read(Ff7Installation.Discover(directory!), 235);
        Assert.True(sample.Loop);
        Assert.Equal(85180u, sample.LoopStartPcmByte);
        Assert.Equal(185372u, sample.LoopEndPcmByte);
        DecodedWave wave = WaveFileDecoder.Read(sample.WaveBytes);
        Assert.Equal(1, wave.Channels);
        Assert.Equal(44100, wave.SampleRate);
        PcmMixer mixer = new();
        using PcmAudioOutput output = new(mixer);
        using Ff7SoundChannels sounds = new();
        // The App decoder normalizes this original mono PCM to stereo through the same common mixer.
        PcmMixer normalization = new();
        using PcmMixer.Voice native = normalization.Create(wave.Pcm, wave.SampleRate, wave.Channels);
        native.Play(false);
        short[] stereo = new short[wave.Pcm.Length];
        normalization.Render(stereo);
        byte[] normalized = new byte[stereo.Length * sizeof(short)];
        for (int index = 0; index < stereo.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(normalized.AsSpan(index * sizeof(short)), stereo[index]);
        }
        (int start, int end) = Ff7SoundChannels.ConvertLoopBounds(sample, 44100, 2);
        Assert.Equal(170360, start);
        Assert.Equal(370744, end);
        sounds.Play(output, normalized, 44100, 2, false, 0, 0, 236, true, start, end);
        int introFrames = (int)sample.LoopStartPcmByte / sizeof(short);
        short[] intro = new short[introFrames * 2];
        mixer.Render(intro);
        for (int frame = 0; frame < introFrames; frame++)
        {
            short expected = BinaryPrimitives.ReadInt16LittleEndian(wave.Pcm.AsSpan(frame * 2));
            Assert.Equal(expected, intro[frame * 2]);
            Assert.Equal(expected, intro[frame * 2 + 1]);
        }
        sounds.RetireFinished(false, introFrames / (double)wave.SampleRate);
        int loopFrames = (int)(sample.LoopEndPcmByte - sample.LoopStartPcmByte) / sizeof(short);
        short[] repeat = new short[(loopFrames + 17) * 2];
        mixer.Render(repeat);
        for (int frame = 0; frame < repeat.Length / 2; frame++)
        {
            int offset = (int)sample.LoopStartPcmByte + frame % loopFrames * sizeof(short);
            short expected = BinaryPrimitives.ReadInt16LittleEndian(wave.Pcm.AsSpan(offset));
            Assert.Equal(expected, repeat[frame * 2]);
            Assert.Equal(expected, repeat[frame * 2 + 1]);
        }
        Assert.Contains(repeat, value => value != 0);
        sounds.StopAll();
        AssertFrame(mixer, 0);
    }

    [Fact]
    public void QueueAdjustedOneShotCursorColdRestoresWithoutReplayingItsIntro()
    {
        string fingerprint = new('A', 64);
        byte[] pcm = Sequence(100, 200, 300, 400, 500, 600, 700, 800);
        PcmMixer mixer = new();
        using PcmAudioOutput output = new(mixer);
        using Ff7SoundChannels sounds = new();
        sounds.Play(output, pcm, 44100, 1, false, 0, 0, 7, fingerprint: fingerprint);
        mixer.Render(new short[8]);
        Ff7SoundVoicePosition saved = Assert.Single(sounds.Capture(2));
        Assert.Equal(2, saved.PositionFrames);
        Ff7AudioPresentationState state = new() { Sounds = [saved] };
        state = JsonSerializer.Deserialize<Ff7AudioPresentationState>(JsonSerializer.Serialize(state))!;
        state.Validate();
        PcmMixer cold = new();
        using PcmAudioOutput coldOutput = new(cold);
        using Ff7SoundChannels restored = new();
        Assert.True(restored.RestoreVoice(coldOutput, pcm, 44100, 1, 7, false, false, 0, 0, fingerprint, state.Sounds[0]));
        AssertFrame(cold, 300);
        AssertFrame(cold, 400);
        Assert.False(restored.RestoreVoice(coldOutput, pcm, 44100, 1, 7, false, false, 0, 0, new string('B', 64), state.Sounds[0]));
        AssertFrame(cold, 500);
    }

    [Fact]
    public void ResidentLoopAndStreamColdRestoreBothOwnersAfterInactiveWorldTime()
    {
        string residentHash = new('C', 64);
        string streamHash = new('D', 64);
        byte[] resident = Sequence(100, 200, 300, 400, 500, 600);
        byte[] stream = Sequence(10, 20, 30, 40, 50, 60);
        PcmMixer mixer = new();
        using PcmAudioOutput output = new(mixer);
        using Ff7SoundChannels sounds = new();
        sounds.Play(output, resident, 44100, 1, false, 0, 0, 236, true, 4, 10, residentHash);
        mixer.Render(new short[4]);
        sounds.RetireFinished(false, 2 / 44100d);
        mixer.Render(new short[8]);
        sounds.Play(output, stream, 44100, 1, true, 0, 0, 99, fingerprint: streamHash);
        AssertFrame(mixer, 410);
        Ff7AudioPresentationState saved = new() { Sounds = sounds.Capture(0) };
        Assert.Equal(2, saved.Sounds.Length);
        Assert.True(saved.Sounds.Single(voice => voice.Channel == 1).LoopStarted);
        string json = JsonSerializer.Serialize(saved);
        saved = JsonSerializer.Deserialize<Ff7AudioPresentationState>(json)!;
        saved.Validate();
        sounds.StopAll();
        mixer.Render(new short[1000]);
        Assert.Equal(json, JsonSerializer.Serialize(saved));
        PcmMixer cold = new();
        using PcmAudioOutput coldOutput = new(cold);
        using Ff7SoundChannels restored = new();
        foreach (Ff7SoundVoicePosition voice in saved.Sounds.OrderByDescending(voice => voice.Channel))
        {
            Assert.True(voice.Channel == 1
                ? restored.RestoreVoice(coldOutput, resident, 44100, 1, 236, false, true, 4, 10, residentHash, voice)
                : restored.RestoreVoice(coldOutput, stream, 44100, 1, 99, true, false, 0, 0, streamHash, voice));
        }
        AssertFrame(cold, 520);
        AssertFrame(cold, 330);
        restored.StopAll();
        Assert.Empty(restored.Capture(0));
        AssertFrame(cold, 0);
    }

    [Fact]
    public void IntroCursorRestoresRemainingHandoffTimeAndMusicUsesAudibleQueueCursor()
    {
        string fingerprint = new('E', 64);
        byte[] pcm = Sequence(100, 200, 300, 400, 500, 600);
        PcmMixer mixer = new();
        using PcmAudioOutput output = new(mixer);
        using Ff7SoundChannels sounds = new();
        sounds.Play(output, pcm, 44100, 1, false, 0, 0, 236, true, 4, 10, fingerprint);
        AssertFrame(mixer, 100);
        Ff7SoundVoicePosition saved = Assert.Single(sounds.Capture(0));
        Assert.False(saved.LoopStarted);
        PcmMixer cold = new();
        using PcmAudioOutput coldOutput = new(cold);
        using Ff7SoundChannels restored = new();
        Assert.True(restored.RestoreVoice(coldOutput, pcm, 44100, 1, 236, false, true, 4, 10, fingerprint, saved));
        AssertFrame(cold, 200);
        restored.RetireFinished(false, 1 / 44100d);
        AssertFrame(cold, 300);
        using PcmAudioOutput.Buffer primary = output.Create(pcm.AsSpan(0, 10).ToArray(), 44100, 1);
        using PcmAudioOutput.Buffer repeat = output.Create(pcm.AsSpan(2, 8).ToArray(), 44100, 1);
        using PcmLoopPlayback music = new(primary, repeat, 100, 5 / 44100d, 88200, 8, blockAlignment: 2);
        sounds.StopAll();
        primary.Play(false);
        mixer.Render(new short[10]);
        music.UpdateAt(5 / 44100d);
        mixer.Render(new short[6]);
        var position = music.CapturePosition(2, 2);
        Assert.True(position.Repeating);
        Assert.Equal(4, position.PositionBytes);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void NativeZeroStopsOnlySelectedResidentAndGlobalStream(int selected)
    {
        PcmMixer mixer = new();
        using PcmAudioOutput output = new(mixer);
        using PcmAudioOutput.Buffer music = output.Create(Tone(1000), 44100, 1);
        music.Play(true);
        using Ff7SoundChannels sounds = new();
        sounds.Play(output, Tone(600), 44100, 1, false, 0, 0, 1, true, 0, 64, channel: 1);
        sounds.Play(output, Tone(300), 44100, 1, false, 0, 0, 2, true, 0, 64, channel: selected);
        sounds.Play(output, Tone(50), 44100, 1, true, 0, 0, 3);
        AssertFrame(mixer, 1950);
        sounds.StopNativeChannel(selected);
        AssertFrame(mixer, 1600);
        sounds.StopNativeChannel(1);
        AssertFrame(mixer, 1000);
    }

    [Fact]
    public void ExplicitChannelTwoLoopKeepsTheOriginalResidentOwner()
    {
        PcmMixer mixer = new();
        using PcmAudioOutput output = new(mixer);
        using Ff7SoundChannels sounds = new();
        sounds.Play(output, Tone(600), 44100, 1, false, 0, 0, 61, true, 0, 64);
        sounds.Play(output, Tone(300), 44100, 1, false, 0, 0, 60, true, 0, 64, channel: 2, stopStream: false);
        AssertFrame(mixer, 900);
        sounds.Play(output, Tone(200), 44100, 1, false, 0, 0, 59, true, 0, 64, channel: 2, stopStream: false);
        AssertFrame(mixer, 800);
        sounds.StopNativeChannel(2);
        AssertFrame(mixer, 600);
    }

    [Fact]
    public void ChannelTwoGainChangesBothIntroAndRepeatWithoutChangingMusicOrOtherOwners()
    {
        PcmMixer mixer = new();
        using PcmAudioOutput output = new(mixer);
        using PcmAudioOutput.Buffer music = output.Create(Tone(1000), 44100, 1);
        music.Play(true);
        using Ff7SoundChannels sounds = new();
        sounds.Play(output, Tone(600), 44100, 1, false, 0, 0, 61, true, 0, 64);
        sounds.Play(output, Tone(300), 44100, 1, false, 0, 0, 60, true, 8, 64, channel: 2, stopStream: false);
        AssertFrame(mixer, 1900);
        sounds.SetGain(2, -10000);
        AssertFrame(mixer, 1600);
        sounds.RetireFinished(false, 1);
        AssertFrame(mixer, 1600);
        sounds.SetGain(2, 0);
        AssertFrame(mixer, 1900);
    }

    [Fact]
    public void LiveUiMuteAndRestoreKeepNativeResidentAndStreamIntensities()
    {
        PcmMixer mixer = new();
        using PcmAudioOutput output = new(mixer);
        using Ff7SoundChannels sounds = new();
        string hash = new('0', 64);
        sounds.Play(output, Tone(600), 44100, 1, false, 0, 0, 10, true, 0, 64, hash);
        sounds.Play(output, Tone(200), 44100, 1, true, 0, 0, 12, fingerprint: hash, nativeVolume: 80);
        sounds.Play(output, Tone(300), 44100, 1, false, 0, 0, 11, true, 0, 64, hash, channel: 2, stopStream: false, nativeVolume: 64);
        int[] native = [127, 64, 127, 127, 127];
        sounds.RefreshGains(native, _ => -10000);
        AssertFrame(mixer, 0);
        Ff7SoundVoicePosition[] muted = sounds.Capture(0);
        Assert.Equal(80, muted.Single(voice => voice.Channel == 0).NativeVolume);
        Assert.Equal(64, muted.Single(voice => voice.Channel == 2).NativeVolume);
        native[1] = 0;
        sounds.RefreshGains(native, volume => volume == 0 ? -10000 : 0);
        AssertFrame(mixer, 800);
        Ff7SoundVoicePosition[] saved = JsonSerializer.Deserialize<Ff7SoundVoicePosition[]>(JsonSerializer.Serialize(sounds.Capture(0)))!;
        string legacy = JsonSerializer.Serialize(saved.Single(voice => voice.Channel == 0)).Replace(",\"NativeVolume\":80", "", StringComparison.Ordinal);
        Assert.Equal(127, JsonSerializer.Deserialize<Ff7SoundVoicePosition>(legacy)!.NativeVolume);
        sounds.StopAll();
        foreach (Ff7SoundVoicePosition voice in saved)
        {
            short sample = voice.Channel == 0 ? (short)200 : voice.Channel == 1 ? (short)600 : (short)300;
            Assert.True(sounds.RestoreVoice(output, Tone(sample), 44100, 1, voice.SoundId, voice.Channel == 0, voice.Loop, 0, voice.Loop ? 64 : 0, hash, voice));
        }
        sounds.RefreshGains(native, volume => volume == 0 ? -10000 : 0);
        AssertFrame(mixer, 800);
        Assert.Equal(80, sounds.Capture(0).Single(voice => voice.Channel == 0).NativeVolume);
    }

    [Fact]
    public void ChannelOneGainAlsoControlsTheGlobalStreamWithoutChangingChannelTwoOrMusic()
    {
        PcmMixer mixer = new();
        using PcmAudioOutput output = new(mixer);
        using PcmAudioOutput.Buffer music = output.Create(Tone(1000), 44100, 1);
        music.Play(true);
        using Ff7SoundChannels sounds = new();
        sounds.Play(output, Tone(300), 44100, 1, false, 0, 0, 1, true, 0, 64);
        sounds.Play(output, Tone(200), 44100, 1, true, 0, 0, 2);
        sounds.Play(output, Tone(500), 44100, 1, false, 0, 0, 3, true, 0, 64, channel: 2, stopStream: false);
        AssertFrame(mixer, 2000);
        sounds.SetGain(1, -10000, 0);
        AssertFrame(mixer, 1500);
        sounds.SetGain(1, 0, 127);
        AssertFrame(mixer, 2000);
    }

    private static byte[] Sequence(params short[] samples)
    {
        byte[] bytes = new byte[samples.Length * sizeof(short)];
        for (int index = 0; index < samples.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * sizeof(short)), samples[index]);
        }
        return bytes;
    }

    private static byte[] Tone(short sample)
    {
        byte[] bytes = new byte[64];
        for (int offset = 0; offset < bytes.Length; offset += sizeof(short))
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), sample);
        }
        return bytes;
    }

    private static void AssertFrame(PcmMixer mixer, short expected)
    {
        short[] frame = new short[2];
        mixer.Render(frame);
        Assert.Equal(expected, frame[0]);
        Assert.Equal(expected, frame[1]);
    }
}
