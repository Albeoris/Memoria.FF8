using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF7.Runtime;
using Memoria.FF8.Audio;

namespace Memoria.FF8;

public sealed class PcmFrequencyTests
{
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
