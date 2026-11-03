using System.Buffers.Binary;
using Memoria.FF8.Audio;

namespace Memoria.FF8.Tests;

public sealed class PcmTimeStretchTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(37)]
    [InlineData(44101)]
    public void DurationIsRoundedToCompleteFramesIncludingShortClips(int frames)
    {
        byte[] pcm = Signal(frames, 2, _ => 12000);
        byte[] compressed = PcmTimeStretch.Compress(pcm, 44100, 2, 3);
        Assert.Equal(((frames + 1) / 3) * 4, compressed.Length);
        for (int i = 0; i < compressed.Length / 2; i++)
        {
            Assert.Equal(12000, Sample(compressed, i));
        }
        Assert.Equal(pcm, PcmTimeStretch.Compress(pcm, 44100, 2, 1));
    }

    [Theory]
    [InlineData(22050, 220)]
    [InlineData(44100, 440)]
    [InlineData(48000, 880)]
    public void CompressionRetainsTonePitchAndAvoidsSpliceDiscontinuities(int rate, int frequency)
    {
        byte[] pcm = Signal(rate * 2, 1, i => 12000 * Math.Sin(2 * Math.PI * frequency * i / rate));
        byte[] compressed = PcmTimeStretch.Compress(pcm, rate, 1, 3);
        int frames = compressed.Length / 2;
        int crossings = 0;
        int maximumStep = 0;
        for (int i = rate / 20; i < frames - rate / 20; i++)
        {
            short previous = Sample(compressed, i - 1);
            short current = Sample(compressed, i);
            if (previous <= 0 && current > 0)
            {
                crossings++;
            }
            maximumStep = Math.Max(maximumStep, Math.Abs(current - previous));
        }
        double measuredFrequency = crossings * (double)rate / (frames - rate / 10);
        Assert.InRange(measuredFrequency, frequency * 0.98, frequency * 1.02);
        Assert.True(maximumStep < 2500, $"Splice step was {maximumStep}.");
    }

    [Fact]
    public void StereoPhaseAndTransientTimingRemainAligned()
    {
        const int rate = 24000;
        byte[] pcm = Signal(rate * 3, 2, i =>
        {
            double time = (double)i / rate;
            bool burst = time is > 0.9 and < 1.1 or > 1.9 and < 2.1;
            return burst ? 20000 * Math.Sin(2 * Math.PI * 600 * time) : 0;
        }, invertRight: true);
        byte[] compressed = PcmTimeStretch.Compress(pcm, rate, 2, 3);
        int firstBurst = 0;
        int secondBurst = 0;
        for (int frame = 0; frame < compressed.Length / 4; frame++)
        {
            int left = Sample(compressed, frame * 2);
            int right = Sample(compressed, frame * 2 + 1);
            Assert.Equal(-left, right);
            Assert.InRange(left, -20000, 20000);
            if (Math.Abs(left) > 5000)
            {
                double time = (double)frame / rate;
                Assert.True(time is > 0.25 and < 0.42 or > 0.58 and < 0.75);
                if (time < 0.5)
                {
                    firstBurst++;
                }
                else
                {
                    secondBurst++;
                }
            }
        }
        Assert.True(firstBurst > 100);
        Assert.True(secondBurst > 100);
    }

    [Fact]
    public void InvalidFormatsAndSpeedsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => PcmTimeStretch.Compress(new byte[3], 44100, 1, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => PcmTimeStretch.Compress([], 0, 1, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => PcmTimeStretch.Compress([], 44100, 3, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => PcmTimeStretch.Compress([], 44100, 1, 2));
    }

    [Fact]
    public void SwitchingTempoPreservesOriginalTimelineAndPausedSeek()
    {
        var mixer = new PcmMixer(24000);
        using PcmMixer.Voice voice = mixer.Create(Signal(24000, 1, i => 10000 * Math.Sin(i * 0.1)), 24000, 1);
        voice.Play(false);
        mixer.Render(new short[2400 * 2]);
        Assert.Equal(2400 * 2, voice.PositionBytes);
        voice.Tempo(3);
        Assert.Equal(2400 * 2, voice.PositionBytes);
        mixer.Render(new short[800 * 2]);
        Assert.Equal(4800 * 2, voice.PositionBytes);
        voice.Pause();
        voice.Tempo(1);
        voice.Position(6000 * 2);
        voice.Tempo(3);
        mixer.Render(new short[500 * 2]);
        Assert.False(voice.IsPlaying);
        Assert.Equal(6000 * 2, voice.PositionBytes);
        voice.Resume();
        mixer.Render(new short[6000 * 2]);
        Assert.False(voice.IsPlaying);
        Assert.Equal(24000 * 2, voice.PositionBytes);
    }

    [Fact]
    public void CompressedVoiceLoopsAtCompressedDurationAndCanReturnToNormalSpeed()
    {
        var mixer = new PcmMixer(12000);
        using PcmMixer.Voice voice = mixer.Create(Signal(12000, 2, _ => 1000), 12000, 2);
        voice.Tempo(3);
        voice.Play(true);
        short[] rendered = new short[4000 * 2];
        mixer.Render(rendered);
        Assert.All(rendered, sample => Assert.Equal(1000, sample));
        Assert.True(voice.IsPlaying);
        Assert.Equal(0, voice.PositionBytes);
        mixer.Render(new short[100 * 2]);
        Assert.Equal(300 * 4, voice.PositionBytes);
        voice.Tempo(1);
        mixer.Render(new short[100 * 2]);
        Assert.Equal(400 * 4, voice.PositionBytes);
        voice.Stop();
        voice.Position(0);
        voice.Tempo(3);
        voice.Play(false);
        mixer.Render(new short[3999 * 2]);
        Assert.True(voice.IsPlaying);
        mixer.Render(new short[2]);
        Assert.False(voice.IsPlaying);
    }

    private static byte[] Signal(int frames, int channels, Func<int, double> sample, bool invertRight = false)
    {
        byte[] result = new byte[frames * channels * 2];
        for (int frame = 0; frame < frames; frame++)
        {
            short value = (short)Math.Round(sample(frame));
            for (int channel = 0; channel < channels; channel++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(result.AsSpan((frame * channels + channel) * 2), channel == 1 && invertRight ? (short)-value : value);
            }
        }
        return result;
    }

    private static short Sample(byte[] pcm, int sample) => BinaryPrimitives.ReadInt16LittleEndian(pcm.AsSpan(sample * 2));
}
