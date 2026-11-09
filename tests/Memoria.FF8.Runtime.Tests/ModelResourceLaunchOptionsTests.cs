using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class ModelResourceLaunchOptionsTests
{
    [Fact]
    public void Ff7ResourcesRemainSeparateFromFf8GameplayAndModels()
    {
        Assert.Equal(LaunchOptions.ConfiguredFf7GameDirectory, LaunchOptions.Parse([]).Ff7GameDirectory);
        string primary = Path.Combine(Path.GetTempPath(), "FF8 primary");
        string secondary = Path.Combine(Path.GetTempPath(), "FF7 resources");
        string models = Path.Combine(Path.GetTempPath(), "FF8 models");
        LaunchOptions options = LaunchOptions.Parse(["--game-dir", primary, "--ff7-game-dir", secondary, "--model-source-dir", models]);
        Assert.Equal(Path.GetFullPath(primary), options.GameDirectory);
        Assert.Equal(Path.GetFullPath(secondary), options.Ff7GameDirectory);
        Assert.Equal(Path.GetFullPath(models), options.ModelSourceDirectory);
        Assert.Equal(SceneKind.Title, options.InitialState.Scene);
        Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--ff7-game-dir"]));
    }

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
