using System.Text;
using Albeoris.Games.FF8.FlArchives;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets.Tests;

public sealed class GameDataSourceLanguageTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Memoria.SourceLanguage.Tests", Guid.NewGuid().ToString("N"));

    public GameDataSourceLanguageTests()
    {
        Directory.CreateDirectory(directory);
        WriteZzz(Path.Combine(directory, "main.zzz"), ("data/lang-en/off_cards_names_en.dat", [1]), ("data/lang-fr/off_cards_names_fr.dat", [2]));
        WriteZzz(Path.Combine(directory, "other.zzz"));
    }

    [Fact]
    public void ExplicitRemasteredLanguageSelectsNativeResourceBanks()
    {
        using IResourceSource source = GameDataSourceFactory.Open(directory, null, " FR ");
        ResourceService resources = new(source);
        Assert.Equal("fr", source.Descriptor.Language);
        Assert.Equal("fr", resources.ResourceLanguage);
        Assert.Equal(new byte[] { 2 }, resources.Read(new("text/card-names")));
    }

    [Fact]
    public void AutomaticRemasteredLanguageRetainsTheProviderDefault()
    {
        using IResourceSource source = GameDataSourceFactory.Open(directory, null);
        Assert.Equal("en", source.Descriptor.Language);
        Assert.Equal(new byte[] { 1 }, new ResourceService(source).Read(new("text/card-names")));
    }

    [Fact]
    public void MissingSelectedBankDoesNotSilentlyUseEnglish()
    {
        using IResourceSource source = GameDataSourceFactory.Open(directory, null, "de");
        Assert.Equal("de", source.Descriptor.Language);
        Assert.Null(source.Find(new("text/card-names")));
        Assert.Throws<ArgumentException>(() => GameDataSourceFactory.Open(directory, null, "unknown"));
    }

    [Fact]
    public void SelectingAnotherModelSourcePreservesThePrimaryResourceLanguage()
    {
        string models = Path.Combine(directory, "models");
        Directory.CreateDirectory(models);
        using IResourceSource source = GameDataSourceFactory.Open(directory, models, "fr");
        Assert.Equal("fr", source.Descriptor.Language);
        Assert.Equal(new byte[] { 2 }, new ResourceService(source).Read(new("text/card-names")));
    }

    [Fact]
    public void LanguageProbeRequiresActualLocalizedFieldBanks()
    {
        (byte[] mapFl, byte[] mapFi, byte[] mapFs) = Archive(("maplist", Encoding.ASCII.GetBytes("test\n")));
        (byte[] textFl, byte[] textFi, byte[] textFs) = Archive(("test_en.msd", [1]), ("test_fr.msd", [2]));
        (byte[] fieldFl, byte[] fieldFi, byte[] fieldFs) = Archive(("mapdata.fl", mapFl), ("mapdata.fi", mapFi), ("mapdata.fs", mapFs), ("test.fl", textFl), ("test.fi", textFi), ("test.fs", textFs));
        WriteZzz(Path.Combine(directory, "main.zzz"), ("data/field.fl", fieldFl), ("data/field.fi", fieldFi), ("data/field.fs", fieldFs));
        var languages = LocalizationSourceExporter.ProbeLanguages(directory, TestContext.Current.CancellationToken);
        Assert.Equal(["en", "fr"], languages.Where(language => language.Available).Select(language => language.Language));
        using IResourceSource source = GameDataSourceFactory.Open(directory, null, "fr");
        Assert.Equal(new byte[] { 2 }, new ResourceService(source).Read(new("field/test/test.msd")));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("fr")]
    public void ResourceLanguageDoesNotBypassEnglishExecutableAuthentication(string language)
    {
        using IResourceSource source = GameDataSourceFactory.Open(directory, null, language);
        IGameRuntimeSource native = Assert.IsAssignableFrom<IGameRuntimeSource>(source);
        Assert.Null(native.NativeExecutableHash);
        File.WriteAllBytes(Path.Combine(directory, "FFVIII_FR.exe.dat"), [1, 2, 3]);
        Assert.Null(native.NativeExecutableHash);
        File.WriteAllBytes(Path.Combine(directory, "FFVIII_EN.exe.dat"), [1, 2, 3]);
        NotImplementedException error = Assert.Throws<NotImplementedException>(() => native.NativeExecutableHash);
        Assert.Contains("FFVIII_EN.exe.dat", error.Message);
    }

    private static (byte[] Fl, byte[] Fi, byte[] Fs) Archive(params (string Name, byte[] Data)[] entries)
    {
        using MemoryStream listing = new();
        using MemoryStream metrics = new();
        using MemoryStream content = new();
        using BinaryWriter writer = new(metrics, Encoding.ASCII, true);
        foreach ((string name, byte[] data) in entries)
        {
            listing.Write(Encoding.ASCII.GetBytes(FlArchive.InternalPathPrefix + name + "\r\n"));
            writer.Write((uint)data.Length);
            writer.Write((uint)content.Position);
            writer.Write(0);
            content.Write(data);
        }
        return (listing.ToArray(), metrics.ToArray(), content.ToArray());
    }

    private static void WriteZzz(string path, params (string Name, byte[] Data)[] entries)
    {
        using FileStream file = File.Create(path);
        using BinaryWriter writer = new(file, Encoding.UTF8);
        writer.Write((uint)entries.Length);
        ulong offset = (ulong)(4 + entries.Sum(entry => 16 + Encoding.UTF8.GetByteCount(entry.Name)));
        foreach ((string name, byte[] data) in entries)
        {
            byte[] encoded = Encoding.UTF8.GetBytes(name);
            writer.Write((uint)encoded.Length);
            writer.Write(encoded);
            writer.Write(offset);
            writer.Write((uint)data.Length);
            offset += (uint)data.Length;
        }
        foreach ((string _, byte[] data) in entries)
        {
            writer.Write(data);
        }
    }

    public void Dispose()
    {
        Directory.Delete(directory, true);
    }
}
