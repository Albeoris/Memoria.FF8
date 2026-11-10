using Avalonia;
using Memoria.Launcher.Controller;

namespace Memoria.Launcher.Tests;

public sealed class SpatialNavigationTests
{
    [Theory]
    [InlineData((int)ControllerAction.Left, "left")]
    [InlineData((int)ControllerAction.Right, "right")]
    [InlineData((int)ControllerAction.Up, "above")]
    [InlineData((int)ControllerAction.Down, "below")]
    public void UsesTheRequestedDirection(int direction, string expected)
    {
        Rect origin = new(100, 100, 100, 30);
        (string, Rect)[] controls =
        [
            ("left", new Rect(0, 100, 90, 30)),
            ("right", new Rect(205, 100, 90, 30)),
            ("above", new Rect(105, 60, 180, 30)),
            ("below", new Rect(25, 145, 135, 30))
        ];

        Assert.Equal(expected, SpatialNavigation.FindNext(origin, controls, (ControllerAction)direction));
    }

    [Fact]
    public void PrefersAnAlignedControlOverACloserDiagonalControl()
    {
        Rect origin = new(100, 100, 90, 30);
        (string, Rect)[] controls =
        [
            ("diagonal", new Rect(0, 135, 85, 30)),
            ("aligned", new Rect(110, 175, 120, 30)),
            ("farther", new Rect(110, 225, 120, 30))
        ];

        Assert.Equal("aligned", SpatialNavigation.FindNext(origin, controls, ControllerAction.Down));
    }

    [Fact]
    public void StaysAtTheEdgeInsteadOfFollowingOffsetCenters()
    {
        Rect origin = new(100, 100, 90, 30);
        (string, Rect)[] controls =
        [
            ("wide control below", new Rect(100, 135, 220, 30)),
            ("wide control above", new Rect(90, 65, 220, 30))
        ];

        Assert.Null(SpatialNavigation.FindNext(origin, controls, ControllerAction.Right));
    }

    [Fact]
    public void UsesADiagonalNeighborWhenThereIsNoAlignedControl()
    {
        Rect origin = new(100, 100, 90, 30);
        (string, Rect)[] controls =
        [
            ("above", new Rect(130, 65, 90, 30)),
            ("diagonal", new Rect(220, 145, 110, 30))
        ];

        Assert.Equal("diagonal", SpatialNavigation.FindNext(origin, controls, ControllerAction.Down));
    }
}
