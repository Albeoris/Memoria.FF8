namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePandemona291BoundsTests
{
    [Fact]
    public void BoundsUseSignedIr2ForEverySelectedVertexWithoutScreenOrDepthFiltering()
    {
        NativeBattleRawMesh selected = new([new(new(1, 2, 3), 0), new(new(4, 5, 6), 1)], []);
        NativeBattleRawMesh skipped = new([new(new(7, 8, 9), 2)], []);
        List<ushort> visited = [];
        NativeBattlePandemona291Bounds result = NativeBattlePandemona291ActorBounds.Capture6ED770([selected, skipped], 1, 3, vertex =>
        {
            visited.Add(vertex.Bone);
            return vertex.Bone == 0 ? (short)-30000 : (short)31000;
        });
        Assert.Equal(new ushort[] { 0, 1 }, visited);
        Assert.Equal(new NativeBattlePandemona291Bounds(31000, -30000), result);
    }

    [Fact]
    public void Model8EOverridesMaximumAfterAllProjectionSideEffectsAndEmptySelectionKeepsNativeWordResults()
    {
        int count = 0;
        NativeBattlePandemona291Bounds result = NativeBattlePandemona291ActorBounds.Capture6ED770([new([new(new(0, 0, 0), 0)], [])], 1, 0x8E, _ =>
        {
            count++;
            return 1234;
        });
        Assert.Equal(1, count);
        Assert.Equal(new NativeBattlePandemona291Bounds(0, 1234), result);
        Assert.Equal(new NativeBattlePandemona291Bounds(0, 0), NativeBattlePandemona291ActorBounds.Capture6ED770([], 0, 1, _ => throw new InvalidOperationException()));
    }
}
