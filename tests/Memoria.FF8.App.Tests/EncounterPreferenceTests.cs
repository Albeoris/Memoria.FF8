using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class EncounterPreferenceTests
{
    [Fact]
    public void EnablingEncountersBeforeFullCombatOnTheWorldMapInitializesTheCurrentProcessClock()
    {
        GameSession initialized = GameSession.CreateColdProcess();
        GameSession session = new(initialized.State with
        {
            Scene = SceneKind.WorldMap,
            WorldArrival = new WorldTransition(0, 0, 0),
            WorldProcess = initialized.State.WorldProcess!.AdvanceMainLoopCallbacks(371)
        });
        using GameWorld world = new(session, InstalledGame());
        world.ApplyBattleInput(new BattleInput(BattleInputKind.ConfigureEncounters, RandomEncounters: true));
        Assert.True(session.State.RandomEncounters!.Enabled);
        Assert.False(session.State.RandomEncounters.Native.WorldInitialized);
        world.ApplyBattleInput(new BattleInput(BattleInputKind.ConfigurePlaythrough, FullCombat: true));
        Assert.True(session.State.RandomEncounters.Native.WorldInitialized);
        Assert.Equal((byte)115, session.State.RandomEncounters.Native.WorldFormationIndex);
        Assert.Equal((byte)115, session.State.RandomEncounters.Native.WorldFormationOffset);
        Assert.Equal(371u, session.State.WorldProcess!.MainLoopCounter);
    }

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
    public void BattlePolicyAndEncountersApplyThroughTheirCurrentMenuPagesWithoutOpeningLaboratory()
    {
        string game = InstalledGame();
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using GameWorld world = new(session, game);
        UiPreferences preferences = new();
        MenuData data = new();
        data.Settings["Random encounters"] = "On";
        data.SupportedActions.Add("config:Random encounters");
        void ApplyPreferences(MenuAction action)
        {
            preferences = preferences.WithSetting(action.SubjectId, action.Value);
            data.Settings[action.SubjectId] = action.Value;
            foreach (BattleInput input in preferences.CreateBattleSettingsInputs())
            {
                world.ApplyBattleInput(input);
            }
        }
        MenuController menus = new(() => data, ApplyPreferences);
        menus.ConnectPreferences(() => preferences, ApplyPreferences);
        menus.Open("debug-battle", true);
        menus.SelectEntry("debug-battle", "Battle policy");
        menus.Navigate(MenuInput.Confirm);
        Assert.True(session.State.FullBattlePlaythrough);
        Assert.Equal("Full combat", preferences.BattlePolicyLabel);
        Assert.DoesNotContain(menus.Entries, entry => entry.Id == "Random encounters");
        menus.Open("config-exploration");
        menus.SelectEntry("config-exploration", "Random encounters");
        menus.Navigate(MenuInput.Confirm);
        Assert.False(session.State.RandomEncounters!.Enabled);
        Assert.False(preferences.RandomEncounters);
        Assert.True(session.State.FullBattlePlaythrough);
        Assert.Equal("config-exploration", menus.ScreenId);
        menus.Open("debug-battle", true);
        menus.SelectEntry("debug-battle", "Battle policy");
        menus.Navigate(MenuInput.Confirm);
        Assert.False(session.State.FullBattlePlaythrough == true);
        Assert.Equal("Instant victory", preferences.BattlePolicyLabel);
        Assert.Equal("debug-battle", menus.ScreenId);
    }

    private static string InstalledGame()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original resources are required for current-game policy checks.");
        return directory;
    }
}
