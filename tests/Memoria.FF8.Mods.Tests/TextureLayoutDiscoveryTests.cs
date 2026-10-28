using System.Text.Json;

namespace Memoria.FF8.Mods.Tests;

public sealed class TextureLayoutDiscoveryTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "ff8-texture-layouts-" + Guid.NewGuid().ToString("N"));
    private const string ImagePath = "textures/model_0.dds";

    [Fact]
    public void MetadataAttachesOnlyToItsOwnPackageFileAndIsNotPayload()
    {
        Write("low", ImagePath, "image");
        Write("low", TextureLayoutManifest.FileName, Manifest());
        Write("low", "mod.ini", "priority=1\n");
        Write("high", ImagePath, "higher priority image");
        Write("high", "mod.ini", "priority=2\n");
        using ModRuntime runtime = Open();
        Publish(runtime);
        ModFile[] files = runtime.Catalog.FindAll(ModFormat.Demaster, ImagePath).ToArray();
        Assert.Equal(2, files.Length);
        Assert.Equal("Demaster/high", files[0].PackageId);
        Assert.Null(files[0].TextureLayout);
        Assert.Equal("Demaster/low", files[1].PackageId);
        Assert.Equal(new(TextureLayoutMode.WholeNativeAtlas, 256, 256), files[1].TextureLayout);
        Assert.Equal(2, runtime.Catalog.Files.Count);
        Assert.DoesNotContain(runtime.Catalog.Files, file => file.RelativePath == TextureLayoutManifest.FileName);
    }

    [Fact]
    public void DeclarationChangeAndRemovalReviseUnchangedImagesAndPreserveOldSnapshots()
    {
        string image = Write("sample", ImagePath, "original image");
        string manifest = Write("sample", TextureLayoutManifest.FileName, Manifest());
        DateTime imageTime = File.GetLastWriteTimeUtc(image);
        DateTime manifestTime = File.GetLastWriteTimeUtc(manifest);
        int preparations = 0;
        using ModRuntime runtime = Open(prepare: _ =>
        {
            preparations++;
            return ModPreparationResult.Accepted;
        });
        Publish(runtime);
        ModFile original = Assert.Single(runtime.Catalog.Files);
        File.WriteAllText(manifest, Manifest(128));
        File.SetLastWriteTimeUtc(manifest, manifestTime);
        runtime.RequestRefresh();
        Publish(runtime);
        ModFile changed = Assert.Single(runtime.Catalog.Files);
        Assert.True(changed.Revision > original.Revision);
        Assert.NotSame(original, changed);
        Assert.Equal(original.ContentStamp, changed.ContentStamp);
        Assert.Equal(256, original.TextureLayout!.CanvasWidth);
        Assert.Equal(128, changed.TextureLayout!.CanvasWidth);
        Assert.Equal(original.ReadBytes(), changed.ReadBytes());
        File.Delete(manifest);
        runtime.RequestRefresh();
        Publish(runtime);
        ModFile removed = Assert.Single(runtime.Catalog.Files);
        Assert.True(removed.Revision > changed.Revision);
        Assert.Null(removed.TextureLayout);
        Assert.Equal(imageTime, File.GetLastWriteTimeUtc(image));
        Assert.Equal(3, preparations);
    }

    [Fact]
    public void ARejectedManifestRetainsTheCommittedCatalogAndRecoversAfterCorrection()
    {
        Write("sample", ImagePath, "image");
        string manifest = Write("sample", TextureLayoutManifest.FileName, Manifest());
        using ModRuntime runtime = Open();
        Publish(runtime);
        ModCatalogSnapshot committed = runtime.Catalog;
        File.WriteAllText(manifest, "{\"version\":99,\"textures\":{}}");
        runtime.RequestRefresh();
        Assert.False(runtime.PreparePending(force: true));
        Assert.False(runtime.CommitPending());
        Assert.Same(committed, runtime.Catalog);
        ModDiagnostic diagnostic = Assert.Single(runtime.Diagnostics, item => item.Code == "invalid-texture-layouts");
        Assert.Equal(("Demaster/sample", TextureLayoutManifest.FileName), (diagnostic.PackageId, diagnostic.RelativePath));
        Assert.Equal(256, Assert.Single(runtime.Catalog.Files).TextureLayout!.CanvasWidth);
        File.WriteAllText(manifest, Manifest(128));
        runtime.RequestRefresh();
        Publish(runtime);
        Assert.True(runtime.Catalog.Generation > committed.Generation);
        Assert.Equal(128, Assert.Single(runtime.Catalog.Files).TextureLayout!.CanvasWidth);
    }

    [Fact]
    public void InertDeclarationsCannotBorrowAnImageFromAnotherPackage()
    {
        Write("other", ImagePath, "foreign image");
        Write("sample", TextureLayoutManifest.FileName, Manifest());
        using ModRuntime runtime = Open();
        Assert.False(runtime.PreparePending(force: true));
        Assert.Empty(runtime.Catalog.Files);
        Assert.Contains(runtime.Diagnostics, diagnostic => diagnostic.Code == "invalid-texture-layouts" && diagnostic.PackageId == "Demaster/sample");
    }

    [Fact]
    public void MetadataWatcherRefreshesTheDeclaredImageWithoutAnImageWrite()
    {
        string image = Write("sample", ImagePath, "image");
        string manifest = Write("sample", TextureLayoutManifest.FileName, Manifest());
        DateTime imageTime = File.GetLastWriteTimeUtc(image);
        using ModRuntime runtime = Open(watch: true);
        Publish(runtime);
        ModFile original = Assert.Single(runtime.Catalog.Files);
        File.WriteAllText(manifest, Manifest(128));
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            runtime.PreparePending(force: true);
            runtime.CommitPending();
            if (runtime.Catalog.Files.Single().TextureLayout?.CanvasWidth == 128)
            {
                break;
            }
            Thread.Sleep(20);
        }
        ModFile updated = Assert.Single(runtime.Catalog.Files);
        Assert.Equal(128, updated.TextureLayout!.CanvasWidth);
        Assert.True(updated.Revision > original.Revision);
        Assert.Equal(imageTime, File.GetLastWriteTimeUtc(image));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }

    private ModRuntime Open(bool watch = false, Func<ModCatalogSnapshot, ModPreparationResult>? prepare = null) => new(root, watch: watch, debounce: TimeSpan.Zero, prepare: prepare);

    private string Write(string package, string relative, string contents)
    {
        string path = Path.Combine(root, "Mods", "Demaster", package, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
        return path;
    }

    private static void Publish(ModRuntime runtime)
    {
        Assert.True(runtime.PreparePending(force: true));
        Assert.True(runtime.CommitPending());
    }

    private static string Manifest(int width = 256)
    {
        return JsonSerializer.Serialize(new { version = 1, textures = new Dictionary<string, object> { [ImagePath] = new { layout = "whole-native-atlas", canvasWidth = width, canvasHeight = 256 } } });
    }
}
