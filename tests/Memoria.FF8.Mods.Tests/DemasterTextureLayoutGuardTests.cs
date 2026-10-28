using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class DemasterTextureLayoutGuardTests
{
    [Theory]
    [InlineData("textures/world/dat/texl/texl_001_0.png", 256, 256)]
    [InlineData("textures/battle.fs/hd_new/mag005_0.dds", 256, 256)]
    [InlineData("textures/battle.fs/hd_new/c0m000_1.dds", 256, 256)]
    [InlineData("textures/battle.fs/hd_new/c0m000_0.dds", 128, 128)]
    public void UnsupportedDeclarationsAreDiagnosedWithoutReadingPixels(string path, int width, int height)
    {
        ModFile file = File(path, width, height);
        DemasterInspection inspection = DemasterTextureResolver.Inspect(new("fixture", GameDataEdition.Remastered, ["classic-world-texl", "native-battle-model-sheet", "native-battle-gf-atlas"]), new(1, [], [file]));
        Assert.Empty(inspection.UsablePackageIds);
        ModDiagnostic diagnostic = Assert.Single(inspection.Diagnostics);
        Assert.Equal(("demaster-unsupported-texture-layout", file.PackageId, file.RelativePath), (diagnostic.Code, diagnostic.PackageId, diagnostic.RelativePath));
    }

    [Fact]
    public void UnsupportedWorldDeclarationCannotSilentlyBecomeAnOrdinaryPage()
    {
        ModFile file = File("textures/world/dat/texl/texl_001_0.png");
        TextureBuildContext context = new(new(new("world/texl.obj"), "texl/0"), new("fixture", GameDataEdition.Classic, ["classic-world-texl"]), 1, "classic-world-texl", 256, 256, () => throw new InvalidOperationException("Original pixels must stay lazy."));
        ModTextureLoadException exception = Assert.Throws<ModTextureLoadException>(() => new DemasterTextureResolver(new(1, [], [file])).Resolve(context));
        Assert.Equal((file.PackageId, file.RelativePath), (exception.PackageId, exception.RelativePath));
    }

    [Fact]
    public void UnsupportedContentDeclarationCannotSilentlyBecomeAnOrdinaryUpload()
    {
        byte[] pixels = new byte[64];
        Assert.True(DemasterTextureFingerprint.TryCalculate(pixels, 4, 4, 4, 7, out DemasterTextureFingerprint fingerprint));
        ModFile file = File($"hashOutput/{fingerprint.High64:X16}_HD.dds");
        TextureBuildContext context = new(new(new("battle/native-pc-texture"), "fixture"), new("fixture", GameDataEdition.Remastered, ["demaster-content-upload"]), 1, "native-pc-texture-upload", 4, 4, () => throw new InvalidOperationException("Original pixels must stay lazy."))
        {
            NativeUpload = new(4, 4, 4, 7, () => pixels)
        };
        ModTextureLoadException exception = Assert.Throws<ModTextureLoadException>(() => new DemasterTextureResolver(new(1, [], [file])).Resolve(context));
        Assert.Equal((file.PackageId, file.RelativePath), (exception.PackageId, exception.RelativePath));
    }

    private static ModFile File(string path, int width = 256, int height = 256)
    {
        return new("Demaster/Fixture", ModFormat.Demaster, path, 1, () => throw new InvalidOperationException("Unsupported layout must not decode image content."))
        {
            TextureLayout = new(TextureLayoutMode.WholeNativeAtlas, width, height)
        };
    }
}
