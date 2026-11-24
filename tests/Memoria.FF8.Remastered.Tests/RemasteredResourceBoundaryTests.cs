using Memoria.FF8.Assets;
using Memoria.FF8.Assets.Remastered;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Remastered.Tests;

public sealed class RemasteredResourceBoundaryTests
{
    [Theory]
    [InlineData("ffviii_en.exe.dat", true)]
    [InlineData("ff8_en.exe.dat", false)]
    public void ColdBattleMemoryUsesAuthenticatedArchiveImageWhenLooseImageIsUnavailable(string archiveName, bool modifiedLooseImage)
    {
        string? installed = Environment.GetEnvironmentVariable("MEMORIA_FF8_REMASTERED_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(installed) && Directory.Exists(installed), "Set MEMORIA_FF8_REMASTERED_DIR to run optional installed Remastered checks.");
        using ZzzArchive archive = new(Path.Combine(installed!, "main.zzz"));
        using Stream original = archive.Open("ffviii_en.exe.dat");
        using MemoryStream bytes = new();
        original.CopyTo(bytes);
        byte[] originalBytes = bytes.ToArray();
        byte[] modified = (byte[])originalBytes.Clone();
        modified[^1] ^= 1;

        using SyntheticResources files = new();
        string directory = Path.GetDirectoryName(files.Write([]))!;
        File.WriteAllBytes(Path.Combine(directory, "main.zzz"), SyntheticResources.Zzz((archiveName, originalBytes)));
        File.WriteAllBytes(Path.Combine(directory, "other.zzz"), SyntheticResources.Zzz());
        string looseImage = Path.Combine(directory, "FFVIII_EN.exe.dat");
        if (modifiedLooseImage)
        {
            File.WriteAllBytes(looseImage, modified);
        }

        using RemasteredGameDataSource source = new(directory);
        Assert.Equal(NativeFieldRuntimeProfile.RemasteredEnImageHash, source.NativeExecutableHash);
        Assert.NotNull(NativeBattleRetainedMemoryReader.ReadColdProcess(source).EffectMemory);
        if (modifiedLooseImage)
        {
            Assert.Equal(modified, File.ReadAllBytes(looseImage));
        }
        else
        {
            Assert.False(File.Exists(looseImage));
        }
    }

    [Fact]
    public void UnauthenticatedLooseAndArchiveImagesAreRejected()
    {
        using SyntheticResources files = new();
        string directory = Path.GetDirectoryName(files.Write([]))!;
        File.WriteAllBytes(Path.Combine(directory, "main.zzz"), SyntheticResources.Zzz(("ffviii_en.exe.dat", [3]), ("ff8_en.exe.dat", [5])));
        File.WriteAllBytes(Path.Combine(directory, "other.zzz"), SyntheticResources.Zzz());
        File.WriteAllBytes(Path.Combine(directory, "FFVIII_EN.exe.dat"), [1]);

        using RemasteredGameDataSource source = new(directory);
        NotImplementedException failure = Assert.Throws<NotImplementedException>(() => NativeBattleRetainedMemoryReader.ReadColdProcess(source));
        Assert.Contains("Verify the game files in Steam", failure.Message);
    }

    [Fact]
    public void RuntimeProfileRequiresTheSupportedDllEvenWithAnAuthenticatedGuestImage()
    {
        string? installed = Environment.GetEnvironmentVariable("MEMORIA_FF8_REMASTERED_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(installed) && Directory.Exists(installed), "Set MEMORIA_FF8_REMASTERED_DIR to run optional installed Remastered checks.");
        using ZzzArchive archive = new(Path.Combine(installed!, "main.zzz"));
        using Stream original = archive.Open("ffviii_en.exe.dat");
        using MemoryStream bytes = new();
        original.CopyTo(bytes);

        using SyntheticResources files = new();
        string directory = Path.GetDirectoryName(files.Write([]))!;
        File.WriteAllBytes(Path.Combine(directory, "main.zzz"), SyntheticResources.Zzz(("ffviii_en.exe.dat", bytes.ToArray())));
        File.WriteAllBytes(Path.Combine(directory, "other.zzz"), SyntheticResources.Zzz());
        using RemasteredGameDataSource source = new(directory);
        FileNotFoundException missing = Assert.Throws<FileNotFoundException>(() => source.RequireNativeRuntimeProfile());
        Assert.Contains("FFVIII_EFIGS.dll is missing", missing.Message);

        string dll = Path.Combine(directory, "FFVIII_EFIGS.dll");
        File.WriteAllBytes(dll, [0x4D, 0x5A, 0]);
        NotImplementedException unsupported = Assert.Throws<NotImplementedException>(() => source.RequireNativeRuntimeProfile());
        Assert.Contains("verify its game files in Steam", unsupported.Message);

        File.Copy(Path.Combine(installed!, "FFVIII_EFIGS.dll"), dll, overwrite: true);
        Assert.Same(NativeFieldRuntimeProfile.RemasteredEn, source.RequireNativeRuntimeProfile());
    }

    [Fact]
    public void RequiredRuntimeProfileIsRejectedByProviderWhileInspectionRemainsAvailable()
    {
        using SyntheticResources files = new();
        string directory = Path.GetDirectoryName(files.Write([]))!;
        File.WriteAllBytes(Path.Combine(directory, "main.zzz"), SyntheticResources.Zzz());
        File.WriteAllBytes(Path.Combine(directory, "other.zzz"), SyntheticResources.Zzz());
        using RemasteredGameDataSource source = new(directory);
        Assert.Null(source.NativeRuntimeProfile);
        Assert.Throws<NotImplementedException>(() => source.RequireNativeRuntimeProfile());
        Assert.Throws<NotImplementedException>(() => GameDataResources.RequireNativeRuntimeProfile(new ResourceService(source)));
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("de")]
    [InlineData("es")]
    [InlineData("it")]
    [InlineData("ja")]
    public void GuestTablesRejectLanguagesWithoutAuthenticatedLayouts(string language)
    {
        using SyntheticResources files = new();
        string directory = Path.GetDirectoryName(files.Write([]))!;
        File.WriteAllBytes(Path.Combine(directory, "main.zzz"), SyntheticResources.Zzz());
        File.WriteAllBytes(Path.Combine(directory, "other.zzz"), SyntheticResources.Zzz());
        using RemasteredGameDataSource source = new(directory, language);
        Assert.Throws<NotImplementedException>(() => new ResourceService(source).Read(new ResourceKey("tables/field-random")));
    }

    [Theory]
    [InlineData("runtime/native-executable")]
    [InlineData("runtime/disc-wrapper")]
    [InlineData("runtime/unknown")]
    public void UnsupportedRuntimeResourcesFailWithoutOpeningClassicFiles(string key)
    {
        using SyntheticResources files = new();
        string directory = Path.GetDirectoryName(files.Write([]))!;
        File.WriteAllBytes(Path.Combine(directory, "main.zzz"), SyntheticResources.Zzz());
        File.WriteAllBytes(Path.Combine(directory, "other.zzz"), SyntheticResources.Zzz());
        File.WriteAllBytes(Path.Combine(directory, "FF8_EN.exe"), [0x4D, 0x5A, 1, 2]);
        using IResourceSource source = GameDataSourceFactory.Open(directory);
        Assert.IsType<RemasteredGameDataSource>(source);
        Assert.Throws<NotImplementedException>(() => new ResourceService(source).Read(new ResourceKey(key)));
    }

    [Theory]
    [InlineData("sound/audio.fmt")]
    [InlineData("sound/audio.dat")]
    public void MissingSoundFileResolutionDoesNotReturnAClassicPath(string path)
    {
        using SyntheticResources files = new();
        string directory = Path.GetDirectoryName(files.Write([]))!;
        File.WriteAllBytes(Path.Combine(directory, "main.zzz"), SyntheticResources.Zzz());
        File.WriteAllBytes(Path.Combine(directory, "other.zzz"), SyntheticResources.Zzz());
        using RemasteredGameDataSource source = new(directory);
        Assert.Throws<FileNotFoundException>(() => source.ResolveFile(path));
    }
}
