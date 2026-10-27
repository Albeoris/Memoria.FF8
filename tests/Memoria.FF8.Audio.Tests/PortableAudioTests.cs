using System.Buffers.Binary;
using Memoria.FF8.Audio;

namespace Memoria.FF8.Tests;

public sealed class PortableAudioTests
{
    [Fact]
    public void Pcm8ConvertsUnsignedSamples()
    {
        byte[] pcm = WaveDecoder.Decode(1, 1, 8, 1, [], [0, 128, 255]);
        Assert.Equal(new short[] { -32768, 0, 32512 }, Samples(pcm));
    }

    [Fact]
    public void AdpcmUsesDeclaredCoefficientsAndBlockHistory()
    {
        byte[] extra = [4, 0, 1, 0, 0, 1, 0, 0];
        byte[] block = [0, 16, 0, 100, 0, 50, 0, 0x1f];
        byte[] pcm = WaveDecoder.Decode(2, 1, 4, 8, extra, block.Concat(block).ToArray());
        Assert.Equal(new short[] { 50, 100, 116, 100, 50, 100, 116, 100 }, Samples(pcm));
    }

    [Fact]
    public void AdpcmNegativePredictionUsesArithmeticShift()
    {
        byte[] extra = [4, 0, 1, 0, 0xff, 0, 0, 0];
        byte[] block = [0, 16, 0, 0xff, 0xff, 0, 0, 0];
        Assert.Equal(new short[] { 0, -1, -1, -1 }, Samples(WaveDecoder.Decode(2, 1, 4, 8, extra, block)));
    }

    [Fact]
    public void StereoAdpcmInterleavesHeaderAndNibbles()
    {
        byte[] extra = [3, 0, 1, 0, 0, 1, 0, 0];
        byte[] block = [0, 0, 16, 0, 16, 0, 100, 0, 200, 0, 50, 0, 60, 0, 0x1f];
        Assert.Equal(new short[] { 50, 60, 100, 200, 116, 184 }, Samples(WaveDecoder.Decode(2, 2, 4, 15, extra, block)));
    }

    [Fact]
    public void MixerRetainsPauseCursorAndCompletesAtSampleBoundary()
    {
        var mixer = new PcmMixer(4);
        using PcmMixer.Voice voice = mixer.Create(Bytes(100, 200, 300, 400), 4, 1);
        voice.Play(false);
        short[] output = new short[4];
        mixer.Render(output);
        Assert.Equal(new short[] { 100, 100, 200, 200 }, output);
        voice.Pause();
        mixer.Render(output);
        Assert.Equal(new short[4], output);
        Assert.Equal(4, voice.PositionBytes);
        voice.Resume();
        mixer.Render(output);
        Assert.Equal(new short[] { 300, 300, 400, 400 }, output);
        Assert.False(voice.IsPlaying);
    }

    [Fact]
    public void ACompletedBufferCanBeExplicitlyPlayedAgain()
    {
        var mixer = new PcmMixer(1);
        using PcmMixer.Voice voice = mixer.Create(Bytes(1000), 1, 1);
        short[] frame = new short[2];
        voice.Play(false);
        mixer.Render(frame);
        Assert.False(voice.IsPlaying);
        voice.Play(false);
        mixer.Render(frame);
        Assert.Equal(new short[] { 1000, 1000 }, frame);
    }

    [Fact]
    public void MixerResamplesAndLoopsWithChannelGain()
    {
        var mixer = new PcmMixer(4);
        using PcmMixer.Voice voice = mixer.Create(Bytes(1000, 3000), 2, 1);
        voice.Play(true);
        voice.Pan(10000);
        short[] output = new short[10];
        mixer.Render(output);
        Assert.Equal(new short[] { 0, 1000, 0, 2000, 0, 3000, 0, 2000, 0, 1000 }, output);
        Assert.True(voice.IsPlaying);
    }

    [Fact]
    public void OriginalSoftwareSegmentsRenderWithConfiguredBank()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_AUDIO_FIXTURES");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(directory), "Set MEMORIA_FF8_AUDIO_FIXTURES to a legally supplied segment/bank directory.");
        var bank = new DlsBank(File.ReadAllBytes(Path.Combine(directory!, "FF8.dls")));
        string[] paths = Directory.GetFiles(directory!, "*.sgt").Where(p => !Path.GetFileName(p).Contains("xg", StringComparison.OrdinalIgnoreCase)).ToArray();
        Assert.NotEmpty(paths);
        foreach (string path in paths)
        {
            var segment = new MusicSegment(File.ReadAllBytes(path));
            var synth = new SegmentSynthesizer(bank);
            SegmentInstrumentCoverage coverage = synth.InspectInstrumentCoverage(segment);
            Assert.True(coverage.MissingInstruments.Count == 0, Path.GetFileName(path) + ": " + string.Join("; ", coverage.MissingInstruments.Take(12)));
            synth.Play(segment);
            double energy = 0;
            for (int i = 0; i < 44100; i++)
            {
                synth.RenderFrame(out double left, out double right);
                energy += Math.Abs(left) + Math.Abs(right);
            }
            Assert.True(double.IsFinite(energy), Path.GetFileName(path));
        }
    }

    private static short[] Samples(byte[] bytes)
    {
        return Enumerable.Range(0, bytes.Length / 2).Select(i => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(i * 2))).ToArray();
    }

    private static byte[] Bytes(params short[] samples)
    {
        byte[] bytes = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(i * 2), samples[i]);
        }
        return bytes;
    }
}
