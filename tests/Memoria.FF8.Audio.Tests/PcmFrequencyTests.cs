using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF7.Runtime;
using Memoria.FF8.Audio;

namespace Memoria.FF8;

public sealed class PcmFrequencyTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void SelectedFrequencyPublicationDoesNotAlterTheGlobalStreamOrOtherResident(int channel)
    {
        byte[] pcm = Sequence(1000, 2000, 3000, 4000, 5000, 6000);
        string fingerprint = new('E', 64);
        PcmMixer mixer = new();
        using PcmAudioOutput output = new(mixer);
        using Ff7SoundChannels sounds = new();
        sounds.Play(output, pcm, 44100, 1, true, 0, 0, 59, fingerprint: fingerprint);
        sounds.Play(output, pcm, 44100, 1, false, 0, 0, 60, fingerprint: fingerprint, channel: channel, stopStream: false);
        sounds.Play(output, pcm, 44100, 1, false, 0, 0, 61, fingerprint: fingerprint, stopStream: false);
        int[] pitches = new int[5];
        long[] versions = new long[5];
        pitches[channel - 1] = -48;
        versions[channel - 1] = 1;
        sounds.RefreshFrequencies(pitches, [44100, 44100, 44100, 44100, 44100], versions);
        Ff7SoundVoicePosition[] voices = sounds.Capture(0);
        Assert.Equal(35832, voices.Single(voice => voice.Channel == channel).PrimaryFrequency);
        Assert.Equal(1L, voices.Single(voice => voice.Channel == channel).NativeFrequencyVersion);
        Assert.Equal(44100, voices.Single(voice => voice.Channel == 0).PrimaryFrequency);
        Assert.Equal(44100, voices.Single(voice => voice.Channel == 1).PrimaryFrequency);
    }

    [Fact]
    public void NativeFrequencyPublicationsUseCurrentBaseForPrimaryAndStreamWhileRepeatAndColdRatesRemainIndependent()
    {
        byte[] pcm = Sequence(1000, 2000, 3000, 4000, 5000, 6000);
        string fingerprint = new('D', 64);
        double time = 0;
        PcmMixer mixer = new();
        using PcmAudioOutput output = new(mixer);
        using Ff7SoundChannels sounds = new(() => time);
        sounds.Play(output, pcm, 44100, 1, true, 0, 0, 59, fingerprint: fingerprint, nativeSampleRate: 44100);
        sounds.Play(output, pcm, 44100, 1, false, 0, 0, 60, true, 4, pcm.Length, fingerprint, stopStream: false, nativeSampleRate: 22050, nativeLoopDelaySeconds: 1);
        int[] pitches = [-48, 0, 0, 0, 0];
        int[] bases = [44100, 44100, 44100, 44100, 44100];
        long[] versions = [1, 0, 0, 0, 0];
        sounds.RefreshFrequencies(pitches, bases, versions);
        Ff7SoundVoicePosition[] saved = sounds.Capture(0);
        Assert.Equal(35832, saved.Single(voice => voice.Channel == 0).PrimaryFrequency);
        Assert.Equal(35832, saved.Single(voice => voice.Channel == 1).PrimaryFrequency);
        Assert.Equal(22050, saved.Single(voice => voice.Channel == 1).SecondaryFrequency);
        bases[0] = 22050;
        sounds.RefreshFrequencies(pitches, bases, versions);
        Assert.All(sounds.Capture(0), voice => Assert.Equal(35832, voice.PrimaryFrequency));
        versions[0] = 2;
        sounds.RefreshFrequencies(pitches, bases, versions);
        Assert.All(sounds.Capture(0), voice => Assert.Equal(17916, voice.PrimaryFrequency));
        saved = sounds.Capture(0);
        PcmMixer coldMixer = new();
        using PcmAudioOutput coldOutput = new(coldMixer);
        using Ff7SoundChannels cold = new(() => time);
        foreach (Ff7SoundVoicePosition voice in JsonSerializer.Deserialize<Ff7SoundVoicePosition[]>(JsonSerializer.Serialize(saved))!)
        {
            Assert.True(cold.RestoreVoice(coldOutput, pcm, 44100, 1, voice.SoundId, voice.Channel == 0, voice.Loop, voice.Loop ? 4 : 0, voice.Loop ? pcm.Length : 0, fingerprint, voice, voice.NativeSampleRate));
        }
        foreach (Ff7SoundChannels current in new[] { sounds, cold })
        {
            current.RefreshFrequencies(pitches, bases, versions);
            Assert.All(current.Capture(0), voice => Assert.Equal(17916, voice.PrimaryFrequency));
            bases[0] = 200000;
            versions[0] = 3;
            current.RefreshFrequencies(pitches, bases, versions);
            Assert.All(current.Capture(0), voice => Assert.Equal(17916, voice.PrimaryFrequency));
            Assert.All(current.Capture(0), voice => Assert.Equal(3L, voice.NativeFrequencyVersion));
            bases[0] = 44100;
            current.RefreshFrequencies(pitches, bases, versions);
            Assert.All(current.Capture(0), voice => Assert.Equal(17916, voice.PrimaryFrequency));
            versions[0] = 4;
            current.RefreshFrequencies(pitches, bases, versions);
            Assert.All(current.Capture(0), voice => Assert.Equal(35832, voice.PrimaryFrequency));
            Assert.Equal(22050, current.Capture(0).Single(voice => voice.Channel == 1).SecondaryFrequency);
            versions[0] = 2;
            bases[0] = 22050;
        }
        Assert.Equal(JsonSerializer.Serialize(sounds.Capture(0)), JsonSerializer.Serialize(cold.Capture(0)));
    }

    [Fact]
    public void CommonFrequencyChangesPitchWithoutResettingTheOriginalCursor()
    {
        PcmMixer mixer = new();
        using PcmMixer.Voice voice = mixer.Create(Sequence(1000, 2000, 3000, 4000, 5000, 6000, 7000, 8000), 44100, 1);
        voice.Play(false);
        Frame(mixer, 1000);
        Frame(mixer, 2000);
        voice.FrequencyRatio(0.5);
        Frame(mixer, 3000);
        Frame(mixer, 3500);
        voice.FrequencyRatio(2);
        Frame(mixer, 4000);
        Frame(mixer, 6000);
        Assert.Throws<ArgumentOutOfRangeException>(() => voice.FrequencyRatio(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => voice.FrequencyRatio(double.NaN));
    }

    [Fact]
    public void NativePrimaryPitchDoesNotChangeRepeatRateOrWallHandoffAndColdRemainingTime()
    {
        double time = 0;
        byte[] pcm = Sequence(1000, 2000, 3000, 4000, 5000, 6000);
        string fingerprint = new('A', 64);
        PcmMixer mixer = new();
        using PcmAudioOutput output = new(mixer);
        using Ff7SoundChannels sounds = new(() => time);
        sounds.Play(output, pcm, 44100, 1, false, 0, 0, 60, true, 4, pcm.Length, fingerprint, nativeSampleRate: 44100, nativeLoopDelaySeconds: 1);
        sounds.SetNativeFrequency(1, 22050);
        Frame(mixer, 1000);
        Frame(mixer, 1500);
        Frame(mixer, 2000);
        Frame(mixer, 2500);
        time = 0.5;
        sounds.RetireFinished(false);
        Ff7SoundVoicePosition saved = Assert.Single(sounds.Capture(0));
        Assert.Equal(22050, saved.PrimaryFrequency);
        Assert.Equal(44100, saved.SecondaryFrequency);
        Assert.Equal(0.5, saved.LoopHandoffRemainingSeconds);
        Assert.False(saved.LoopStarted);
        Ff7AudioPresentationState state = JsonSerializer.Deserialize<Ff7AudioPresentationState>(JsonSerializer.Serialize(new Ff7AudioPresentationState { Sounds = [saved] }))!;
        state.Validate();
        double coldTime = 10;
        PcmMixer coldMixer = new();
        using PcmAudioOutput coldOutput = new(coldMixer);
        using Ff7SoundChannels cold = new(() => coldTime);
        Assert.True(cold.RestoreVoice(coldOutput, pcm, 44100, 1, 60, false, true, 4, pcm.Length, fingerprint, state.Sounds[0], 44100, 1));
        Frame(coldMixer, 3000);
        coldTime = 10.49;
        cold.RetireFinished(false);
        Assert.False(Assert.Single(cold.Capture(0)).LoopStarted);
        coldTime = 10.5;
        cold.RetireFinished(false);
        Frame(coldMixer, 3000);
        Frame(coldMixer, 4000);
        time = 1;
        sounds.RetireFinished(false);
        Frame(mixer, 3000);
        Frame(mixer, 4000);
    }

    [Fact]
    public void FastPrimaryExhaustionPreservesTheNativeGapAcrossColdRestore()
    {
        double time = 0;
        byte[] pcm = Sequence(1000, 2000, 3000, 4000);
        string fingerprint = new('B', 64);
        PcmMixer mixer = new();
        using PcmAudioOutput output = new(mixer);
        using Ff7SoundChannels sounds = new(() => time);
        sounds.Play(output, pcm, 44100, 1, false, 0, 0, 60, true, 4, pcm.Length, fingerprint, nativeLoopDelaySeconds: 1);
        sounds.SetNativeFrequency(1, 88200);
        Frame(mixer, 1000);
        Frame(mixer, 3000);
        Frame(mixer, 0);
        time = 0.5;
        Ff7SoundVoicePosition saved = Assert.Single(sounds.Capture(0));
        Assert.Equal(4, saved.PositionFrames);
        Assert.Equal(0.5, saved.LoopHandoffRemainingSeconds);
        new Ff7AudioPresentationState { Sounds = [saved] }.Validate();
        double restoredTime = 5;
        PcmMixer restoredMixer = new();
        using PcmAudioOutput restoredOutput = new(restoredMixer);
        using Ff7SoundChannels restored = new(() => restoredTime);
        Assert.True(restored.RestoreVoice(restoredOutput, pcm, 44100, 1, 60, false, true, 4, pcm.Length, fingerprint, saved));
        Frame(restoredMixer, 0);
        restoredTime = 5.5;
        restored.RetireFinished(false);
        Frame(restoredMixer, 3000);
        Frame(restoredMixer, 4000);
    }

    [Fact]
    public void AbsoluteNativeFrequencyUsesEachOriginalRateAndRejectedRequestsKeepPriorRates()
    {
        PcmMixer mixer = new();
        using PcmAudioOutput output = new(mixer);
        using Ff7SoundChannels sounds = new();
        byte[] pcm = Sequence(100, 200, 300, 400, 500, 600);
        string fingerprint = new('C', 64);
        sounds.Play(output, pcm, 44100, 1, true, 0, 0, 59, fingerprint: fingerprint, nativeSampleRate: 44100);
        sounds.Play(output, pcm, 44100, 1, false, 0, 0, 60, fingerprint: fingerprint, stopStream: false, nativeSampleRate: 22050);
        sounds.SetNativeFrequency(1, 17916);
        Ff7SoundVoicePosition[] positions = sounds.Capture(0);
        Assert.All(positions, position => Assert.Equal(17916, position.PrimaryFrequency));
        Assert.Equal(44100, positions.Single(position => position.Channel == 0).NativeSampleRate);
        Assert.Equal(22050, positions.Single(position => position.Channel == 1).NativeSampleRate);
        sounds.SetNativeFrequency(1, 99);
        sounds.SetNativeFrequency(1, 100001);
        Assert.All(sounds.Capture(0), position => Assert.Equal(17916, position.PrimaryFrequency));
        Assert.Throws<InvalidDataException>(() => new Ff7AudioPresentationState { Sounds = [positions[0] with { PrimaryFrequency = 99 }] }.Validate());
    }

    private static byte[] Sequence(params short[] samples)
    {
        byte[] result = new byte[samples.Length * 2];
        for (int index = 0; index < samples.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(result.AsSpan(index * 2), samples[index]);
        }
        return result;
    }

    private static void Frame(PcmMixer mixer, short expected)
    {
        short[] stereo = new short[2];
        mixer.Render(stereo);
        Assert.Equal(expected, stereo[0]);
        Assert.Equal(expected, stereo[1]);
    }
}
