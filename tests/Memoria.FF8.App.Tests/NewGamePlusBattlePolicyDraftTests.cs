using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NewGamePlusBattlePolicyDraftTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void StartAppliesSelectedCombatAndEncounterPolicyWithFreshCounters(bool fullCombat, bool encounters)
    {
        GameState source = CompletedSaveFixture.State();
        UiPreferences settings = new() { BattlePolicy = fullCombat ? "NewGameFullCombat" : "ResearchInstantVictory", RandomEncounters = encounters };
        NewGamePlusDraft draft = new(source, source, settings) { ExperienceMultiplierTenths = 13, ApMultiplierTenths = 0 };
        GameState result = draft.ApplyTo(source);
        Assert.Equal(fullCombat, result.FullBattlePlaythrough);
        Assert.Equal(encounters, result.RandomEncounters!.Enabled);
        Assert.Equal(NativeRandomEncounterState.FromColdProcess(), result.RandomEncounters.Native);
        Assert.Null(result.RandomEncounters.Pending);
        Assert.Equal(fullCombat, result.NewGamePlus!.InitialFullBattlePlaythrough);
        Assert.Equal(encounters, result.NewGamePlus.InitialRandomEncountersEnabled);
        Assert.Equal(13, result.NewGamePlusRewards!.ExperienceTenths);
        Assert.Equal(0, result.NewGamePlusRewards.AbilityPointTenths);
    }
}
