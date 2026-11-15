using Memoria.FF7.Runtime;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7NumericWindowViewportTests
{
    [Fact]
    public void BottomRatingDigitsUseTheRelocatedPanelAndRemainFullyInsideItsAdaptiveClip()
    {
        Ff7Window window = new(2, 159, 217, 155, 51, 1, 1) { SpecialType = 2, SpecialX = 101, SpecialY = 14, NativeNumber = 33, NativeNumberDigits = 2 };
        PixelBounds viewport = ViewportLayout.Fit(320, 240, 3440, 1334);
        Rectangle bounds = new(viewport.X, viewport.Y, viewport.Width, viewport.Height);
        float scale = viewport.Width / 320f;
        Vector2 measured = Ff7NumberWindowPresentation.IncludeContent(window, new(window.Width - 16, 12), 100);
        Assert.Equal(19, measured.Y);
        DialogueState state = new(2, 0, "Ratings %", window.X, window.Y);
        DialogueWindow native = new(2, window.X, window.Y, window.Width, window.Height);
        PixelBounds panel = DialoguePresentation.Bounds(state, native, viewport, measured.X * scale, measured.Y * scale);
        Rectangle clip = Ff7DialoguePresentationPolicy.Clip(new UiRect(panel.X, panel.Y, panel.Width, panel.Height), bounds);
        Ff7SpecialWindowView view = new(new(panel.X, panel.Y), new(scale), clip);
        IReadOnlyList<Ff7SpecialNumberSprite> sprites = Ff7NumberWindowPresentation.Place(window, view, 100);
        Assert.Equal(2, sprites.Count);
        Assert.Equal(panel.X + 101 * scale, sprites[0].Position.X);
        Assert.Equal(panel.Y + 14 * scale, sprites[0].Position.Y);
        Assert.All(sprites, sprite =>
        {
            Assert.Equal(32, sprite.Source.Width);
            Assert.Equal(42, sprite.Source.Height);
            Assert.InRange(sprite.Position.Y, clip.Top, clip.Bottom);
            Assert.True(sprite.Position.Y + sprite.Source.Height * sprite.Scale.Y <= clip.Bottom + 0.01f);
        });
        Ff7SpecialWindowView rawWindow = view with { Origin = new(bounds.X + window.X * scale, bounds.Y + window.Y * scale) };
        Assert.Empty(Ff7NumberWindowPresentation.Place(window, rawWindow, 100));
    }
}
