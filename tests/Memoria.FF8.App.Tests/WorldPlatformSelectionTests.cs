using Memoria.FF7.Runtime;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Memoria.FF8.App.Tests;

public sealed class WorldPlatformSelectionTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Memoria.WorldPlatform.Tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("3837340")]
    [InlineData("39140")]
    public void SelectedFf7ManifestResolvesItsOwnProduct(string product)
    {
        string installation = CreateInstallation(product);
        GamePlatformContext context = GamePlatformContextResolver.ResolveFf7(installation);
        Assert.Equal(product, context.ProductId);
        Assert.Equal(GameDistributionProvider.Steam, context.Provider);
        Assert.True(context.CanInitializeExternalBackend);
        Assert.False(GamePlatformContextResolver.ResolveFf7(installation, localOnly: true).CanInitializeExternalBackend);
        Assert.Null(GamePlatformContextResolver.Resolve(installation).ProductId);
    }

    [Fact]
    public void ConflictingFf7ManifestsCannotInitializeSteam()
    {
        string installation = CreateInstallation("3837340");
        WriteManifest("39140");
        Assert.False(GamePlatformContextResolver.ResolveFf7(installation).CanInitializeExternalBackend);
    }

    [Fact]
    public void InitialCombinedWorldOverridesTheLaunchModeWithoutReadingAStalePath()
    {
        LaunchOptions options = new() { StatePath = Path.Combine(directory, "absent.ffsave"), Ff7Debug = false };
        Assert.Equal(SessionGame.Ff7, RecreateGame.ReadAchievementGame(options, new CombinedSessionSave { ActiveGame = SessionGame.Ff7 }));
        Assert.Equal(SessionGame.Ff8, RecreateGame.ReadAchievementGame(options with { Ff7Debug = true }, new CombinedSessionSave { ActiveGame = SessionGame.Ff8 }));
    }

    [Fact]
    public void SuspendedAndDisposedLifetimesDoNotTouchUninitializedGameplayOrSteam()
    {
        RecreateGame game = (RecreateGame)RuntimeHelpers.GetUninitializedObject(typeof(RecreateGame));
        FieldInfo suspended = typeof(RecreateGame).GetField("achievementsSuspended", BindingFlags.Instance | BindingFlags.NonPublic)!;
        FieldInfo disposed = typeof(RecreateGame).GetField("achievementsDisposed", BindingFlags.Instance | BindingFlags.NonPublic)!;
        MethodInfo initialize = typeof(RecreateGame).GetMethod("InitializeAchievements", BindingFlags.Instance | BindingFlags.NonPublic)!;
        MethodInfo update = typeof(RecreateGame).GetMethod("UpdateAchievements", BindingFlags.Instance | BindingFlags.NonPublic)!;
        suspended.SetValue(game, true);
        initialize.Invoke(game, null);
        update.Invoke(game, [true]);
        suspended.SetValue(game, false);
        disposed.SetValue(game, true);
        initialize.Invoke(game, null);
        update.Invoke(game, [true]);
    }

    private string CreateInstallation(string product)
    {
        string installation = Path.Combine(directory, "steamapps", "common", "Selected game");
        Directory.CreateDirectory(Path.Combine(installation, "ff7", "workingdir", "data"));
        WriteManifest(product);
        return installation;
    }

    private void WriteManifest(string product)
    {
        File.WriteAllText(Path.Combine(directory, "steamapps", $"appmanifest_{product}.acf"), $"\"AppState\" {{ \"appid\" \"{product}\" \"installdir\" \"Selected game\" }}");
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
