using System.Reflection;
using System.Runtime.CompilerServices;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class RemasteredStartupMenuTests
{
    [Theory]
    [InlineData("MEMORIA_FF8_REMASTERED_DIR")]
    [InlineData("MEMORIA_FF8_GAME_DIR")]
    public void ColdTitleOpensLiveNewGameAndConfigurationThroughTheHostProvider(string installationVariable)
    {
        string? directory = Environment.GetEnvironmentVariable(installationVariable);
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory), $"Set {installationVariable} to an installed game directory.");
        GameSession session = GameWorld.CreateColdProcess(directory!);
        Assert.Equal(SceneKind.Title, session.State.Scene);
        // Exercise the production host provider without constructing a graphics device or sampling physical input.
        using GameResourceHost resources = new(directory!, watch: false);
        string saveRoot = Path.Combine(Path.GetTempPath(), "memoria-title-menu-" + Guid.NewGuid().ToString("N"));
        PlayerSaveMenu saveMenu = new(new PlayerSaveStore(Path.Combine(saveRoot, "checkpoint.json"), directory: Path.Combine(saveRoot, "player-saves")));
        RecreateGame host = (RecreateGame)RuntimeHelpers.GetUninitializedObject(typeof(RecreateGame));
        Set(host, "session", session);
        Set(host, "options", new LaunchOptions { GameDirectory = directory! });
        Set(host, "uiPreferences", new UiPreferences());
        Set(host, "newGameMagicPreferences", new GameplayPreferences());
        Set(host, "keyboard", new KeyboardInput());
        Set(host, "resourceHost", resources);
        Set(host, "saveMenu", saveMenu);
        MethodInfo read = typeof(RecreateGame).GetMethod("ReadLiveMenuData", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Func<MenuData> provider = read.CreateDelegate<Func<MenuData>>(host);
        MenuController menus = new(provider);
        foreach (string screen in new[] { "new-game-options", "config" })
        {
            menus.Open(screen, false);
            Assert.False(menus.IsDemo);
            Assert.NotNull(menus.BuildView());
            Assert.Contains("new-game-start", menus.Data.SupportedActions);
            Assert.Equal(SceneKind.Title, session.State.Scene);
            menus.Close();
        }
        using GameAssets assets = new(directory!);
        Assert.Equal(new byte[] { 0, 0, 0, 1, 1, 1, 2, 3, 4, 5, 6, 1, 3, 3, 3, 3, 6, 1, 1, 6, 6, 0 }, assets.ReadItemIconKinds());
        Assert.Equal(assets.NativeExecutableHash, assets.ReadNativeWeaponShopDefinitions().ExecutableHash);
        Assert.Equal(assets.NativeExecutableHash, assets.ReadNativeSolomonRingDefinition().ExecutableHash);
        Assert.Equal(assets.NativeExecutableHash, assets.ReadNativeRefinementDefinitions().ExecutableHash);
    }

    private static void Set(RecreateGame host, string name, object value)
    {
        typeof(RecreateGame).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(host, value);
    }
}
