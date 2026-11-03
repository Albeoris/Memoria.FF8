using System.Buffers.Binary;
using Memoria.FF8.Assets;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets.Tests;

public sealed class ClassicLooseFileResolutionTests
{
    [Theory]
    [InlineData("Data", "Sound", "audio.fmt", "audio.dat")]
    [InlineData("DATA", "SOUND", "AUDIO.FMT", "AUDIO.DAT")]
    [InlineData("dAtA", "sOuNd", "Audio.Fmt", "Audio.Dat")]
    public void GlobalAudioReadsMixedCaseInstalledContainers(string data, string sound, string formatName, string payloadName)
    {
        using Corpus corpus = new();
        byte[] format = new byte[40];
        BinaryPrimitives.WriteUInt32LittleEndian(format.AsSpan(2), 4);
        BinaryPrimitives.WriteUInt16LittleEndian(format.AsSpan(22), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(format.AsSpan(24), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(format.AsSpan(26), 22050);
        BinaryPrimitives.WriteUInt32LittleEndian(format.AsSpan(30), 44100);
        BinaryPrimitives.WriteUInt16LittleEndian(format.AsSpan(34), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(format.AsSpan(36), 16);
        string formatPath = corpus.Write([data, sound, formatName], format);
        string payloadPath = corpus.Write([data, sound, payloadName], [1, 2, 3, 4]);
        using ClassicGameDataSource source = new(corpus.Root);
        ResourceService resources = new(source);

        GlobalAudioSampleDescriptor sample = GlobalAudioFormat.Read(resources, 0);

        Assert.Equal(4u, sample.CompressedLength);
        Assert.Equal(22050u, sample.WaveFormat.SampleRate);
        Assert.Equal((ushort)1, sample.WaveFormat.Tag);
        Assert.Equal(formatPath, source.ResolveFile("sound/audio.fmt"));
        Assert.Equal(payloadPath, source.ResolveFile("SOUND/AUDIO.DAT"));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, resources.Read(new("files/sound/audio.dat")));
    }

    [Fact]
    public void MusicAndLocalizedMoviesResolveActualNamesAndKeepFallback()
    {
        using Corpus corpus = new();
        string music = corpus.Write(["data", "Music", "DMusic", "Track.SGT"], [1]);
        string localized = corpus.Write(["data", "LANG-EN", "Movies", "Opening.AVI"], [2]);
        corpus.Write(["data", "Movies", "Opening.AVI"], [3]);
        string shared = corpus.Write(["data", "Movies", "Ending.AVI"], [4]);
        using ClassicGameDataSource source = new(corpus.Root);

        Assert.Equal(music, source.ResolveFile("music/dmusic/track.sgt"));
        Assert.Equal(localized, source.ResolveFile("movies/opening.avi"));
        Assert.Equal(shared, source.ResolveFile("movies/ending.avi"));
    }

    [Fact]
    public void MissingLooseFileRetainsAbsentResourceAndCandidatePathSemantics()
    {
        using Corpus corpus = new();
        corpus.Write(["Data", "Sound", "existing.bin"], [1]);
        using ClassicGameDataSource source = new(corpus.Root);

        Assert.Null(source.Find(new("files/sound/missing.dat")));
        Assert.Equal(Path.Combine(corpus.Root, "Data", "Sound", "missing.dat"), source.ResolveFile("sound/missing.dat"));
        Assert.Null(source.Find(new("files/absent/missing.dat")));
    }

    [Theory]
    [InlineData("../audio.dat")]
    [InlineData("sound/../../audio.dat")]
    [InlineData("/sound/audio.dat")]
    [InlineData("sound/./audio.dat")]
    public void LooseFileResolutionRejectsTraversalAndRootedPaths(string path)
    {
        using Corpus corpus = new();
        using ClassicGameDataSource source = new(corpus.Root);

        Assert.Throws<ArgumentException>(() => source.ResolveFile(path));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CaseCollisionsFailEvenWhenOneNameMatchesExactly(bool directoryCollision)
    {
        using Corpus corpus = new();
        corpus.Write(["Data", "sound", "audio.dat"], [1]);
        corpus.Write(directoryCollision ? ["Data", "Sound", "other.dat"] : ["Data", "sound", "AUDIO.DAT"], [2]);
        string parent = directoryCollision ? Path.Combine(corpus.Root, "Data") : Path.Combine(corpus.Root, "Data", "sound");
        Assert.SkipUnless(Directory.EnumerateFileSystemEntries(parent).Count() == 2, "Case-sensitive filesystem is required to create a name collision.");
        using ClassicGameDataSource source = new(corpus.Root);

        Assert.Throws<InvalidDataException>(() => source.ResolveFile("sound/audio.dat"));
        Assert.Throws<InvalidDataException>(() => source.Find(new("files/sound/audio.dat")));
    }

    private sealed class Corpus : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "Memoria.FF8.ClassicFiles", Guid.NewGuid().ToString("N"));

        public Corpus()
        {
            Directory.CreateDirectory(Root);
        }

        public string Write(string[] components, byte[] bytes)
        {
            string path = Path.Combine([Root, .. components]);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, bytes);
            return path;
        }

        public void Dispose()
        {
            Directory.Delete(Root, true);
        }
    }
}
