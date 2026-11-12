using Memoria.FF8.Platform;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

[CollectionDefinition("FFVII launch environment", DisableParallelization = true)]
public sealed class Ff7LaunchEnvironmentCollection;

[Collection("FFVII launch environment")]
public sealed class ModelResourceLaunchOptionsTests
{
    [Fact]
    public void Ff7ResourcesRemainSeparateFromFf8GameplayAndModels()
    {
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
    public void DirectLaunchUsesTheSavedFf7SelectionUnlessExplicitlyOverridden()
    {
        const string variable = "MEMORIA_FF7_GAME_DIR";
        string? original = Environment.GetEnvironmentVariable(variable);
        DirectoryInfo settings = Directory.CreateTempSubdirectory("memoria-launch-");
        try
        {
            string saved = Path.Combine(settings.FullName, "Saved FF7 resources");
            string environment = Path.Combine(settings.FullName, "Environment FF7 resources");
            string commandLine = Path.Combine(settings.FullName, "Command line FF7 resources");
            string settingsPath = (ApplicationPaths.Current with { Configuration = settings.FullName }).LauncherSettings;
            new LauncherSettings(Ff7GameDirectory: saved).Save(settingsPath);

            Environment.SetEnvironmentVariable(variable, null);
            LaunchOptions direct = LaunchOptions.Parse(["--settings-dir", settings.FullName]);
            Assert.Equal(saved, direct.Ff7GameDirectory);
            Assert.False(direct.Ff7Debug);
            Assert.Equal(SceneKind.Title, direct.InitialState.Scene);

            Environment.SetEnvironmentVariable(variable, environment);
            Assert.Equal(environment, LaunchOptions.Parse(["--settings-dir", settings.FullName]).Ff7GameDirectory);
            Assert.Equal(commandLine, LaunchOptions.Parse(["--settings-dir", settings.FullName, "--ff7-game-dir", commandLine]).Ff7GameDirectory);

            Environment.SetEnvironmentVariable(variable, null);
            new LauncherSettings().Save(settingsPath);
            Assert.Null(LaunchOptions.Parse(["--settings-dir", settings.FullName]).Ff7GameDirectory);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, original);
            settings.Delete(recursive: true);
        }
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
