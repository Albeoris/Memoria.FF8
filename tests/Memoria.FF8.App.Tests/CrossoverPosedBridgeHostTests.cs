namespace Memoria.FF8.App.Tests;

public sealed class CrossoverPosedBridgeHostTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("Water", 1)]
    [InlineData("Water", 2)]
    [InlineData("Water", 16)]
    [InlineData("Bio", 1)]
    [InlineData("Bio", 2)]
    [InlineData("Bio", 16)]
    [InlineData("Flare", 1)]
    [InlineData("Flare", 2)]
    [InlineData("Flare", 16)]
    public void OriginalPosedEffectsCompleteAgainstTheCasterNativeAllyAndNativeEnemy(string name, ushort targets)
    {
        new CrossoverUniversalAbilityHostTests(output).Run(name, targets);
    }

    [Theory]
    [InlineData("Water")]
    [InlineData("Bio")]
    [InlineData("Flare")]
    public void OriginalPosedEffectsRespectTheirNativeEnemyGroupSelectionContract(string name)
    {
        new CrossoverUniversalAbilityHostTests(output).Run(name, 0x30);
    }
}
