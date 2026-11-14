namespace Memoria.FF8.Runtime.Tests;

public sealed class ExpandedAiReferenceInventoryTests
{
    [Fact]
    public void TypedInventoryRecordsOnlyEnemyModelsAndPreservesDiagnosticReferences()
    {
        NativeBattleAiDefinition program = Program([
            4, 0, 4, 16, 4, 199, 4, 200,
            2, 0, 5, 0, 0, 0, 0, 0,
            2, 0, 17, 0, 0, 0, 0, 0,
            2, 8, 0, 0, 18, 0, 0, 0,
            2, 9, 0, 0, 15, 0, 0, 0,
            2, 10, 203, 0, 19, 0, 0, 0,
            38, 1, 15, 0, 203, 38, 1, 20, 0, 203, 38, 1, 201, 0, 203, 0
        ]);
        List<string> diagnostics = [];
        HashSet<int> enemyModels = [];
        Assert.Equal(ExpandedAiAdmission.Supported, NativeBattleExpandedAi.Evaluate(program, [], 0, explicitModelReferences: diagnostics, explicitEnemyModelReferences: enemyModels));
        Assert.Equal(new[] { 16, 17, 18, 19, 20, 199 }, enemyModels.Order());
        Assert.Equal(new[]
        {
            "target-model:0", "target-model:16", "target-model:199", "condition-model:0/5", "condition-model:0/17",
            "condition-model:8/18", "condition-model:9/15", "condition-model:10/19", "target-filter-model:15", "target-filter-model:20"
        }, diagnostics);
    }

    [Fact]
    public void ReserveInventoryUsesDestinationAndBandBranchesAndIncludesInvalidReachedOperands()
    {
        NativeBattleAiDefinition program = Program([
            2, 3, 0, 0, 9, 0, 2, 0, 31, 2,
            2, 14, 0, 0, 2, 0, 3, 0, 59, 9, 4,
            0, 52, 7, 0
        ]);
        HashSet<int> reserves = [];
        Assert.Equal(ExpandedAiAdmission.Supported, NativeBattleExpandedAi.Evaluate(program, [], 8, originalEnemySlots: true, reserveSceneSlots: reserves));
        Assert.Empty(reserves);
        Assert.Equal(ExpandedAiAdmission.Supported, NativeBattleExpandedAi.Evaluate(program, [], 9, originalEnemySlots: true, reserveSceneSlots: reserves));
        Assert.Equal(2, Assert.Single(reserves));
        Assert.Equal(ExpandedAiAdmission.RequiresUnsupportedService, NativeBattleExpandedAi.Evaluate(program with { LevelBand = 2 }, [], 9, originalEnemySlots: true, reserveSceneSlots: reserves));
        Assert.Equal(new[] { 2, 9 }, reserves.Order());
    }

    private static NativeBattleAiDefinition Program(byte[] init)
    {
        return new([init, [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(0, 0, 0), 48).ToArray(), 0, []);
    }
}
