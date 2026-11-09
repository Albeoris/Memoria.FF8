using Memoria.FF7.Resources;

namespace Memoria.FF7.Resources.Tests;

public sealed class Ff7SoundResourcesTests
{
    [Fact]
    public void SelectedSampleUsesNativeOffsetAndVariableWaveExtraData()
    {
        using MemoryStream metadata = new();
        using (BinaryWriter writer = new(metadata, System.Text.Encoding.UTF8, true))
        {
            WriteDescriptor(writer, 4, 0, 0, [1, 2]);
            WriteDescriptor(writer, 4, 4, 0, []);
        }
        metadata.Position = 0;
        using MemoryStream data = new([0, 0, 0, 0, 1, 2, 3, 4]);
        Ff7SoundSample sample = Ff7SoundResources.Read(metadata, data, 1);
        Assert.Equal(1, sample.SampleIndex);
        Assert.Equal(4u, sample.CompressedByteLength);
        Assert.Equal(44100, sample.NativeSampleRate);
        Assert.False(sample.Loop);
        Assert.Equal("RIFF"u8.ToArray(), sample.WaveBytes[..4]);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, sample.WaveBytes[^4..]);
    }

    [Fact]
    public void UnusedDescriptorDoesNotConsumeUninitializedWaveExtraSize()
    {
        using MemoryStream metadata = new();
        using (BinaryWriter writer = new(metadata, System.Text.Encoding.UTF8, true))
        {
            writer.Write(new byte[24]);
            writer.Write(Enumerable.Repeat((byte)0xCD, 18).ToArray());
            WriteDescriptor(writer, 4, 0, 0, []);
        }
        metadata.Position = 0;
        using MemoryStream data = new(new byte[] { 1, 2, 3, 4 });
        Ff7SoundSample sample = Ff7SoundResources.Read(metadata, data, 1);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, sample.WaveBytes[^4..]);
        metadata.Position = 0;
        Assert.Throws<InvalidDataException>(() => Ff7SoundResources.Read(metadata, data, 0));
    }

    [Fact]
    public void TruncatedMetadataAndOutOfBoundsDataAreRejected()
    {
        using MemoryStream truncated = new(new byte[41]);
        using MemoryStream data = new(new byte[4]);
        Assert.Throws<InvalidDataException>(() => Ff7SoundResources.Read(truncated, data, 0));
        using MemoryStream metadata = new();
        using (BinaryWriter writer = new(metadata, System.Text.Encoding.UTF8, true))
        {
            WriteDescriptor(writer, 8, 0, 0, []);
        }
        metadata.Position = 0;
        Assert.Throws<InvalidDataException>(() => Ff7SoundResources.Read(metadata, data, 0));
    }

    [Fact]
    public void InstalledOpeningSoundsAndMusicNamesReadWhenConfigured()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Assert.Skip("Set MEMORIA_FF7_GAME_DIR to check original resources.");
        }
        Ff7Installation installation = Ff7Installation.Discover(directory);
        foreach (int index in new[] { 27, 56, 199, 290 })
        {
            Ff7SoundSample sample = Ff7SoundResources.Read(installation, index);
            Assert.False(sample.Loop);
            Assert.True(sample.WaveBytes.Length > 44);
        }
        Ff7PcExecutableData native = Ff7PcExecutableData.Load(installation);
        Assert.Equal(99, native.MusicNames.Count);
        Assert.Equal("NONE", native.MusicNames[0]);
        Assert.Equal("OB", native.MusicNames[2]);
        Assert.Equal(128, native.NativeSoundPan.Count);
        Assert.Equal(-10000, native.NativeSoundPan[0]);
        Assert.Equal(0, native.NativeSoundPan[64]);
        Assert.Equal(10000, native.NativeSoundPan[127]);
        Assert.Equal(256, native.NativeFieldRandomTable.Count);
        Assert.Equal(1, native.FieldRandomStep);
    }

    private static void WriteDescriptor(BinaryWriter writer, uint length, uint offset, uint loop, byte[] extra)
    {
        writer.Write(length);
        writer.Write(offset);
        writer.Write(loop);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write((ushort)(extra.Length == 0 ? 1 : 2));
        writer.Write((ushort)1);
        writer.Write(44100u);
        writer.Write(88200u);
        writer.Write((ushort)2);
        writer.Write((ushort)16);
        writer.Write((ushort)extra.Length);
        writer.Write(extra);
    }
}
