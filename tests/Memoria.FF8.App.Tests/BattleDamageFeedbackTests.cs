using Bitmap = SkiaSharp.SKBitmap;
using Color = SkiaSharp.SKColor;

using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class BattleDamageFeedbackTests
{
    [Fact]
    public void Damage_digits_use_outlined_system_text_at_native_fixed_advances()
    {
        RecordingCanvas canvas = new();
        BattleDamageTextPresentation.Draw([new(148, 88, 0x808080, [57, 58, 59, 0]), new(32, 50, 0x04408040, [60, 61, 0])], canvas, new(20, 30, 640, 480));
        Assert.Equal(new[] { "1", "2", "3", "4", "5" }, canvas.Draws.Select(draw => draw.Text));
        Assert.Equal(new float[] { 316, 332, 348, 84, 100 }, canvas.Draws.Select(draw => draw.Bounds.X));
        Assert.All(canvas.Draws, draw => Assert.Equal(new UiColor(0, 0, 0, draw.Color.A), draw.Outline));
        Assert.Equal(new UiColor(255, 255, 255), canvas.Draws[0].Color);
        Assert.Equal(new UiColor(128, 255, 128, 128), canvas.Draws[3].Color);
    }

    [Fact]
    public void Miss_and_double_triple_announcements_preserve_the_original_labels_and_offsets()
    {
        RecordingCanvas canvas = new();
        BattleDamageTextPresentation.Draw([new(152, 88, 0x808080, [16, 0]), new(160, 88, 0x808080, [69, 0])], canvas, new(0, 0, 320, 240));
        Assert.Equal(new[] { "Miss", "TRIPLE!!", "DOUBLE!" }, canvas.Draws.Select(draw => draw.Text));
        Assert.Equal(new float[] { 144, 136, 136 }, canvas.Draws.Select(draw => draw.Bounds.X));
        Assert.Equal(new float[] { 88, 88, 76 }, canvas.Draws.Select(draw => draw.Bounds.Y));
    }

    private sealed class RecordingCanvas : IUiCanvas
    {
        public List<(string Text, UiRect Bounds, UiColor Color, UiColor Outline)> Draws { get; } = [];

        public void OutlinedText(string text, float fontSize, UiRect bounds, UiColor color, UiColor outline, float thickness)
        {
            Draws.Add((text, bounds, color, outline));
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color)
        {
            throw new InvalidOperationException("Damage values require an outline.");
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment)
        {
            throw new InvalidOperationException("Damage values require an outline.");
        }

        public void Fill(UiRect bounds, UiColor color)
        {
        }

        public void MenuPointer(UiRect bounds)
        {
        }

        public void Image(UiImageSource source, UiRect bounds)
        {
        }

        public void Line(float x1, float y1, float x2, float y2, UiColor color, float thickness)
        {
        }

        public void PushClip(UiRect bounds)
        {
        }

        public void PopClip()
        {
        }
    }
}
