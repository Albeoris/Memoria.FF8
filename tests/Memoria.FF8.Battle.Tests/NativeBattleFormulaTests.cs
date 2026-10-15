using Memoria.FF8.Runtime;

namespace Memoria.FF8.Battle.Tests;

public sealed class NativeBattleFormulaTests
{
    [Fact]
    public void NativeSeedZeroInitializesAllEightIndexesAndSelectedLaneBeforeDraw()
    {
        NativeBattleRandomState random = NativeBattleRandomState.Initialize(0);
        Assert.Equal(new byte[] { 9, 99, 6, 240, 35, 248, 229, 168 }, random.Indexes);
        Assert.Equal(1, random.Lane);
        List<byte> output = [];
        for (int index = 0; index < 4; index++)
        {
            random = random.Draw(out byte value);
            output.Add(value);
        }
        Assert.Equal(new byte[] { 242, 24, 218, 135 }, output);
        Assert.Equal(103, random.Indexes[1]);
    }

    [Theory]
    [InlineData(20, 5, 20, 242, 54)]
    [InlineData(100, 100, 20, 0, 546)]
    public void NativePhysicalIntegerOrderRetainsIntermediateTruncation(int strength, int vitality, int power, int random, int expected)
    {
        Assert.Equal(expected, NativeBattleDamage.Physical(strength, vitality, power, random));
    }

    [Fact]
    public void NativeCureAndMagicUseDifferentFormulasAndEnemyMagicIsHalved()
    {
        Assert.Equal(248, NativeBattleDamage.Magic(30, 5, 20, 242, false));
        Assert.Equal(124, NativeBattleDamage.Magic(30, 5, 20, 242, true));
        Assert.Equal(490, NativeBattleDamage.Cure(30, 20, 242));
    }
}
