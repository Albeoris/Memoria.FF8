using Bitmap = SkiaSharp.SKBitmap;
using Color = SkiaSharp.SKColor;

using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class UiTextOverflowRenderingTests
{
    private static readonly UiColor White = new(255, 255, 255);
    private static readonly UiColor Black = new(0, 0, 0);
    private static readonly UiRect Surface = new(0, 0, 340, 240);

    [Theory]
    [InlineData(24, 100, false)]
    [InlineData(48, 150, true)]
    public void ControlsKeyboardMappingHelpOnlyScrollsWhenItExceedsItsActualMenuPanel(int fontSize, int scale, bool overflows)
    {
        MenuController menus = new();
        menus.ConnectPreferences(() => new UiPreferences { UiFontSize = fontSize, TextScale = scale }, _ => { });
        menus.Open("config-controls", true);
        menus.Data.Settings["Text scale"] = scale + "%";
        while (menus.SelectedEntry!.Id != "config-keyboard")
        {
            menus.Navigate(MenuInput.Down);
        }
        UiView view = menus.BuildView();
        view.Resize(960, 672);
        UiPanel help = Assert.Single(Descendants(view.Root).OfType<UiPanel>(), panel => panel.Caption == "HELP");
        UiLabel label = Assert.Single(help.Children.OfType<UiLabel>());
        Assert.Equal("Assign keyboard actions and combinations. Bindings are saved globally for every game save.", label.Text);
        using Bitmap wrapped = SystemTextLayout.Rasterize(label.Text, label.FontSize, label.Bounds.Width);
        Assert.Equal(overflows, wrapped.Height / (float)SystemTextLayout.RasterScale > label.Bounds.Height);
        using Bitmap bitmap = new(960, 672);
        using BattlePreviewCanvas canvas = new(bitmap, "");
        UiTheme theme = new();
        int[] Render(double seconds)
        {
            canvas.BeginFrame(TimeSpan.FromSeconds(seconds));
            canvas.Fill(new UiRect(0, 0, bitmap.Width, bitmap.Height), Black);
            help.Draw(canvas, theme);
            theme.Caption(canvas, help.Bounds, help.Caption, help.CaptionFontSize);
            return Pixels(bitmap);
        }

        int[] beginning = Render(0);
        Save(bitmap, "controls-keyboard-help-start");
        int[] moving = Render(3);
        Assert.Equal(!overflows, beginning.SequenceEqual(moving));
        Save(bitmap, "controls-keyboard-help-moving");
    }

    [Fact]
    public void LargeHelpLineShowsBeginningMovesAndPausesWithItsEndInsideNestedClip()
    {
        const string text = "HELP: Select a character to inspect the available junction abilities.";
        const float size = 40;
        UiRect bounds = new(20, 20, 260, 50);
        UiRect parentClip = new(30, 10, 240, 100);
        using Bitmap unwrapped = SystemTextLayout.Rasterize(text, size, float.PositiveInfinity);
        float width = unwrapped.Width / (float)SystemTextLayout.RasterScale;
        float distance = width - bounds.Width;
        Assert.True(distance > bounds.Width);
        using Bitmap bitmap = new((int)Surface.Width, (int)Surface.Height);
        using BattlePreviewCanvas canvas = new(bitmap, "");

        int[] beginning = DrawFrame(canvas, bitmap, text, size, bounds, parentClip, 0);
        Assert.Equal(Reference(text, size, bounds, parentClip, width + 20, bounds.Height, 0, 0), beginning);
        Assert.Contains(beginning, pixel => (pixel & 0xFFFFFF) != 0);
        Save(bitmap, "help-line-start");
        int[] moving = DrawFrame(canvas, bitmap, text, size, bounds, parentClip, 2.5);
        Assert.False(beginning.SequenceEqual(moving));
        Save(bitmap, "help-line-moving");
        double endTime = 1.5 + distance / (size * 1.5) + 0.25;
        int[] ending = DrawFrame(canvas, bitmap, text, size, bounds, parentClip, endTime);
        Assert.Equal(Reference(text, size, bounds, parentClip, width + 20, bounds.Height, distance, 0), ending);
        Assert.Equal(ending, DrawFrame(canvas, bitmap, text, size, bounds, parentClip, endTime + 1));
        Save(bitmap, "help-line-end");
        UiRect visible = bounds.Intersect(parentClip);
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (x < visible.X || x >= visible.Right || y < visible.Y || y >= visible.Bottom)
                {
                    Assert.Equal(SkiaSharp.SKColors.Black.ToArgb(), bitmap.GetPixel(x, y).ToArgb());
                }
            }
        }
    }

    [Fact]
    public void WrappedHelpThatFitsStaysStillAcrossPresentationFrames()
    {
        const string text = "Wrapped help remains readable across several lines.";
        const float size = 32;
        UiRect bounds = new(20, 20, 260, 200);
        using Bitmap wrapped = SystemTextLayout.Rasterize(text, size, bounds.Width);
        using Bitmap singleLine = SystemTextLayout.Rasterize("Wrapped", size, float.PositiveInfinity);
        Assert.True(wrapped.Height > singleLine.Height);
        Assert.True(wrapped.Height / (float)SystemTextLayout.RasterScale < bounds.Height);
        using Bitmap bitmap = new((int)Surface.Width, (int)Surface.Height);
        using BattlePreviewCanvas canvas = new(bitmap, "");

        int[] beginning = DrawFrame(canvas, bitmap, text, size, bounds, Surface, 0, UiAlignment.Center);
        Assert.Contains(beginning, pixel => (pixel & 0xFFFFFF) != 0);
        Assert.Equal(beginning, DrawFrame(canvas, bitmap, text, size, bounds, Surface, 3, UiAlignment.Center));
        Assert.Equal(beginning, DrawFrame(canvas, bitmap, text, size, bounds, Surface, 20, UiAlignment.Center));
        Save(bitmap, "help-wrapped-fitting");
    }

    [Fact]
    public void CenteredMultilineHelpStartsAtTopAndEventuallyShowsLastLine()
    {
        const string text = "Choose a block.\nConfirm the selected destination.\nCancel returns to the previous menu.";
        const float size = 32;
        UiRect bounds = new(20, 20, 260, 65);
        using Bitmap wrapped = SystemTextLayout.Rasterize(text, size, bounds.Width);
        float height = wrapped.Height / (float)SystemTextLayout.RasterScale;
        float distance = height - bounds.Height;
        Assert.True(distance > bounds.Height);
        using Bitmap bitmap = new((int)Surface.Width, (int)Surface.Height);
        using BattlePreviewCanvas canvas = new(bitmap, "");

        int[] beginning = DrawFrame(canvas, bitmap, text, size, bounds, Surface, 0, UiAlignment.Center);
        Assert.Equal(Reference(text, size, bounds, Surface, bounds.Width, height + 20, 0, 0), beginning);
        Save(bitmap, "help-multiline-start");
        double endTime = 1.5 + distance / (size * 1.5) + 0.25;
        int[] ending = DrawFrame(canvas, bitmap, text, size, bounds, Surface, endTime, UiAlignment.Center);
        Assert.False(beginning.SequenceEqual(ending));
        Assert.Equal(Reference(text, size, bounds, Surface, bounds.Width, height + 20, 0, distance), ending);
        Assert.Contains(ending, pixel => (pixel & 0xFFFFFF) != 0);
        Save(bitmap, "help-multiline-end");
    }

    private static int[] DrawFrame(BattlePreviewCanvas canvas, Bitmap bitmap, string text, float size, UiRect bounds, UiRect clip, double seconds, UiAlignment alignment = UiAlignment.Start)
    {
        canvas.BeginFrame(TimeSpan.FromSeconds(seconds));
        canvas.Fill(Surface, Black);
        canvas.PushClip(clip);
        try
        {
            canvas.Text(text, size, bounds, White, alignment);
        }
        finally
        {
            canvas.PopClip();
        }
        return Pixels(bitmap);
    }

    private static int[] Reference(string text, float size, UiRect viewport, UiRect clip, float width, float height, float offsetX, float offsetY)
    {
        using Bitmap bitmap = new((int)Surface.Width, (int)Surface.Height);
        using BattlePreviewCanvas canvas = new(bitmap, "");
        canvas.PushClip(viewport);
        try
        {
            return DrawFrame(canvas, bitmap, text, size, new UiRect(viewport.X - offsetX, viewport.Y - offsetY, width, height), clip, 0);
        }
        finally
        {
            canvas.PopClip();
        }
    }

    private static int[] Pixels(Bitmap bitmap)
    {
        int[] pixels = new int[bitmap.Width * bitmap.Height];
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                pixels[y * bitmap.Width + x] = bitmap.GetPixel(x, y).ToArgb();
            }
        }
        return pixels;
    }

    private static IEnumerable<UiElement> Descendants(UiElement element)
    {
        yield return element;
        foreach (UiElement child in element.Children)
        {
            foreach (UiElement descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static void Save(Bitmap bitmap, string name)
    {
        if (Environment.GetEnvironmentVariable("MEMORIA_FF8_TEXT_OVERFLOW_OUTPUT") is { Length: > 0 } directory)
        {
            Directory.CreateDirectory(directory);
            bitmap.SavePng(Path.Combine(directory, name + ".png"));
        }
    }
}
