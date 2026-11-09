using System.Buffers.Binary;
using Memoria.FF8.Audio;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class PcmConcertSourceTests
{
    [Fact]
    public void Mixer_renders_independent_stereo_stems_on_one_clock_with_distinct_intros_and_loops()
    {
        using PcmConcertSource source = new(Parts());
        PcmMixer mixer = new();
        mixer.Add(source);
        short[] output = new short[16];

        mixer.Render(output);

        Assert.Equal(new short[] { 210, 800, 320, 1700, 430, 2600, 540, 3500, 320, 1700, 430, 2600, 540, 3500, 320, 1700 }, output);
        Assert.Equal(new long[] { 3, 2 }, source.CapturePositions(0).Select(position => position.Position));
    }

    [Fact]
    public void Pause_freezes_both_cursors_and_preserves_queued_position_history()
    {
        using PcmConcertSource source = new(Parts());
        Render(source, 2);
        GameplaySaveAudioPosition[] beforePause = source.CapturePositions(0);

        source.Pause(true);
        Assert.All(Render(source, 5), frame => Assert.Equal((0d, 0d), frame));

        Assert.Equal(beforePause, source.CapturePositions(0));
        Assert.Equal(beforePause, source.CapturePositions(5));
        Assert.Equal(new long[] { 1, 0 }, source.CapturePositions(7).Select(position => position.Position));
        source.Pause(false);
        AssertFrame(source, 430, 2600);
        Assert.Equal(new long[] { 4, 3 }, source.CapturePositions(0).Select(position => position.Position));
    }

    [Fact]
    public void Per_part_percentage_and_native_gain_multiply_without_cross_channel_scaling()
    {
        PcmAudio first = Audio((1000, -2000));
        PcmAudio second = Audio((4000, 8000));
        using PcmConcertSource source = new([new(first, 0, 4, 0, "first", 50), new(second, 0, 4, 0, "second", 25)]);

        AssertFrame(source, 1500, 1000);
        source.SetGain(-2000);
        AssertFrame(source, 150, 100);
        source.SetGain(-10000);
        AssertFrame(source, 0, 0);
        source.SetGain(0);
        AssertFrame(source, 1500, 1000);
    }

    [Fact]
    public void Muting_keeps_stems_in_time_and_resume_uses_the_next_frame()
    {
        using PcmConcertSource source = new(Parts());
        AssertFrame(source, 210, 800);

        source.SetGain(-10000);
        Assert.All(Render(source, 2), frame => Assert.Equal((0d, 0d), frame));
        Assert.Equal(new long[] { 4, 3 }, source.CapturePositions(0).Select(position => position.Position));

        source.SetGain(0);
        AssertFrame(source, 540, 3500);
    }

    [Fact]
    public void Zero_volume_stem_still_advances_with_other_stems()
    {
        PcmConcertSource.Part[] parts = Parts();
        parts[0] = parts[0] with { Volume = 0 };
        using PcmConcertSource source = new(parts);

        AssertFrame(source, 10, 1000);
        AssertFrame(source, 20, 2000);
        Assert.Equal(new long[] { 3, 2 }, source.CapturePositions(0).Select(position => position.Position));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(9)]
    [InlineData(15)]
    public void Queued_cursor_after_independent_loop_wraps_restores_exact_subsequent_samples(int queuedFrames)
    {
        PcmConcertSource.Part[] parts = Parts();
        using PcmConcertSource source = new(parts);
        (double Left, double Right)[] rendered = Render(source, 11);
        GameplaySaveAudioPosition[] saved = source.CapturePositions(queuedFrames);
        int audibleFrames = Math.Max(0, rendered.Length - queuedFrames);
        PcmConcertSource.Part[] restoredParts = new PcmConcertSource.Part[parts.Length];
        for (int index = 0; index < parts.Length; index++)
        {
            Assert.Equal("pcm16-v1", saved[index].Backend);
            Assert.Equal(parts[index].Fingerprint, saved[index].Fingerprint);
            Assert.Equal("source-frames", saved[index].Unit);
            Assert.Equal(44100, saved[index].SampleRate);
            Assert.Equal(2, saved[index].Channels);
            Assert.Equal(parts[index].LoopStartByte / 4, saved[index].LoopStart);
            Assert.Equal(parts[index].LoopEndByte / 4, saved[index].LoopEnd);
            restoredParts[index] = parts[index] with { StartByte = checked((int)saved[index].Position * 4) };
        }
        using PcmConcertSource restored = new(restoredParts);
        (double Left, double Right)[] expected = rendered[audibleFrames..].Concat(Render(source, 8)).ToArray();

        Assert.Equal(expected, Render(restored, expected.Length));
    }

    [Fact]
    public void Disposed_source_renders_silence_when_still_attached_to_mixer()
    {
        using PcmConcertSource source = new(Parts());
        PcmMixer mixer = new();
        mixer.Add(source);
        AssertFrame(source, 210, 800);

        source.Dispose();
        source.Dispose();
        short[] output = new short[8];
        Array.Fill(output, (short)123);
        mixer.Render(output);

        Assert.All(output, sample => Assert.Equal(0, sample));
    }

    [Theory]
    [InlineData(0, 2, 16, 0, 16, 0)]
    [InlineData(22050, 2, 16, 0, 16, 0)]
    [InlineData(44100, 0, 16, 0, 16, 0)]
    [InlineData(44100, 1, 16, 0, 16, 0)]
    [InlineData(44100, 3, 16, 0, 16, 0)]
    [InlineData(44100, 2, 0, 0, 0, 0)]
    [InlineData(44100, 2, 15, 0, 12, 0)]
    [InlineData(44100, 2, 16, -4, 16, 0)]
    [InlineData(44100, 2, 16, 2, 16, 0)]
    [InlineData(44100, 2, 16, 8, 8, 0)]
    [InlineData(44100, 2, 16, 12, 8, 0)]
    [InlineData(44100, 2, 16, 0, 20, 0)]
    [InlineData(44100, 2, 16, 0, 14, 0)]
    [InlineData(44100, 2, 16, 0, 16, -4)]
    [InlineData(44100, 2, 16, 0, 16, 2)]
    [InlineData(44100, 2, 16, 0, 16, 16)]
    [InlineData(44100, 2, 16, 0, 12, 12)]
    public void Malformed_format_loop_or_start_is_rejected_before_rendering(int rate, int channels, int length, int loopStart, int loopEnd, int start)
    {
        PcmAudio audio = new(new byte[length], rate, channels);

        Assert.Throws<ArgumentException>(() => new PcmConcertSource([new(audio, loopStart, loopEnd, start, "stem")]));
    }

    private static PcmConcertSource.Part[] Parts()
    {
        PcmAudio first = Audio((100, -100), (200, -200), (300, -300), (400, -400), (500, -500), (600, -600));
        PcmAudio second = Audio((10, 1000), (20, 2000), (30, 3000), (40, 4000), (50, 5000));
        return [new(first, 8, 20, 4, "first"), new(second, 4, 16, 0, "second")];
    }

    private static PcmAudio Audio(params (short Left, short Right)[] frames)
    {
        byte[] bytes = new byte[frames.Length * 4];
        for (int index = 0; index < frames.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 4, 2), frames[index].Left);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 4 + 2, 2), frames[index].Right);
        }
        return new(bytes, 44100, 2);
    }

    private static (double Left, double Right)[] Render(PcmConcertSource source, int frames)
    {
        (double Left, double Right)[] result = new (double, double)[frames];
        for (int index = 0; index < result.Length; index++)
        {
            source.RenderFrame(out double left, out double right);
            result[index] = (left, right);
        }
        return result;
    }

    private static void AssertFrame(PcmConcertSource source, double expectedLeft, double expectedRight)
    {
        source.RenderFrame(out double left, out double right);
        Assert.Equal(expectedLeft, left, 8);
        Assert.Equal(expectedRight, right, 8);
    }
}
