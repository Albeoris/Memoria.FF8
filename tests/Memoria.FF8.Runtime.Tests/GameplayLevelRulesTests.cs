namespace Memoria.FF8.Runtime.Tests;

public sealed class GameplayLevelRulesTests
{
    [Fact]
    public void HardcoreUsesFixedRulesOnlyOutsideNewGamePlus()
    {
        GameState state = new() { GameplayPreferences = new GameplayPreferences { Difficulty = BattleDifficulty.Hardcore }.NormalizeForDifficulty() };
        Assert.Equal(GameplayLevelRules.Hardcore, state.LevelRules);
        Assert.Equal(110, state.LevelRules.MaximumEnemyLevel);
        Assert.Equal(10, state.LevelRules.AdditionalPartyLevel);
        Assert.Equal(110, NativeBattleEnemyLevelRules.ResolveExpanded(254, [100], 0, null, state.LevelRules));

        state = state with { NewGamePlus = new NewGamePlusDefinition() };
        Assert.Equal(GameplayLevelRules.Default, state.LevelRules);
    }

    [Theory]
    [InlineData(80, 15, 0, 0, 15)]
    [InlineData(252, 15, 70, 0, 15)]
    [InlineData(251, 300, 1, 3, 65)]
    [InlineData(254, 300, 0, 0, 290)]
    [InlineData(255, 300, 1, 0, 300)]
    [InlineData(200, 300, 1, 0, 100)]
    [InlineData(215, 300, 0, 0, 247)]
    public void EncounterCodesKeepTheirOwnRulesWithinTheConfiguredMaximum(int code, int maximum, int first, int second, int expected)
    {
        GameplayLevelRules rules = new() { MaximumEnemyLevel = maximum, MaximumCharacterLevel = 300, AdditionalPartyLevel = 30 };
        Assert.Equal(expected, NativeBattleEnemyLevelRules.ResolveExpanded((byte)code, [255, 265], (byte)first, (byte)second, rules));
    }
}
