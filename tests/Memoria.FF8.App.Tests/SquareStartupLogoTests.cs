using System.Text;
using System.Text.Json;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Assets.Remastered;
using Memoria.FF8.Mods;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class SquareStartupLogoTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Memoria-StartupLogo-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PublishesTheLogoThroughTheLiveTextureAndRetainsInstantSkip(bool useNativePackage)
    {
        byte[] archiveLogo = Logo(new(8, 16, 24, 255));
        byte[] newLogo = Logo(new(130, 80, 40, 255));
        CreateArchives(archiveLogo);
        if (useNativePackage)
        {
            const string package = "Mods/Memoria/Opening";
            Write($"{package}/{NativeModDescriptor.FileName}", JsonSerializer.SerializeToUtf8Bytes(new { id = "fixture-opening", name = "Opening", package = "Opening", description = new { en = "Startup artwork" } }));
            Write($"{package}/{ShellArtworkMods.NativeSquareLogoPath}", newLogo);
        }
        else
        {
            Write("DEMASTER_EXP/" + RemasteredGameDataSource.SquareLogoPath, newLogo);
        }

        using GameResourceHost host = new(root, root, watch: false, modsDirectory: Path.Combine(root, "Mods"));
        PixelImage stock = new(NativeStartupArtwork.Width, NativeStartupArtwork.Height);
        AssetHandle<TextureValue> logo = StartupTexture(host.Resources, stock);
        Assert.True(logo.Value.IsModReplacement, string.Join(Environment.NewLine, host.Mods.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.Equal(new Rgba32(130, 80, 40, 255), logo.Value.Image[0, 0]);
        Assert.Equal(new PixelBounds(53, 0, 533, 400), NativeStartupArtwork.PictureBounds(NativeStartupArtwork.SquareLogoResourceIndex, logo.Value.Image.Width, logo.Value.Image.Height, NativeStartupArtwork.Width, NativeStartupArtwork.Height));
        Assert.Equal(new PixelBounds(0, 0, 640, 400), NativeStartupArtwork.PictureBounds(5, 4, 3, 640, 400));

        StartupSequence sequence = new(true);
        Assert.True(sequence.TryConfigure(new(SkipPublisherMovie: true), host.Mods.Catalog.Generation));
        Assert.Equal(StartupStage.SquareLogo, sequence.Stage);
        Assert.True(sequence.HandleInput(true, false));
        Assert.Equal(StartupStage.Credits, sequence.Stage);
        Assert.Equal(TimeSpan.Zero, sequence.Elapsed);
        sequence.RepeatCredits();
        Assert.Equal(StartupStage.SquareLogo, sequence.Stage);
        Assert.True(sequence.HandleInput(false, true));
        Assert.Equal(StartupStage.Complete, sequence.Stage);

        ModPackage packageStatus = Assert.Single(host.Mods.Catalog.Packages, package => package.Status == ModEffectiveStatus.Active);
        Assert.True(host.Mods.SetEnabled(packageStatus.Id, false).Success);
        host.Pump(force: true);
        Assert.Same(stock, logo.Value.Image);
    }

    [Fact]
    public void StockEquivalentDemasterCopyKeepsTheStartupTexture()
    {
        byte[] archiveLogo = Logo(new(20, 30, 40, 255));
        CreateArchives(archiveLogo);
        Write("DEMASTER_EXP/" + RemasteredGameDataSource.SquareLogoPath, archiveLogo);
        using GameResourceHost host = new(root, root, watch: false, modsDirectory: Path.Combine(root, "Mods"));
        PixelImage stock = new(NativeStartupArtwork.Width, NativeStartupArtwork.Height);
        Assert.Same(stock, StartupTexture(host.Resources, stock).Value.Image);
        Assert.Contains(host.Mods.Diagnostics, diagnostic => diagnostic.Code == "Demaster.StockSquareLogo" && diagnostic.RelativePath == RemasteredGameDataSource.SquareLogoPath);
        Assert.Equal(new PixelBounds(0, 0, 640, 400), NativeStartupArtwork.PictureBounds(NativeStartupArtwork.SquareLogoResourceIndex, stock.Width, stock.Height, 640, 400));
    }

    [Fact]
    public void InstalledStockCopyKeepsTheDecodedSquareArtwork()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_REMASTERED_DIR");
        string? export = Environment.GetEnvironmentVariable("MEMORIA_FF8_DEMASTER_DATA_DIR");
        Assert.SkipUnless(Directory.Exists(game) && Directory.Exists(export), "Set the optional Remastered installation and Demaster data fixture directories.");
        string logoPath = Path.Combine(export!, RemasteredGameDataSource.SquareLogoPath);
        Assert.SkipUnless(File.Exists(logoPath), "The optional Demaster data fixture has no Square logo.");
        using RemasteredGameDataSource source = new(game!);
        using Stream? archivedLogo = source.OpenStockSquareLogo();
        Assert.NotNull(archivedLogo);
        byte[] png = File.ReadAllBytes(logoPath);
        Assert.Equal(archivedLogo.Length, png.LongLength);
        ModFile file = new("fixture", ModFormat.Demaster, RemasteredGameDataSource.SquareLogoPath, 1, () => png);
        List<ModDiagnostic> diagnostics = [];
        Assert.Null(ShellArtworkMods.Prepare(new(1, [], [file]), source, diagnostics));
        Assert.Equal("Demaster.StockSquareLogo", Assert.Single(diagnostics).Code);
        NativeStartupArtwork artwork = NativeStartupArtwork.Load(game!, new ResourceService(source));
        PixelImage logo = artwork.Read(NativeStartupArtwork.SquareLogoResourceIndex);
        Assert.Equal(NativeStartupArtwork.Width, logo.Width);
        Assert.Equal(NativeStartupArtwork.Height, logo.Height);
    }

    private static AssetHandle<TextureValue> StartupTexture(ResourceService resources, PixelImage stock)
    {
        return resources.GetTexture(new(NativeStartupArtwork.SquareLogoTexture, resources.Source, 1, "native-artwork", stock.Width, stock.Height, () => stock));
    }

    private static byte[] Logo(Rgba32 color)
    {
        PixelImage image = new(4, 3);
        Array.Fill(image.Pixels, color);
        return LocalizationArtworkExporter.EncodePng(image);
    }

    private void CreateArchives(byte[] logo)
    {
        Directory.CreateDirectory(root);
        byte[] name = Encoding.UTF8.GetBytes(RemasteredGameDataSource.SquareLogoPath);
        using (BinaryWriter main = new(File.Create(Path.Combine(root, "main.zzz"))))
        {
            main.Write(1);
            main.Write(name.Length);
            main.Write(name);
            main.Write((long)(sizeof(int) * 3 + sizeof(long) + name.Length));
            main.Write(logo.Length);
            main.Write(logo);
        }
        File.WriteAllBytes(Path.Combine(root, "other.zzz"), new byte[sizeof(int)]);
    }

    private void Write(string relativePath, byte[] bytes)
    {
        string path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}
