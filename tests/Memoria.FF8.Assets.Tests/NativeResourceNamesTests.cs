using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeResourceNamesTests
{
    [Theory]
    [InlineData("en", "world/wmsetus.obj")]
    [InlineData("fr", "world/wmsetfr.obj")]
    [InlineData("de", "world/wmsetgr.obj")]
    [InlineData("es", "world/wmsetsp.obj")]
    [InlineData("it", "world/wmsetit.obj")]
    [InlineData("jp", "world/wmsetjp.obj")]
    [InlineData(" FR ", "world/wmsetfr.obj")]
    public void NativeWorldNamesFollowExplicitResourceLanguage(string language, string path)
    {
        Assert.Equal(new ResourceKey(path), NativeResourceNames.WorldSettings(language));
    }

    [Theory]
    [InlineData("")]
    [InlineData("auto")]
    [InlineData("ru")]
    [InlineData("wmsetfr.obj")]
    public void UnsupportedLanguageDoesNotFallBackToEnglish(string language)
    {
        Assert.Throws<NotSupportedException>(() => NativeResourceNames.WorldSettings(language));
    }

    [Fact]
    public void MissingLanguageIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => NativeResourceNames.WorldSettings(null!));
    }
}
