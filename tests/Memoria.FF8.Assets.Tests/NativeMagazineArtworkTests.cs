using Albeoris.Games.FF8.FlArchives;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Tex;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeMagazineArtworkTests
{
    [Fact]
    public void OriginalIllustrationsKeepNativeCropsAndMultipartComposition()
    {
        string directory = InstalledGameDirectory();
        NativeMagazineArtwork artwork = NativeMagazineArtwork.Load(directory);
        for (int id = 0; id < 40; id++)
        {
            PixelImage image = artwork.GetImage(id);
            Assert.Contains(image.Pixels, pixel => pixel.A != 0);
            Assert.Same(image, artwork.GetImage(id));
        }
        Assert.Equal(104, artwork.GetImage(0).Height);
        Assert.Equal(96, artwork.GetImage(1).Height);
        PixelImage pet = ReadAtlas(directory, "mag07.TEX");
        AssertCrop(pet, 168, 0, artwork.GetImage(31), 0, 0, 56, 72);
        PixelImage occult = ReadAtlas(directory, "mag08.TEX");
        PixelImage composite = artwork.GetImage(36);
        Assert.Equal(184, composite.Width);
        Assert.Equal(96, composite.Height);
        AssertCrop(occult, 168, 0, composite, 0, 0, 88, 96);
        AssertCrop(occult, 160, 96, composite, 88, 0, 96, 96);
        Assert.Throws<ArgumentOutOfRangeException>(() => artwork.GetImage(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => artwork.GetImage(40));
    }

    [Fact]
    public void PrintedDuelButtonsRetainEveryOriginalControllerSymbol()
    {
        NativeMagazineArtwork artwork = NativeMagazineArtwork.Load(InstalledGameDirectory());
        for (int bit = 0; bit < 16; bit++)
        {
            Assert.Contains(artwork.GetButtonImage(bit).Pixels, pixel => pixel.A != 0);
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => artwork.GetButtonImage(16));
    }

    private static void AssertCrop(PixelImage atlas, int sourceX, int sourceY, PixelImage image, int destinationX, int destinationY, int width, int height)
    {
        for (int y = 0; y < height; y++)
        {
            Assert.Equal(atlas.Pixels.AsSpan((sourceY + y) * atlas.Width + sourceX, width).ToArray(), image.Pixels.AsSpan((destinationY + y) * image.Width + destinationX, width).ToArray());
        }
    }

    private static PixelImage ReadAtlas(string directory, string name)
    {
        using IFlArchive archive = FlArchive.OpenForRead(Path.Combine(directory, "Data", "lang-en", "menu.fs"), FlArchiveRepresentation.Files);
        using Stream stream = archive.Entries.Single(entry => Path.GetFileName(entry.RelativePath).Equals(name, StringComparison.OrdinalIgnoreCase)).OpenForRead();
        using MemoryStream content = new();
        stream.CopyTo(content);
        return TexFile.Read(content.ToArray()).Decode();
    }

    private static string InstalledGameDirectory()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(File.Exists(Path.Combine(directory, "Data", "lang-en", "menu.fs")), "Installed original magazine resources are required.");
        return directory;
    }
}
