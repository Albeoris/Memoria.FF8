using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class ResourceLanguageLaunchOptionsTests
{
    [Fact]
    public void DefaultAndAutoPreserveInstalledResourceLanguage()
    {
        Assert.Null(LaunchOptions.Parse([]).ResourceLanguage);
        Assert.Null(LaunchOptions.Parse(["--resource-language", "auto"]).ResourceLanguage);
    }

    [Theory]
    [InlineData("en", "en")]
    [InlineData("FR", "fr")]
    [InlineData("de", "de")]
    [InlineData("es", "es")]
    [InlineData("it", "it")]
    public void ExplicitResourceLanguageIsNormalizedAndPreservesOtherOptions(string input, string expected)
    {
        LaunchOptions options = LaunchOptions.Parse(["--resource-language", input, "--mute-audio", "--resolution", "1280x720"]);
        Assert.Equal(expected, options.ResourceLanguage);
        Assert.True(options.MuteAudio);
        Assert.Equal(1280, options.Width);
        Assert.Equal(720, options.Height);
    }

    [Fact]
    public void InvalidOrMissingLanguageIsRejected()
    {
        Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--resource-language", "unknown"]));
        Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--resource-language"]));
    }
}
