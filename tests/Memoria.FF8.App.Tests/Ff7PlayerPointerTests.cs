using Memoria.FF7.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7PlayerPointerTests
{
    [Theory]
    [InlineData(0, false, false)]
    [InlineData(1, false, true)]
    [InlineData(1, true, false)]
    [InlineData(2, true, true)]
    [InlineData(3, true, true)]
    [InlineData(4, false, false)]
    public void NativeHelpModeOwnsThePlayerLockGate(byte mode, bool locked, bool requested)
    {
        Ff7FieldProcess process = new() { PlayerLocked = locked };
        Assert.Equal(requested, Ff7PlayerPointer.IsRequested(mode, process));
        Assert.False(Ff7PlayerPointer.IsRequested(mode, process with { CursorDisabled = true }));
    }

    [Theory]
    [InlineData(160, 100, 160, 101, false, false)]
    [InlineData(290, 17, 290, 18, false, false)]
    [InlineData(291, 16, 276, 32, true, true)]
    [InlineData(500, 500, 305, 225, true, false)]
    [InlineData(-100, -100, 0, -16, false, true)]
    public void PointerUsesNativeEdgeClampAndMirroring(int x, int y, int expectedX, int expectedY, bool flipX, bool flipY)
    {
        Assert.Equal(new Ff7PlayerPointerBounds(expectedX, expectedY, flipX, flipY), Ff7PlayerPointer.Place(x, y));
    }

    [Fact]
    public void UnsetAndTooDistantPointerCoordinatesHaveNoPresentation()
    {
        Assert.Null(Ff7PlayerPointer.Place(short.MaxValue, short.MaxValue));
        Assert.Null(Ff7PlayerPointer.Place(-101, 100));
        Assert.Null(Ff7PlayerPointer.Place(100, -101));
    }
}
