namespace Memoria.FF8.Runtime.Tests;

public sealed class ExpandedBattleFormationTests
{
    [Theory]
    [InlineData(4, 4, 0, 0)]
    [InlineData(5, 4, 1, 0)]
    [InlineData(6, 4, 2, 0)]
    [InlineData(8, 4, 3, 1)]
    [InlineData(10, 4, 3, 3)]
    public void MiddleThenRearThenFrontUseDistinctMirroredPositions(int count, int middle, int rear, int front)
    {
        NativeBattleVector[] allies = Enumerable.Range(0, count).Select(slot => ExpandedBattleFormation.Position(slot, count, isAlly: true)).ToArray();
        NativeBattleVector[] enemies = Enumerable.Range(0, count).Select(slot => ExpandedBattleFormation.Position(slot, count, isAlly: false)).ToArray();

        Assert.Equal(count, allies.Distinct().Count());
        Assert.Equal(allies.Select(position => new NativeBattleVector(position.X, position.Y, checked((short)-position.Z))), enemies);
        Assert.Equal(middle, allies.Count(position => position.Z == 1800));
        Assert.Equal(rear, allies.Count(position => position.Z == 2700));
        Assert.Equal(front, allies.Count(position => position.Z == 900));
        Assert.Equal(new short[] { -1800, -600, 600, 1800 }, allies.Take(4).Select(position => position.X));
        Assert.Equal(new short[] { -1200, 0, 1200 }.Take(rear), allies.Skip(4).Take(rear).Select(position => position.X));
        Assert.Equal(new short[] { -1200, 0, 1200 }.Take(front), allies.Skip(7).Select(position => position.X));
    }
}
