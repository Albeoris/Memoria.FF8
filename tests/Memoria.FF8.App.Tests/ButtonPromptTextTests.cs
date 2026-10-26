using Bitmap = SkiaSharp.SKBitmap;
using Color = SkiaSharp.SKColor;

using Microsoft.Xna.Framework.Input;
using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class ButtonPromptTextTests
{
    [Theory]
    [InlineData("")]
    [InlineData("W")]
    [InlineData("Left Ctrl + Up")]
    [InlineData("×")]
    public void Mark_preserves_labels_and_is_idempotent(string label)
    {
        string marked = ButtonPromptText.Mark(label);

        Assert.Equal(label, ButtonPromptText.PlainText(marked));
        Assert.Equal(marked, ButtonPromptText.Mark(marked));
        Assert.Equal(label.Length > 0, ButtonPromptText.Contains(marked));
        Assert.False(ButtonPromptText.Contains(label));
    }

    [Fact]
    public void Binding_prompts_preserve_per_key_chords_and_plain_diagnostic_hints()
    {
        KeyboardInput input = new();
        input.Profile = input.Profile.WithBinding(ControlAction.Cancel, ControlDevice.Keyboard, new([Keys.K, Keys.LeftControl], []));
        string plain = input.BindingHint(ControlAction.Cancel);
        string prompt = input.BindingPrompt(ControlAction.Cancel);
        Assert.Equal("Left Ctrl + K", plain);
        Assert.Equal(plain, ButtonPromptText.PlainText(prompt));
        Assert.False(ButtonPromptText.Contains(plain));
        Assert.Equal(new[] { "Left Ctrl", "K" }, ButtonPromptText.Runs(prompt).Where(run => run.IsButton).Select(run => run.Text));
    }

    [Fact]
    public void Alternatives_and_chords_keep_individual_prompts_and_plain_separators()
    {
        string value = $"Move {ButtonPromptText.Mark("W")} / {ButtonPromptText.Mark("Up")} · {ButtonPromptText.Mark("Left Ctrl")} + {ButtonPromptText.Mark("N5")}";

        DialogueRun[] runs = ButtonPromptText.Runs(value).ToArray();

        Assert.Equal(new[] { "W", "Up", "Left Ctrl", "N5" }, runs.Where(run => run.IsButton).Select(run => run.Text));
        Assert.Equal(new[] { "Move ", " / ", " · ", " + " }, runs.Where(run => !run.IsButton).Select(run => run.Text));
        Assert.All(runs, run => Assert.False(ButtonPromptText.Contains(run.Text)));
        Assert.Equal("Move W / Up · Left Ctrl + N5", string.Concat(runs.Select(run => run.Text)));
    }

    [Fact]
    public void Wrapping_keeps_a_chord_atomic_and_preserves_explicit_line_indices()
    {
        const string chord = "Left Ctrl + Right Shift + N5";
        string value = $"before {ButtonPromptText.Mark(chord)} after\r\n{ButtonPromptText.Mark("×")}";

        DialogueVisualLine[] lines = ButtonPromptText.Lines(value, 20, 65, "Arial");
        DialoguePlacedRun[] buttons = lines.SelectMany(line => line.Runs).Where(placed => placed.Run.IsButton).ToArray();

        Assert.Equal(new[] { chord, "×" }, buttons.Select(placed => placed.Run.Text));
        Assert.Contains(lines, line => line.SourceLineIndex == 0 && line.Runs.Count == 1 && line.Runs[0].Run.Text == chord);
        Assert.Equal(1, lines[^1].SourceLineIndex);
        Assert.All(lines.SelectMany(line => line.Runs), placed => Assert.False(ButtonPromptText.Contains(placed.Run.Text)));
    }

    [Fact]
    public void Measurement_reserves_prompt_advance_and_authored_line_height()
    {
        const float size = 22;
        const string label = "Left Ctrl";
        using SystemTextMeasurer measurer = SystemTextLayout.CreateMeasurer(size, "Arial");
        float expected = ButtonPrompt.Advance(measurer.Measure(label), measurer.LineHeight);

        UiSize single = ButtonPromptText.Measure(ButtonPromptText.Mark(label), size, float.PositiveInfinity, "Arial");
        UiSize twoLines = ButtonPromptText.Measure(ButtonPromptText.Mark(label) + "\nW", size, float.PositiveInfinity, "Arial");

        Assert.Equal(expected, single.Width, 3);
        Assert.Equal(measurer.LineHeight, single.Height, 3);
        Assert.Equal(expected, twoLines.Width, 3);
        Assert.Equal(2 * measurer.LineHeight, twoLines.Height, 3);
        Assert.Equal(new UiSize(0, 0), ButtonPromptText.Measure("", size, 100, "Arial"));
    }

    [Fact]
    public void Renderer_draws_keycap_faces_and_obeys_its_clip_and_parent_clip()
    {
        using Bitmap marked = new(180, 80);
        using Bitmap plain = new(180, 80);
        UiRect bounds = new(12, 14, 66, 40);
        UiRect parentClip = new(20, 8, 40, 50);
        using (BattlePreviewCanvas canvas = new(marked, gameDirectory: ""))
        {
            canvas.PushClip(parentClip);
            Draw(canvas, ButtonPromptText.Mark("Left Ctrl + W"), 24, bounds);
            canvas.PopClip();
        }
        using (BattlePreviewCanvas canvas = new(plain, gameDirectory: ""))
        {
            canvas.PushClip(parentClip);
            Draw(canvas, "Left Ctrl + W", 24, bounds);
            canvas.PopClip();
        }

        int keycapPixels = 0;
        int plainFacePixels = 0;
        for (int y = 0; y < marked.Height; y++)
        {
            for (int x = 0; x < marked.Width; x++)
            {
                Color pixel = marked.GetPixel(x, y);
                if (x < 20 || x >= 60 || y < 14 || y >= 54)
                {
                    Assert.Equal(SkiaSharp.SKColors.Black.ToArgb(), pixel.ToArgb());
                }
                keycapPixels += IsFace(pixel) ? 1 : 0;
                plainFacePixels += IsFace(plain.GetPixel(x, y)) ? 1 : 0;
            }
        }
        Assert.True(keycapPixels > plainFacePixels + 100, "Marked bindings must draw a solid keycap face, beyond antialiased text pixels.");
    }

    [Fact]
    public void MixedPromptProsePreservesExplicitWrappedFlowInsteadOfBecomingOneScrollingRow()
    {
        string value = $"Confirm {ButtonPromptText.Mark("Enter")} then " + string.Join(" ", Enumerable.Repeat("read the destination instructions before continuing", 8));
        UiRect bounds = new(10, 10, 220, 100);
        using Bitmap wrapped = new(240, 120);
        using Bitmap singleLine = new(240, 120);
        using (BattlePreviewCanvas canvas = new(wrapped, gameDirectory: ""))
        {
            canvas.BeginFrame(TimeSpan.Zero);
            canvas.Text(value, 20, bounds, new UiColor(244, 244, 244), UiAlignment.Start, UiTextFlow.Wrap);
        }
        using (BattlePreviewCanvas canvas = new(singleLine, gameDirectory: ""))
        {
            canvas.BeginFrame(TimeSpan.Zero);
            canvas.Text(value, 20, bounds, new UiColor(244, 244, 244), UiAlignment.Start, UiTextFlow.SingleLine);
        }

        int wrappedLowerPixels = 0;
        int singleLowerPixels = 0;
        for (int y = 60; y < 110; y++)
        {
            for (int x = 10; x < 230; x++)
            {
                wrappedLowerPixels += wrapped.GetPixel(x, y).ToArgb() != SkiaSharp.SKColors.Black.ToArgb() ? 1 : 0;
                singleLowerPixels += singleLine.GetPixel(x, y).ToArgb() != SkiaSharp.SKColors.Black.ToArgb() ? 1 : 0;
            }
        }
        Assert.True(wrappedLowerPixels > 100);
        Assert.Equal(0, singleLowerPixels);
    }

    [Theory]
    [InlineData(640)]
    [InlineData(1280)]
    public void Keyboard_and_controller_prompt_cards_render_without_game_resources(int width)
    {
        float scale = width / 640f;
        using Bitmap bitmap = new(width, (int)(360 * scale));
        using (BattlePreviewCanvas canvas = new(bitmap, gameDirectory: ""))
        {
            canvas.Fill(new UiRect(0, 0, width, bitmap.Height), new UiColor(20, 24, 32));
            canvas.Fill(Rect(20, 48, 600, 124), new UiColor(31, 39, 54));
            canvas.Fill(Rect(20, 188, 600, 144), new UiColor(31, 39, 54));
            Draw(canvas, "Control prompts", 24 * scale, Rect(32, 8, 570, 36));
            Draw(canvas, "Keyboard", 18 * scale, Rect(32, 58, 570, 28));
            Draw(canvas, $"Move  {ButtonPromptText.Mark("W")} / {ButtonPromptText.Mark("Up")}    Confirm  {ButtonPromptText.Mark("Enter")}", 20 * scale, Rect(32, 92, 570, 34));
            Draw(canvas, $"Chord  {ButtonPromptText.Mark("Left Ctrl + Right Shift + N5")}", 18 * scale, Rect(32, 132, 570, 32));
            Draw(canvas, "Controller", 18 * scale, Rect(32, 200, 570, 28));
            Draw(canvas, $"Confirm  {ButtonPromptText.Mark("×")}    Cancel  {ButtonPromptText.Mark("○")}    Menu  {ButtonPromptText.Mark("△")}", 20 * scale, Rect(32, 238, 570, 36));
            Draw(canvas, $"Alternatives  {ButtonPromptText.Mark("[A]")} / {ButtonPromptText.Mark("South button")}", 20 * scale, Rect(32, 282, 570, 36));
        }

        int facePixels = 0;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                facePixels += IsFace(bitmap.GetPixel(x, y)) ? 1 : 0;
            }
        }
        Assert.True(facePixels > 500 * scale * scale);
        if (Environment.GetEnvironmentVariable("MEMORIA_FF8_CONTROL_PROMPT_OUTPUT") is string directory && !string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
            bitmap.SavePng(Path.Combine(directory, $"control-prompts-{width}.png"));
        }

        UiRect Rect(float x, float y, float w, float h)
        {
            return new UiRect(x * scale, y * scale, w * scale, h * scale);
        }
    }

    private static void Draw(BattlePreviewCanvas canvas, string value, float size, UiRect bounds)
    {
        canvas.Text(value, size, bounds, new UiColor(244, 244, 244), "Arial");
    }

    private static bool IsFace(Color color)
    {
        return color.R == 53 && color.G == 57 && color.B == 64;
    }
}
