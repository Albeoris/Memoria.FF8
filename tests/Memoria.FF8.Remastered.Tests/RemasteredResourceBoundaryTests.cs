using Memoria.FF8.Assets;
using Memoria.FF8.Assets.Remastered;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Remastered.Tests;

public sealed class RemasteredResourceBoundaryTests
{
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
    [InlineData("files/sound/audio.fmt")]
    [InlineData("files/sound/audio.dat")]
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
    public void UnsupportedSoundFileResolutionDoesNotReturnAClassicPath(string path)
    {
        using SyntheticResources files = new();
        string directory = Path.GetDirectoryName(files.Write([]))!;
        File.WriteAllBytes(Path.Combine(directory, "main.zzz"), SyntheticResources.Zzz());
        File.WriteAllBytes(Path.Combine(directory, "other.zzz"), SyntheticResources.Zzz());
        using RemasteredGameDataSource source = new(directory);
        Assert.Throws<NotImplementedException>(() => source.ResolveFile(path));
    }
}
