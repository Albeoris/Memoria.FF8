using Albeoris.Games.FF8.Imaging;
using ImageMagick;
using Memoria.FF8.Mods;
using Memoria.FF8.Resources;

namespace Memoria.FF8.App.Tests;

public sealed class GameResourceHostTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "ff8-resource-host-" + Guid.NewGuid().ToString("N"));
    private static readonly Rgba32 BaseColor = new(24, 48, 72);

    [Fact]
    public void DemasterThenTonberryThenBasePublishThroughTheSameStableHandle()
    {
        WriteImage("Demaster/high/textures/world/dat/texl/texl_001_0.png", MagickColors.Red);
        WriteTonberry();
        using GameResourceHost host = Host();
        TextureBuildContext context = Context(host);
        AssetHandle<TextureValue> handle = host.Resources.GetTexture(context);
        Assert.Equal(new Rgba32(255, 0, 0), handle.Value.Image[0, 0]);
        Assert.StartsWith("Demaster/high/", handle.Value.Provenance);
        long generation = host.Resources.Generation;
        Assert.True(host.Mods.SetEnabled("Demaster/high", false).Success);
        Assert.True(host.Mods.PreparePending(force: true));
        Assert.Equal(generation, host.Resources.Generation);
        Assert.StartsWith("Demaster/high/", handle.Value.Provenance);
        Assert.True(host.Mods.CommitPending());
        Assert.Equal(new Rgba32(0, 128, 0), handle.Value.Image[0, 0]);
        Assert.StartsWith("Tonberry/low/", handle.Value.Provenance);
        Assert.Same(handle, host.Resources.GetTexture(context));
        Assert.True(host.Mods.SetEnabled("Tonberry/low", false).Success);
        Assert.True(host.Pump(true));
        Assert.Equal(BaseColor, handle.Value.Image[0, 0]);
        Assert.Equal("classic", handle.Value.Provenance);
        Assert.Same(handle, host.Resources.GetTexture(context));
    }

    [Fact]
    public void InvalidResidentImageEditRetainsCatalogAndTextureUntilAValidRefresh()
    {
        WriteImage("Demaster/high/textures/world/dat/texl/texl_001_0.png", MagickColors.Red);
        using GameResourceHost host = Host();
        AssetHandle<TextureValue> handle = host.Resources.GetTexture(Context(host));
        ModCatalogSnapshot originalCatalog = host.Mods.Catalog;
        TextureValue originalValue = handle.Value;
        File.WriteAllText(Path.Combine(root, "Mods/Demaster/high/textures/world/dat/texl/texl_001_0.png"), "incomplete PNG");
        host.Mods.Refresh();
        Assert.False(host.Pump(true));
        Assert.Same(originalCatalog, host.Mods.Catalog);
        Assert.Same(originalValue, handle.Value);
        Assert.Contains(host.Mods.Diagnostics, diagnostic => diagnostic.Code == "reload-failed" && diagnostic.PackageId == "Demaster/high" && diagnostic.RelativePath == "textures/world/dat/texl/texl_001_0.png");
        WriteImage("Demaster/high/textures/world/dat/texl/texl_001_0.png", MagickColors.Blue);
        host.Mods.Refresh();
        Assert.True(host.Pump(true));
        Assert.Equal(new Rgba32(0, 0, 255), handle.Value.Image[0, 0]);
    }

    [Fact]
    public void InvalidUnresidentImageAtStartupDoesNotCrashFirstTextureAcquisition()
    {
        WriteImage("Demaster/broken/textures/world/dat/texl/texl_001_0.png", MagickColors.Red);
        string path = Path.Combine(root, "Mods/Demaster/broken/textures/world/dat/texl/texl_001_0.png");
        byte[] validBytes = File.ReadAllBytes(path);
        DateTime timestamp = File.GetLastWriteTimeUtc(path);
        File.WriteAllBytes(path, new byte[validBytes.Length]);
        File.SetLastWriteTimeUtc(path, timestamp);
        using GameResourceHost host = Host();
        AssetHandle<TextureValue>? handle = null;
        Exception? exception = Record.Exception(() => handle = host.Resources.GetTexture(Context(host)));
        Assert.Null(exception);
        Assert.NotNull(handle);
        Assert.Equal(BaseColor, handle.Value.Image[0, 0]);
        Assert.NotEmpty(host.Mods.Diagnostics);
        File.WriteAllBytes(path, validBytes);
        File.SetLastWriteTimeUtc(path, timestamp);
        host.Mods.Refresh();
        Assert.True(host.Pump(true));
        Assert.Equal(new Rgba32(255, 0, 0), handle.Value.Image[0, 0]);
    }

    [Fact]
    public void UnavailableDemasterLayoutDoesNotClaimPackageIsActive()
    {
        WriteImage("Demaster/unsupported/textures/battle.fs/hd_new/d0c000_0.png", MagickColors.Red);
        using GameResourceHost host = Host();
        ModPackage package = Assert.Single(host.Mods.Catalog.Packages);
        Assert.Equal(ModEffectiveStatus.NoUsableEntries, package.Status);
        Assert.Contains(host.Mods.Diagnostics, diagnostic => diagnostic.PackageId == package.Id);
    }

    [Fact]
    public void MalformedExistingTableKeepsPreviouslyPublishedTonberryTexture()
    {
        WriteTonberry();
        using GameResourceHost host = Host();
        AssetHandle<TextureValue> handle = host.Resources.GetTexture(Context(host));
        TextureValue original = handle.Value;
        ModCatalogSnapshot catalog = host.Mods.Catalog;
        WriteText("Tonberry/low/hashmap/test.csv", "unfinished CSV row");
        host.Mods.Refresh();
        Assert.False(host.Pump(true));
        Assert.Same(catalog, host.Mods.Catalog);
        Assert.Same(original, handle.Value);
        Assert.Contains(host.Mods.Diagnostics, diagnostic => diagnostic.Code == "Tonberry.MalformedRow");
    }

    [Fact]
    public void ResourceChangeNotificationObservesTheNewModCatalogAndTextureTogether()
    {
        WriteImage("Demaster/high/textures/world/dat/texl/texl_001_0.png", MagickColors.Red);
        using GameResourceHost host = Host();
        AssetHandle<TextureValue> handle = host.Resources.GetTexture(Context(host));
        long expectedCatalogGeneration = host.Mods.Catalog.Generation + 1;
        long observedCatalogGeneration = -1;
        Rgba32 observedColor = default;
        using IDisposable subscription = host.Resources.Subscribe(_ =>
        {
            observedCatalogGeneration = host.Mods.Catalog.Generation;
            observedColor = handle.Value.Image[0, 0];
        });
        Assert.True(host.Mods.SetEnabled("Demaster/high", false).Success);
        Assert.True(host.Pump(true));
        Assert.Equal(expectedCatalogGeneration, observedCatalogGeneration);
        Assert.Equal(BaseColor, observedColor);
    }

    [Fact]
    public void InvalidFirstUseImageAutomaticallyRetriesAndRecoversWithUnchangedFileMetadata()
    {
        WriteImage("Demaster/broken/textures/world/dat/texl/texl_001_0.png", MagickColors.Red);
        string path = Path.Combine(root, "Mods/Demaster/broken/textures/world/dat/texl/texl_001_0.png");
        byte[] validBytes = File.ReadAllBytes(path);
        DateTime timestamp = File.GetLastWriteTimeUtc(path);
        File.WriteAllBytes(path, new byte[validBytes.Length]);
        File.SetLastWriteTimeUtc(path, timestamp);
        using GameResourceHost host = Host();
        AssetHandle<TextureValue> handle = host.Resources.GetTexture(Context(host));
        Assert.Equal(BaseColor, handle.Value.Image[0, 0]);
        Assert.True(host.Mods.IsReloadPending);
        File.WriteAllBytes(path, validBytes);
        File.SetLastWriteTimeUtc(path, timestamp);
        DateTime deadline = DateTime.UtcNow.AddSeconds(3);
        while (handle.Value.Image[0, 0] != new Rgba32(255, 0, 0) && DateTime.UtcNow < deadline)
        {
            host.Pump();
            Thread.Sleep(20);
        }
        Assert.Equal(new Rgba32(255, 0, 0), handle.Value.Image[0, 0]);
        Assert.False(host.Mods.IsReloadPending);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }

    private GameResourceHost Host()
    {
        return new(Path.Combine(root, "empty-classic-game"), root, watch: false);
    }

    private static TextureBuildContext Context(GameResourceHost host)
    {
        PixelImage pixels = new(256, 256, Enumerable.Repeat(BaseColor, 256 * 256).ToArray());
        return new(new(new("world/texl.obj"), "texl/0"), host.Resources.Source, 1, "classic-world-texl", 256, 256, () => pixels);
    }

    private void WriteTonberry()
    {
        PixelImage pixels = new(256, 256, Enumerable.Repeat(BaseColor, 256 * 256).ToArray());
        ulong primary = TonberrySignatures.Calculate(pixels).Primary;
        WriteText("Tonberry/low/hashmap/test.csv", $"example_13,{primary}\n");
        WriteImage("Tonberry/low/textures/ex/example/example_13.png", MagickColors.Green);
    }

    private void WriteText(string relative, string text)
    {
        string path = Path.Combine(root, "Mods", relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private void WriteImage(string relative, IMagickColor<byte> color)
    {
        string path = Path.Combine(root, "Mods", relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using MagickImage image = new(color, 256, 256);
        image.Write(path, MagickFormat.Png);
    }
}
