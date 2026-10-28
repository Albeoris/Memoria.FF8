using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class ModelResourceLaunchOptionsTests
{
    [Fact]
    public void ModelSourceDoesNotReplaceGameplayDirectoryOrLanguage()
    {
        string primary = Path.Combine(Path.GetTempPath(), "Primary game");
        string models = Path.Combine(Path.GetTempPath(), "Model resources");
        LaunchOptions options = LaunchOptions.Parse(["--game-dir", primary, "--model-source-dir", models, "--resource-language", "fr"]);
        Assert.Equal(Path.GetFullPath(primary), options.GameDirectory);
        Assert.Equal(Path.GetFullPath(models), options.ModelSourceDirectory);
        Assert.Equal("fr", options.ResourceLanguage);
        Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--model-source-dir"]));
    }
}
