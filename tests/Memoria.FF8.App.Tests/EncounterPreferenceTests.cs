using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class EncounterPreferenceTests
{
    [Theory]
    [InlineData("Full combat", true)]
    [InlineData("Instant victory", false)]
    public void BattleSettingsInputsApplyTheSelectedPolicyAndEncounterSettingToCurrentGame(string label, bool fullCombat)
    {
        string game = InstalledGame();
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using GameWorld world = new(session, game);
        UiPreferences preferences = new UiPreferences().WithSetting("Battle policy", label).WithSetting("Random encounters", "Off");
        foreach (BattleInput input in preferences.CreateBattleSettingsInputs())
        {
            world.ApplyBattleInput(input);
        }
        Assert.Equal(fullCombat, session.State.FullBattlePlaythrough == true);
        Assert.False(session.State.RandomEncounters!.Enabled);
        world.ApplyBattleInput(new BattleInput(BattleInputKind.ConfigureEncounters, RandomEncounters: true));
        Assert.True(session.State.RandomEncounters.Enabled);
    }

    [Fact]
    public void DebugRoomBattleSettingsApplyImmediatelyThroughTheHostWithoutOpeningLaboratory()
    {
        string game = InstalledGame();
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using GameWorld world = new(session, game);
        UiPreferences preferences = new();
        MenuController menus = new();
        menus.ConnectPreferences(() => preferences, action =>
        {
            preferences = preferences.WithSetting(action.SubjectId, action.Value);
            foreach (BattleInput input in preferences.CreateBattleSettingsInputs())
            {
                world.ApplyBattleInput(input);
            }
        });
        menus.Open("debug-battle", true);
        menus.Navigate(MenuInput.Confirm);
        Assert.True(session.State.FullBattlePlaythrough);
        Assert.Equal("Full combat", preferences.BattlePolicyLabel);
        menus.Navigate(MenuInput.Down);
        menus.Navigate(MenuInput.Confirm);
        Assert.False(session.State.RandomEncounters!.Enabled);
        menus.Navigate(MenuInput.Up);
        menus.Navigate(MenuInput.Confirm);
        Assert.False(session.State.FullBattlePlaythrough == true);
        Assert.Equal("Instant victory", preferences.BattlePolicyLabel);
        Assert.Equal("debug-battle", menus.ScreenId);
    }

    private static string InstalledGame()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original resources are required for current-game policy checks.");
        return directory;
    }
}
