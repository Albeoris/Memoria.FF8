using Bitmap = SkiaSharp.SKBitmap;
using Color = SkiaSharp.SKColor;

namespace Memoria.FF8.App.Tests;

public sealed class TextRunSpacingTests
{
    [Theory]
    [InlineData(" follows")]
    [InlineData("  follows")]
    [InlineData("  follows  text ")]
    [InlineData("   ")]
    [InlineData(" first\n  second\n\n third")]
    public void Wrapping_preserves_authored_spaces_when_lines_fit(string text)
    {
        string[] expected = text.Split('\n');
        Assert.Equal(expected, SystemTextLayout.Wrap(text, value => value.Length, float.PositiveInfinity));
        Assert.Equal(expected, SystemTextLayout.Wrap(text, value => value.Length, 40));
    }

    [Fact]
    public void Soft_wrapping_preserves_initial_indentation_and_consumes_the_word_separator()
    {
        Assert.Equal(new[] { "  word", "tail" }, SystemTextLayout.Wrap("  word tail", value => value.Length, 6));
    }

    [Fact]
    public void Color_boundaries_retain_the_space_at_the_start_of_the_next_run()
    {
        DialogueLine source = Assert.Single(Assert.Single(DialogueTextFormatter.Parse("{Color Green}Term{Color White} follows").Pages).Lines);
        DialogueVisualLine line = Assert.Single(DialogueLayout.Wrap(source, 200, value => value.Length, false, 10));
        Assert.Equal(new[] { "Term", " follows" }, line.Runs.Select(run => run.Run.Text));
        Assert.Equal(4, line.Runs[1].X);
        Assert.Equal(new[] { " follows" }, SystemTextLayout.Wrap(line.Runs[1].Run.Text, value => value.Length, float.PositiveInfinity));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Rasterization_retains_leading_run_space_in_both_canvas_and_dialogue_modes(bool preserveLineMetrics)
    {
        const float size = 20;
        const string family = "Arial";
        using Bitmap plain = SystemTextLayout.Rasterize("follows", size, float.PositiveInfinity, family, preserveLineMetrics);
        using Bitmap spaced = SystemTextLayout.Rasterize(" follows", size, float.PositiveInfinity, family, preserveLineMetrics);
        float expected = SystemTextLayout.MeasureAdvance(" ", size, family) * SystemTextLayout.RasterScale;
        Assert.InRange(FirstInkColumn(spaced) - FirstInkColumn(plain), expected - 1, expected + 1);
        Assert.InRange(spaced.Width - plain.Width, expected - 1, expected + 1);
    }

    [Fact]
    public void Cropped_multiline_raster_preserves_common_indentation_through_explicit_blank_lines()
    {
        using Bitmap plain = SystemTextLayout.Rasterize("follows\n\nfollows", 20, float.PositiveInfinity, "Arial");
        using Bitmap spaced = SystemTextLayout.Rasterize("  follows\n\n  follows", 20, float.PositiveInfinity, "Arial");
        float expected = SystemTextLayout.MeasureAdvance("  ", 20, "Arial") * SystemTextLayout.RasterScale;
        Assert.InRange(FirstInkColumn(spaced) - FirstInkColumn(plain), expected - 1, expected + 1);
    }

    private static int FirstInkColumn(Bitmap bitmap)
    {
        var pixels = SystemTextLayout.PremultipliedPixels(bitmap);
        return Enumerable.Range(0, bitmap.Width).First(x => Enumerable.Range(0, bitmap.Height).Any(y => pixels[y * bitmap.Width + x].A > 0));
    }
}
