namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel215PlacementTests
{
    [Fact]
    public void PlacementRestartsAllTargetsAfterEachNative128Step()
    {
        NativeBattleDuel215TargetBounds[] targets = [new(128, -100, 100, 50), new(0, -100, 100, 50)];
        Assert.Equal(256, NativeBattleDuel215Controller.PlaceActor69D500(0, 100, 49, targets));
        Assert.Equal(0, NativeBattleDuel215Controller.PlaceActor69D500(0, 100, 50, targets));
    }

    [Fact]
    public void PlacementKeepsSignedHalfBoundsAndStrictTouchingEdges()
    {
        Assert.Equal(0, NativeBattleDuel215Controller.PlaceActor69D500(0, 100, 0, [new(100, -100, 100, 1)]));
        Assert.Equal(0, NativeBattleDuel215Controller.PlaceActor69D500(0, 100, 0, [new(-100, -100, 100, 1)]));
        Assert.Equal(256, NativeBattleDuel215Controller.PlaceActor69D500(0, 100, 0, [new(100, -101, 100, 1)]));
    }
}
