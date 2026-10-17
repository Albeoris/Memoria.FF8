using System.Buffers.Binary;
using Memoria.FF8.Assets;
using Memoria.FF8.Assets.Remastered;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Remastered.Tests;

public sealed class RemasteredSoundTests
{
    [Theory]
    [InlineData("audio.fmt")]
    [InlineData("audio.dat")]
    public void SoundContainersUseMountedBytesAndCachedFileResolution(string name)
    {
        using SyntheticResources files = new();
        string directory = Path.GetDirectoryName(files.Write([]))!;
        byte[] mounted = [1, 4, 9, 16];
        File.WriteAllBytes(Path.Combine(directory, "main.zzz"), SyntheticResources.Zzz());
        File.WriteAllBytes(Path.Combine(directory, "other.zzz"), SyntheticResources.Zzz(("data/sound/" + name, mounted)));
        Directory.CreateDirectory(Path.Combine(directory, "sound"));
        File.WriteAllBytes(Path.Combine(directory, "sound", name), [99]);
        string resolved;
        using (RemasteredGameDataSource source = new(directory))
        {
            Assert.Equal(mounted, new ResourceService(source).Read(new ResourceKey("files/sound/" + name)));
            resolved = source.ResolveFile("sound/" + name);
            Assert.Equal(mounted, File.ReadAllBytes(resolved));
            Assert.Equal(resolved, source.ResolveFile("sound/" + name));
        }
        Assert.False(File.Exists(resolved));
    }

    [Theory]
    [InlineData("audio.fmt")]
    [InlineData("audio.dat")]
    public void MissingMountedSoundDoesNotUseLooseClassicFiles(string name)
    {
        using SyntheticResources files = new();
        string directory = Path.GetDirectoryName(files.Write([]))!;
        File.WriteAllBytes(Path.Combine(directory, "main.zzz"), SyntheticResources.Zzz());
        File.WriteAllBytes(Path.Combine(directory, "other.zzz"), SyntheticResources.Zzz());
        Directory.CreateDirectory(Path.Combine(directory, "sound"));
        File.WriteAllBytes(Path.Combine(directory, "sound", name), [99]);
        using RemasteredGameDataSource source = new(directory);
        Assert.Null(source.Find(new ResourceKey("files/sound/" + name)));
        Assert.Throws<FileNotFoundException>(() => source.ResolveFile("sound/" + name));
    }

    [Fact]
    public void InstalledSoundContainersUseTheSharedPcmAndMicrosoftAdpcmDescriptorContract()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_REMASTERED_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory), "Set MEMORIA_FF8_REMASTERED_DIR to run optional installed Remastered checks.");
        using RemasteredGameDataSource source = new(directory!);
        ResourceService resources = new(source);
        using MemoryStream format = new(resources.Read(new ResourceKey("files/sound/audio.fmt")), false);
        using Stream data = resources.Get(new ResourceKey("files/sound/audio.dat")).OpenRead();
        Span<byte> header = stackalloc byte[2];
        format.ReadExactly(header);
        int last = BinaryPrimitives.ReadUInt16LittleEndian(header);
        Assert.True(last > 0);
        int populated = 0;
        for (int index = 0; index <= last; index++)
        {
            format.Position = 0;
            GlobalAudioSampleDescriptor sample = GlobalAudioFormat.Read(format, data.Length, index);
            if (sample.CompressedLength == 0)
            {
                continue;
            }
            Assert.Contains(sample.WaveFormat.Tag, new ushort[] { 1, 2 });
            Assert.Contains(sample.WaveFormat.Channels, new ushort[] { 1, 2 });
            Assert.True(sample.WaveFormat.SampleRate > 0);
            data.Position = sample.CompressedOffset;
            Assert.NotEqual(-1, data.ReadByte());
            populated++;
        }
        Assert.True(populated > 0);
        Assert.Equal(format.Length, format.Position);
    }
}
