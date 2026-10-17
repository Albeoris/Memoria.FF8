using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class DialogueWindowTests
{
    [Fact]
    public void Lower_channel_prompt_covers_overlapping_persistent_hints()
    {
        DialogueState prompt = new(0, 1, "Question{Line}Yes{Line}No", 15, 145, FirstChoice: 1, LastChoice: 2);
        DialogueState hint = new(1, 2, "Hint", 15, 170, CanConfirm: false);
        DialogueState code = new(2, 3, "Code", 230, 80, CanConfirm: false);
        Assert.Equal(new[] { code, hint, prompt }, DialoguePresentation.DrawOrder([prompt, hint, code]));
        Assert.Equal(new[] { code, hint, prompt }, DialoguePresentation.DrawOrder([hint, code, prompt]));
    }

    [Fact]
    public void Native_channel_depth_is_independent_of_confirmation_state()
    {
        DialogueState foreground = new(0, 1, "Hint", 15, 145, CanConfirm: false);
        DialogueState background = new(2, 2, "Question", 15, 170);
        Assert.Equal(new[] { background, foreground }, DialoguePresentation.DrawOrder([foreground, background]));
        Assert.Empty(DialoguePresentation.DrawOrder([]));
        Assert.Equal(new[] { background }, DialoguePresentation.DrawOrder([background]));
    }

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
        Assert.Equal(new PixelBounds(280, 210, 49, 49), explicitWindow);
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

    [Theory]
    [InlineData(320, 240)]
    [InlineData(1012, 843)]
    [InlineData(1280, 720)]
    public void Corridor_MES_keeps_the_WINSIZE_origin_above_the_later_AMES(int width, int height)
    {
        PixelBounds viewport = ViewportLayout.Fit(320, 240, width, height);
        float scale = viewport.Width / 320f;
        var squall = new DialogueState(1, 11, "{Char Squall}{Line}”It’s none of your...“{End}", 0, 0);
        var quistis = new DialogueState(0, 12, "Quistis{Line}”Business!“{End}", 125, 200);
        using var measure = SystemTextLayout.CreateMeasurer(8, "Arial");
        var window = new DialogueWindow(1, 60, 200, 167, 87);
        DialogueVisualPage Page(DialogueState dialogue, DialogueWindow? scriptWindow) => Assert.Single(DialogueLayout.Create(DialogueTextFormatter.Parse(dialogue.Text), dialogue, scriptWindow, measure.LineHeight, measure.Measure));
        var first = Page(squall, window);
        var second = Page(quistis, null);
        PixelBounds squallBounds = DialoguePresentation.Bounds(squall, window, viewport, first.Width * scale, first.Height * scale);
        PixelBounds quistisBounds = DialoguePresentation.Bounds(quistis, null, viewport, second.Width * scale, second.Height * scale);
        Assert.Equal(viewport.X + (int)MathF.Round(60 * scale), squallBounds.X);
        Assert.Equal(viewport.Y + (int)MathF.Round(137 * scale), squallBounds.Y);
        Assert.True(squallBounds.Y + squallBounds.Height < quistisBounds.Y);
    }

    [Theory]
    [InlineData(250, 180, 100, 80, 212, 144)]
    [InlineData(204, 20, 100, 80, 212, 20)]
    [InlineData(0, 65535, 100, 80, 8, 8)]
    public void Script_rectangle_is_positioned_before_short_page_sizing(int x, int y, int width, int height, int expectedX, int expectedY)
    {
        PixelBounds panel = DialoguePresentation.Bounds(new DialogueState(1, 11, "Short", 0, 0), new DialogueWindow(1, x, y, width, height), new PixelBounds(0, 0, 320, 240), 30, 12);
        Assert.Equal(new PixelBounds(expectedX, expectedY, 46, 28), panel);
    }

    [Fact]
    public void Taller_script_window_shrinks_to_its_current_page_with_equal_padding()
    {
        PixelBounds panel = DialoguePresentation.Bounds(new DialogueState(0, 10, "gyp", 0, 0), new DialogueWindow(0, 40, 70, 100, 80), new PixelBounds(0, 0, 320, 240), 30, 12);
        Assert.Equal(28, panel.Height);
        Assert.Equal(8, (panel.Height - 12) / 2);
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
