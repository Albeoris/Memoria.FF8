namespace Memoria.FF8.Assets.Tests;

using Memoria.FF8.Runtime;

public sealed class ExpandedAiInstalledAdmissionTests
{
    [Theory]
    [InlineData(89, 18, ExpandedAiAdmission.Supported)]
    [InlineData(236, 56, ExpandedAiAdmission.RequiresOriginalSlots)]
    [InlineData(237, 56, ExpandedAiAdmission.RequiresOriginalSlots)]
    [InlineData(344, 95, ExpandedAiAdmission.Supported)]
    [InlineData(371, 95, ExpandedAiAdmission.Supported)]
    [InlineData(448, 95, ExpandedAiAdmission.Supported)]
    [InlineData(478, 95, ExpandedAiAdmission.Supported)]
    [InlineData(0, 71, ExpandedAiAdmission.RequiresUnsupportedService)]
    public void InstalledScriptsUseFormationConditionsAndPreserveAllocationBoundaries(int formation, int model, ExpandedAiAdmission expected)
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        Assert.Contains(assets.LoadEncounter(formation).Enemies, enemy => enemy.ModelId == model);
        NativeBattleAiDefinition program = assets.ReadEnemyAi(model, 8);
        Assert.Equal(expected, NativeBattleExpandedAi.Evaluate(program, assets.NativeActions, formation));
    }
}
