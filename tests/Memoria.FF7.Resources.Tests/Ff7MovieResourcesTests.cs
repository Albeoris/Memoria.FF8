using System.Text;

namespace Memoria.FF7.Resources.Tests;

public sealed class Ff7MovieResourcesTests
{
    [Fact]
    public void InstalledReachedMovieUsesSelectedLanguageVideoWithoutInventingCameraAlias()
    {
        Ff7Installation installation = Installed();
        Ff7PcExecutableData native = Ff7PcExecutableData.Load(installation);
        Assert.Equal("jenova_e.avi", native.MovieFiles[36]);
        Ff7MovieResources movie = Ff7MovieResources.Load(installation, 36, native);
        Assert.Equal(Path.Combine(installation.DataDirectory, "lang-en", "movies", native.MovieFiles[36]), movie.Path);
        Assert.True(File.Exists(movie.Path));
        Assert.Null(movie.Cameras);
        Ff7MovieResources shared = Ff7MovieResources.Load(installation, 0, native);
        Assert.Equal(Path.Combine(installation.GetRequired(Ff7ResourceRole.Movies).Path, native.MovieFiles[0]), shared.Path);
        Assert.True(File.Exists(shared.Path));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MovieLookupUsesSelectedLanguageThenSharedPreservesAbsentVideoAndRejectsAmbiguousCamera(bool nested)
    {
        Ff7PcExecutableData native = Ff7PcExecutableData.Load(Installed());
        string name = native.MovieFiles[36];
        string root = Path.Combine(Path.GetTempPath(), $"ff7-movie-resource-{Guid.NewGuid():N}");
        string data = nested ? Path.Combine(root, "ff7", "workingdir", "data") : Path.Combine(root, "data");
        try
        {
            foreach (string directory in new[] { "field", "cd", "movies", "lang-en/movies", "lang-fr/movies" })
            {
                Directory.CreateDirectory(Path.Combine(data, directory));
            }
            File.WriteAllBytes(Path.Combine(data, "field", "fflevel.lgp"), []);
            File.WriteAllBytes(Path.Combine(data, "field", "char.lgp"), []);
            string archive = Path.Combine(data, "cd", "moviecam.lgp");
            WriteCameraArchive(archive, Path.ChangeExtension(name, ".cam"), 0);
            Ff7Installation installation = Ff7Installation.Discover(root, "fr");
            string selected = Path.Combine(data, "lang-fr", "movies", name);
            string otherLanguage = Path.Combine(data, "lang-en", "movies", name);
            string common = Path.Combine(data, "movies", name);
            File.WriteAllBytes(otherLanguage, []);
            Ff7MovieResources absent = Ff7MovieResources.Load(installation, 36, native);
            Assert.Equal(common, absent.Path);
            Assert.False(File.Exists(absent.Path));
            Assert.Null(absent.Cameras);
            File.WriteAllBytes(common, []);
            Assert.Equal(common, Ff7MovieResources.Load(installation, 36, native).Path);
            File.WriteAllBytes(selected, []);
            Assert.Equal(selected, Ff7MovieResources.Load(installation, 36, native).Path);
            WriteCameraArchive(archive, Path.ChangeExtension(name, ".cam"), 2);
            Assert.Throws<InvalidDataException>(() => Ff7MovieResources.Load(installation, 36, native));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    private static Ff7Installation Installed()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original FFVII resources for the bounded movie resource check.");
        return Ff7Installation.Discover(root);
    }

    private static void WriteCameraArchive(string path, string name, int count)
    {
        using FileStream stream = File.Create(path);
        using BinaryWriter writer = new(stream, Encoding.ASCII);
        writer.Write((ushort)0);
        writer.Write(Encoding.ASCII.GetBytes("SQUARESOFT"));
        writer.Write(count);
        int dataStart = 16 + count * 27 + 3602;
        for (int index = 0; index < count; index++)
        {
            WriteName();
            writer.Write(dataStart + index * 24);
            writer.Write((byte)0);
            writer.Write((ushort)0);
        }
        writer.Write(new byte[3600]);
        writer.Write((ushort)0);
        for (int index = 0; index < count; index++)
        {
            WriteName();
            writer.Write(0);
        }

        void WriteName()
        {
            byte[] bytes = Encoding.ASCII.GetBytes(name);
            writer.Write(bytes);
            writer.Write(new byte[20 - bytes.Length]);
        }
    }
}
