using Memoria.FF8.Assets;

namespace Memoria.FF8.Assets.Tests;

public sealed class GlobalAudioFormatTests
{
    [Fact]
    public void StreamsVariableAdpcmMetadataThroughSelectedDescriptorOnly()
    {
        using MemoryStream format = Format(2, Entry(2, 4, [4, 0, 1, 0, 0, 1, 0, 0]), Entry(1, 6, []));
        long selectedEnd = format.Length;
        format.Position = selectedEnd;
        format.WriteByte(0xEE);
        format.Position = 0;
        GlobalAudioSampleDescriptor sample = GlobalAudioFormat.Read(format, 100, 1);
        Assert.Equal(48, sample.DescriptorOffset);
        Assert.Equal(1, sample.SampleIndex);
        Assert.Equal(6u, sample.CompressedLength);
        Assert.Equal(10u, sample.CompressedOffset);
        Assert.Equal(selectedEnd, format.Position);
        Assert.True(format.CanRead);
        Assert.Empty(sample.WaveFormat.ExtraData);
        Assert.Null(sample.WaveFormat.SamplesPerBlock);
    }

    [Fact]
    public void UsesInclusiveLastSampleIndexAndNativePcmExtraStride()
    {
        using MemoryStream format = Format(1, Entry(1, 4, [], declaredExtraSize: 77), Entry(1, 8, [], playbackFlags: 0x12345601));
        GlobalAudioSampleDescriptor sample = GlobalAudioFormat.Read(format, 18, 1);
        Assert.Equal(40, sample.DescriptorOffset);
        Assert.Equal(8u, sample.CompressedLength);
        Assert.Equal(0x12345601u, sample.PlaybackFlags);
        format.Position = 0;
        Assert.Throws<ArgumentOutOfRangeException>(() => GlobalAudioFormat.Read(format, 100, 2));
    }

    [Fact]
    public void RejectsTruncatedHeaderEntryAndExtraWithoutReadingDataPayload()
    {
        foreach (int length in new[] { 1, 2, 39, 43 })
        {
            using MemoryStream original = Format(0, Entry(2, 4, [4, 0, 1, 0, 0, 1, 0, 0]));
            using MemoryStream truncated = new(original.ToArray()[..length]);
            Assert.Throws<InvalidDataException>(() => GlobalAudioFormat.Read(truncated, 100, 0));
        }
        using MemoryStream priorTruncated = Format(1, Entry(2, 4, [4, 0, 1, 0, 0, 1, 0, 0]));
        Assert.Throws<InvalidDataException>(() => GlobalAudioFormat.Read(priorTruncated, 100, 1));
    }

    [Fact]
    public void RejectsOutOfFileUnsignedRangeAndMalformedLoopOrAdpcm()
    {
        using MemoryStream outside = Format(0, Entry(1, uint.MaxValue, [], compressedOffset: uint.MaxValue));
        Assert.Throws<InvalidDataException>(() => GlobalAudioFormat.Read(outside, uint.MaxValue, 0));
        using MemoryStream loop = Format(0, Entry(1, 4, [], loopStart: 8, loopEnd: 8));
        Assert.Throws<InvalidDataException>(() => GlobalAudioFormat.Read(loop, 100, 0));
        using MemoryStream extra = Format(0, Entry(2, 4, [4, 0, 2, 0]));
        Assert.Throws<InvalidDataException>(() => GlobalAudioFormat.Read(extra, 100, 0));
        using MemoryStream emptyExtra = Format(0, Entry(2, 4, []));
        Assert.Throws<InvalidDataException>(() => GlobalAudioFormat.Read(emptyExtra, 100, 0));
    }

    [Fact]
    public void SelectedOriginalSample69MatchesDirectEffectLoopMetadata()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? @"C:\Steam\steamapps\common\FINAL FANTASY VIII";
        Assert.SkipUnless(File.Exists(Path.Combine(directory, "Data", "Sound", "audio.fmt")), "Installed selected global audio corpus is required.");
        GlobalAudioSampleDescriptor sample = GlobalAudioFormat.Read(directory, 69);
        Assert.Equal(69, sample.SampleIndex);
        Assert.Equal(4800, sample.DescriptorOffset);
        Assert.Equal(2587648u, sample.CompressedOffset);
        Assert.Equal(33280u, sample.CompressedLength);
        Assert.Equal(1u, sample.PlaybackFlags);
        Assert.Equal(47844u, sample.LoopStartPcmByte);
        Assert.Equal(130622u, sample.LoopEndPcmByte);
        Assert.Equal(2, sample.WaveFormat.Tag);
        Assert.Equal(1, sample.WaveFormat.Channels);
        Assert.Equal(22050u, sample.WaveFormat.SampleRate);
        Assert.Equal(11155u, sample.WaveFormat.AverageBytesPerSecond);
        Assert.Equal(512, sample.WaveFormat.BlockAlign);
        Assert.Equal(4, sample.WaveFormat.BitsPerSample);
        Assert.Equal(32, sample.WaveFormat.DeclaredExtraSize);
        Assert.Equal(32, sample.WaveFormat.ExtraData.Count);
        Assert.Equal(1012, sample.WaveFormat.SamplesPerBlock);
        Assert.Equal(7, sample.WaveFormat.ExtraData[2]);
    }

    private static MemoryStream Format(ushort lastIndex, params byte[][] entries)
    {
        MemoryStream stream = new();
        using BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, true);
        writer.Write(lastIndex);
        foreach (byte[] entry in entries)
        {
            writer.Write(entry);
        }
        stream.Position = 0;
        return stream;
    }

    private static byte[] Entry(ushort tag, uint compressedLength, byte[] extra, uint compressedOffset = 10, uint loopStart = 0, uint loopEnd = 0, ushort? declaredExtraSize = null, uint playbackFlags = 1)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        writer.Write(compressedLength);
        writer.Write(compressedOffset);
        writer.Write(playbackFlags);
        writer.Write(loopStart);
        writer.Write(loopEnd);
        writer.Write(tag);
        writer.Write((ushort)1);
        writer.Write(22050u);
        writer.Write(11155u);
        writer.Write((ushort)512);
        writer.Write((ushort)4);
        writer.Write(declaredExtraSize ?? checked((ushort)extra.Length));
        writer.Write(extra);
        return stream.ToArray();
    }
}
