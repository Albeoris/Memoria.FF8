using System.Buffers.Binary;
using Memoria.FF8.Audio;

namespace Memoria.FF8.Tests;

public sealed class WaveFileDecoderTests
{
    [Fact]
    public void PcmWavePreservesNativeSamplesAndFormat()
    {
        byte[] format = new byte[16];
        BinaryPrimitives.WriteUInt16LittleEndian(format, 1);
        BinaryPrimitives.WriteUInt16LittleEndian(format.AsSpan(2), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(format.AsSpan(4), 44100);
        BinaryPrimitives.WriteUInt32LittleEndian(format.AsSpan(8), 176400);
        BinaryPrimitives.WriteUInt16LittleEndian(format.AsSpan(12), 4);
        BinaryPrimitives.WriteUInt16LittleEndian(format.AsSpan(14), 16);
        byte[] samples = [0, 1, 0, 2, 0, 3, 0, 4];
        DecodedWave wave = WaveFileDecoder.Read(AudioFixtures.Form("RIFF", "WAVE", AudioFixtures.Chunk("fmt ", format), AudioFixtures.Chunk("data", samples)));
        Assert.Equal(samples, wave.Pcm);
        Assert.Equal(44100, wave.SampleRate);
        Assert.Equal(2, wave.Channels);
    }

    [Fact]
    public void AdpcmFactTrimsBlockPaddingToTheDeclaredSampleCount()
    {
        byte[] format = new byte[26];
        BinaryPrimitives.WriteUInt16LittleEndian(format, 2);
        BinaryPrimitives.WriteUInt16LittleEndian(format.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(format.AsSpan(4), 22050);
        BinaryPrimitives.WriteUInt16LittleEndian(format.AsSpan(12), 8);
        BinaryPrimitives.WriteUInt16LittleEndian(format.AsSpan(14), 4);
        BinaryPrimitives.WriteUInt16LittleEndian(format.AsSpan(16), 8);
        new byte[] { 4, 0, 1, 0, 0, 1, 0, 0 }.CopyTo(format, 18);
        byte[] block = [0, 16, 0, 100, 0, 50, 0, 0x1f];
        DecodedWave wave = WaveFileDecoder.Read(AudioFixtures.Form("RIFF", "WAVE", AudioFixtures.Chunk("fmt ", format), AudioFixtures.Chunk("fact", [3, 0, 0, 0]), AudioFixtures.Chunk("data", block)));
        Assert.Equal(6, wave.Pcm.Length);
        Assert.Equal(116, BinaryPrimitives.ReadInt16LittleEndian(wave.Pcm.AsSpan(4)));
    }
}
