using System.Text.Json;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class EncounterPreferenceTests
{
    [Fact]
    public void ConfigPreferenceAppliesToTheNextFullCombatNewGameAndLeavesTheRunningPlaythroughUntouched()
    {
        string game = InstalledGame();
        string folder = Path.Combine(Path.GetTempPath(), "Memoria-EncounterPreferences-" + Guid.NewGuid().ToString("N"));
        string preferencesPath = Path.Combine(folder, "ui-settings.json");
        UiPreferences preferences = new UiPreferences().WithSetting("Battle policy", "NewGameFullCombat").WithSetting("Random encounters", "Off");
        Assert.Empty(new UiPreferences { RandomEncounters = true }.CreateNewGameBattleInputs());
        GameSession current = GameSession.CreateColdProcess();
        current.StartNewGame();
        using GameWorld currentWorld = new(current, game, persistReplayJournal: false);
        foreach (BattleInput input in preferences.CreateNewGameBattleInputs())
        {
            currentWorld.ApplyBattleInput(input);
        }
        Assert.False(current.State.RandomEncounters!.Enabled);
        GameState before = current.State;
        try
        {
            MenuController menus = new(() =>
            {
                MenuData data = new();
                data.Settings["Random encounters"] = preferences.RandomEncounters ? "On" : "Off";
                data.SupportedActions.Add("config:Random encounters");
                return data;
            }, action =>
            {
                preferences = preferences.WithSetting(action.SubjectId, action.Value);
                preferences.Save(preferencesPath);
            });
            menus.Open("config");
            int row = menus.Entries.ToList().FindIndex(entry => entry.Id == "Random encounters");
            Assert.True(row >= 0);
            Assert.Contains("next", menus.Entries[row].Help, StringComparison.OrdinalIgnoreCase);
            for (int index = 0; index < row; index++)
            {
                menus.Navigate(MenuInput.Down);
            }
            menus.Navigate(MenuInput.Confirm);
            Assert.True(preferences.RandomEncounters);
            Assert.True(UiPreferences.Load(preferencesPath).RandomEncounters);
            Assert.Same(before, current.State);
            Assert.False(current.State.RandomEncounters.Enabled);
            GameSession next = GameSession.CreateColdProcess();
            next.StartNewGame();
            using GameWorld nextWorld = new(next, game, persistReplayJournal: false);
            foreach (BattleInput input in UiPreferences.Load(preferencesPath).CreateNewGameBattleInputs())
            {
                nextWorld.ApplyBattleInput(input);
            }
            Assert.True(next.State.FullBattlePlaythrough);
            Assert.True(next.State.RandomEncounters!.Enabled);
            Assert.False(current.State.RandomEncounters.Enabled);
        }
        finally
        {
            if (File.Exists(preferencesPath))
            {
                File.Delete(preferencesPath);
            }
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder);
            }
        }
    }

    [Fact]
    public void CurrentSaveDebugToggleAppearsImmediatelyAndItsJournaledPolicySurvivesSaveLoadAndColdRecreation()
    {
        string game = InstalledGame();
        UiPreferences preferences = new() { BattlePolicy = "NewGameFullCombat", RandomEncounters = true };
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using GameWorld world = new(session, game, persistReplayJournal: false);
        foreach (BattleInput input in preferences.CreateNewGameBattleInputs())
        {
            world.ApplyBattleInput(input);
        }
        BattleDefinition scenario = new() { Combatants = [new() { Id = "party-0", Name = "Squall", IsPlayer = true }, new() { Id = "enemy-0", Name = "Dummy" }] };
        List<BattleInput> toggles = [];
        BattleDebugController debug = new(scenario, new BattleDebugCatalog(), BattleAtbMode.Normal, preferences.BattlePolicy, false, (_, _) => { }, (_, _, _) => { }, () => { });
        debug.ConnectCurrentEncounters(() => session.State.RandomEncounters?.Enabled, enabled =>
        {
            BattleInput input = new(BattleInputKind.ConfigureEncounters, RandomEncounters: enabled);
            toggles.Add(input);
            world.ApplyBattleInput(input);
        });
        int row = debug.Rows.Select((entry, index) => (entry, index)).Single(pair => pair.entry.Label == "Current save random battles").index;
        Assert.True(session.State.RandomEncounters!.Enabled);
        long count = session.State.ReplayJournal!.EntryCount;
        debug.Select(row, true);
        Assert.False(session.State.RandomEncounters.Enabled);
        Assert.False(Assert.Single(toggles).RandomEncounters);
        Assert.Equal(count + 1, session.State.ReplayJournal.EntryCount);
        Assert.Equal(BattleInputKind.ConfigureEncounters, session.State.ReplayJournal.Endpoint!.BattleInput!.Kind);
        Assert.False(session.State.ReplayJournal.Endpoint.BattleInput.RandomEncounters);
        Assert.True(preferences.RandomEncounters);
        session.SetEventState(world.Events.Capture());
        string path = Path.Combine(Path.GetTempPath(), "Memoria-EncounterToggle-" + Guid.NewGuid().ToString("N") + ".json");
        bool verified = false;
        try
        {
            session.Save(path, registerJournalReference: false);
            GameState loaded = GameSession.Load(path);
            Assert.False(loaded.RandomEncounters!.Enabled);
            GameSession restored = new(loaded);
            using GameWorld restoredWorld = new(restored, game, persistReplayJournal: false);
            restoredWorld.EnsureField();
            Assert.False(restored.State.RandomEncounters!.Enabled);
            GameState recreated = GameWorld.RecreateSave(GameSession.LoadReplaySource(path), game);
            Assert.False(recreated.RandomEncounters!.Enabled);
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(loaded.RandomEncounters), JsonSerializer.SerializeToElement(recreated.RandomEncounters)));
            verified = true;
        }
        finally
        {
            if (verified)
            {
                File.Delete(path);
            }
        }
    }

    private static string InstalledGame()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original opening resources are required for playthrough policy checks.");
        return directory;
    }
}
