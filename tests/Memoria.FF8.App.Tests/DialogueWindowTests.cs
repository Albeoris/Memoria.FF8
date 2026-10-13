using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class DialogueWindowTests
{
    [Fact]
    public void Independent_windows_follow_the_original_coordinates_inside_the_fitted_field()
    {
        PixelBounds viewport = ViewportLayout.Fit(320, 240, 1280, 720);
        DialogueState first = new(0, 10, "First", 16, 20);
        DialogueState second = new(1, 11, "Second", 160, 120);
        PixelBounds firstBounds = DialoguePresentation.Bounds(first, null, viewport, 120, 60);
        PixelBounds secondBounds = DialoguePresentation.Bounds(second, null, viewport, 120, 60);
        Assert.Equal(new PixelBounds(208, 60, 168, 111), firstBounds);
        Assert.Equal(new PixelBounds(640, 360, 168, 111), secondBounds);
        Assert.NotEqual(firstBounds, secondBounds);
        PixelBounds clamped = DialoguePresentation.Bounds(second with { X = 310, Y = 230 }, null, viewport, 120, 60);
        Assert.Equal(new PixelBounds(928, 561, 168, 111), clamped);
        PixelBounds explicitWindow = DialoguePresentation.Bounds(first, new DialogueWindow(0, 40, 70, 100, 50), viewport, 1, 1);
        Assert.Equal(new PixelBounds(280, 210, 300, 150), explicitWindow);
    }
}
