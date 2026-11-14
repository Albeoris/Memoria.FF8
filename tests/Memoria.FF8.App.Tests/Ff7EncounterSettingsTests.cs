using System.Reflection;
using System.Runtime.CompilerServices;
using Memoria.FF7.Runtime;
using Memoria.FF8.GameUi;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7EncounterSettingsTests
{
    [Fact]
    public void CombatPageExposesOnlyAtbAndSavedEncounterPolicies()
    {
        MenuController menu = new(() => new MenuData());
        menu.ConnectPreferences(() => new UiPreferences(), _ => { });
        Ff7Scene.RegisterPlayerCombatSettings(menu);
        menu.Open("config-combat");
        Assert.Equal(new[] { "Battle ATB mode", "Battle policy", "Random encounters" }, menu.Entries.Select(entry => entry.Id));
    }

    [Fact]
    public void PolicyDisplayReadsRestoredProgressWithoutWritingSharedDefaults()
    {
        UiPreferences defaults = new() { RandomEncounters = true, BattlePolicy = "NewGameFullCombat", WorldMovementSpeedPercent = 180 };
        Ff7Progress saved = new() { InitialKernelData = [], Characters = [], Party = [], Inventory = [], Materia = [], FullCombatEnabled = false, RandomEncountersEnabled = false };
        Ff7Scene scene = CreateScene(defaults, saved, out Ff7Session session, out Func<int> writes);
        Assert.False(scene.PresentationPreferences.RandomEncounters);
        Assert.Equal("ResearchInstantVictory", scene.PresentationPreferences.BattlePolicy);
        Assert.Equal(180, scene.PresentationPreferences.WorldMovementSpeedPercent);
        Assert.Same(saved, session.Progress);
        Assert.True(defaults.RandomEncounters);
        Assert.Equal("NewGameFullCombat", defaults.BattlePolicy);
        Assert.Equal(0, writes());
    }

    [Fact]
    public void MenuActionsChangeOnlyTheSelectedSavedPolicyAndAcceptBothDirections()
    {
        Ff7Progress saved = new() { InitialKernelData = [], Characters = [], Party = [], Inventory = [], Materia = [], FullCombatEnabled = true, RandomEncountersEnabled = true, Gil = 1234 };
        Ff7Scene scene = CreateScene(new UiPreferences(), saved, out Ff7Session session, out Func<int> writes);
        Apply(scene, new("config", "Random encounters", Value: "Off"));
        Assert.Equal(saved with { RandomEncountersEnabled = false }, session.Progress);
        Apply(scene, new("config", "Battle policy", Value: "Instant victory"));
        Assert.Equal(saved with { RandomEncountersEnabled = false, FullCombatEnabled = false }, session.Progress);
        Assert.False(scene.PresentationPreferences.RandomEncounters);
        Assert.Equal("Instant victory", scene.PresentationPreferences.BattlePolicyLabel);
        Apply(scene, new("config", "Random encounters", Value: "On"));
        Apply(scene, new("config", "Battle policy", Value: "Full combat"));
        Assert.Equal(saved, session.Progress);
        Assert.Equal(0, writes());
    }

    private static void Apply(Ff7Scene scene, MenuAction action)
    {
        typeof(Ff7Scene).GetMethod("ApplyPlayerMenuAction", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(scene, [action]);
    }

    private static Ff7Scene CreateScene(UiPreferences preferences, Ff7Progress progress, out Ff7Session session, out Func<int> writes)
    {
        Ff7Scene scene = (Ff7Scene)RuntimeHelpers.GetUninitializedObject(typeof(Ff7Scene));
        session = (Ff7Session)RuntimeHelpers.GetUninitializedObject(typeof(Ff7Session));
        typeof(Ff7Session).GetProperty(nameof(Ff7Session.Progress))!.SetValue(session, progress);
        typeof(Ff7Scene).GetProperty(nameof(Ff7Scene.Session))!.SetValue(scene, session);
        typeof(Ff7Scene).GetField("envelope", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(scene, new CombinedSessionSave());
        typeof(Ff7Scene).GetField("dialoguePreferences", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(scene, (Func<UiPreferences>)(() => preferences));
        int count = 0;
        typeof(Ff7Scene).GetField("setDialoguePreferences", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(scene, (Action<UiPreferences>)(_ => count++));
        writes = () => count;
        return scene;
    }
}
