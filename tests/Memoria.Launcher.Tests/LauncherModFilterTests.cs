namespace Memoria.Launcher.Tests;

public sealed class LauncherModFilterTests
{
    [Theory]
    [InlineData("audio", "All categories", true)]
    [InlineData("voices", "Audio", true)]
    [InlineData("voices", "Gameplay", false)]
    [InlineData("missing", "All categories", false)]
    [InlineData("", "Audio", true)]
    public void SearchMatchesNamesAndDescriptionsWithinSelectedCategory(string search, string category, bool expected)
    {
        Assert.Equal(expected, LauncherWindow.MatchesLauncherModFilter("Audio pack", "New character voices", "Audio", search, category));
    }
}
