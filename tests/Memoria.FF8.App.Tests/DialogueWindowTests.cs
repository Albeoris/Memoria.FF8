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
        Assert.Equal(new PixelBounds(208, 60, 168, 108), firstBounds);
        Assert.Equal(new PixelBounds(640, 360, 168, 108), secondBounds);
        Assert.NotEqual(firstBounds, secondBounds);
        PixelBounds clamped = DialoguePresentation.Bounds(second with { X = 310, Y = 230 }, null, viewport, 120, 60);
        Assert.Equal(new PixelBounds(928, 564, 168, 108), clamped);
        PixelBounds explicitWindow = DialoguePresentation.Bounds(first, new DialogueWindow(0, 40, 70, 100, 50), viewport, 1, 1);
        Assert.Equal(new PixelBounds(280, 210, 300, 150), explicitWindow);
    }

    [Theory]
    [InlineData(557, 370, "Quistis\n“Then let’s get\ngoing.”")]
    [InlineData(1280, 720, "Choose:\n> Yes\n  No\nArrows: select   Enter: confirm")]
    [InlineData(961, 721, "Name:\nSquall_  (Enter: accept)")]
    [InlineData(557, 370, "École\n\ngyp\nДлинноесловобезпробелов")]
    public void Undersized_script_window_grows_to_the_visible_text_with_equal_vertical_padding(int width, int height, string text)
    {
        PixelBounds viewport = ViewportLayout.Fit(320, 240, width, height);
        float scale = viewport.Width / 320f;
        using var raster = SystemTextLayout.Rasterize(text, 8 * scale, 84 * scale);
        float textWidth = raster.Width / (float)SystemTextLayout.RasterScale;
        float textHeight = raster.Height / (float)SystemTextLayout.RasterScale;
        PixelBounds panel = DialoguePresentation.Bounds(new DialogueState(0, 10, text, 0, 0), new DialogueWindow(0, 200, 200, 100, 20), viewport, textWidth, textHeight);
        float top = (panel.Height - textHeight) / 2;
        float bottom = panel.Height - top - textHeight;
        Assert.Equal(top, bottom);
        Assert.True(top >= DialoguePresentation.Padding * scale);
        Assert.True(panel.Width >= textWidth + 2 * DialoguePresentation.Padding * scale);
        Assert.True(panel.Y + panel.Height <= viewport.Y + 224 * scale + 1);
    }

    [Fact]
    public void Taller_script_window_keeps_its_height_and_centers_the_visible_text()
    {
        PixelBounds panel = DialoguePresentation.Bounds(new DialogueState(0, 10, "gyp", 0, 0), new DialogueWindow(0, 40, 70, 100, 80), new PixelBounds(0, 0, 320, 240), 30, 12);
        Assert.Equal(80, panel.Height);
        Assert.Equal(34, (panel.Height - 12) / 2);
    }

    [Fact]
    public void Arial_raster_has_cropped_visible_bounds_and_premultiplied_antialiased_edges()
    {
        using var raster = SystemTextLayout.Rasterize("École\n\ngyp", 14.5f, 200);
        var pixels = SystemTextLayout.PremultipliedPixels(raster);
        Assert.Contains(pixels.Take(raster.Width), pixel => pixel.A != 0);
        Assert.Contains(pixels.TakeLast(raster.Width), pixel => pixel.A != 0);
        Assert.Contains(pixels, pixel => pixel.A > 0 && pixel.A < 255);
        Assert.All(pixels, pixel =>
        {
            Assert.Equal(pixel.A, pixel.R);
            Assert.Equal(pixel.A, pixel.G);
            Assert.Equal(pixel.A, pixel.B);
        });
        using var withoutBlank = SystemTextLayout.Rasterize("École\ngyp", 14.5f, 200);
        Assert.True(raster.Height > withoutBlank.Height);
        using var longWord = SystemTextLayout.Rasterize("Длинноесловобезпробелов", 14.5f, 45);
        Assert.True(longWord.Width / (float)SystemTextLayout.RasterScale <= 45);
        Assert.True(longWord.Height > withoutBlank.Height);
    }
}
