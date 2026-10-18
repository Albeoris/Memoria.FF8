using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;

namespace Memoria.FF8.App.Tests;

public sealed class NativeScanWindowDrawingTests
{
    [Fact]
    public void Current_rich_prefix_uses_original_origin_fixed_line_offsets_and_existing_inner_clip()
    {
        RecordingCanvas canvas = new();
        canvas.PushClip(new UiRect(0, 0, 600, 460));
        NativeScanWindowText window = Window(0, 0, 8) with
        {
            Lines = [new(new DialogueLine([new("A ", Color.Lime), new("Pr", Color.Red)], 0), 2, -3), new(new DialogueLine([new("Next", Color.White)], 1), 34, 13)]
        };
        NativeScanWindowDrawing.Draw(canvas, new UiTheme(), [window], new UiRect(10, 20, 640, 448), 0, 1);
        Assert.Equal(new[] { "A ", "Pr", "Next" }, canvas.Runs.Select(run => run.Text));
        Assert.Equal(new[] { 226f, 250f, 290f }, canvas.Runs.Select(run => run.X));
        Assert.Equal(new[] { 106f, 106f, 138f }, canvas.Runs.Select(run => run.Y));
        Assert.All(canvas.Runs, run => Assert.Equal(new UiRect(250, 120, 200, 100), run.Clip));
        Assert.Equal(new UiColor(0, 255, 0), canvas.Runs[0].Color);
        Assert.Equal(32, canvas.Runs[0].Size);
        Assert.Equal(0, canvas.Fills);
        Assert.Single(canvas.Clips);
    }

    [Fact]
    public void Priority_then_physical_index_orders_windows_and_flags_eight_alone_do_not_draw_a_panel()
    {
        RecordingCanvas canvas = new();
        NativeScanWindowDrawing.Draw(canvas, new UiTheme(), [Window(7, 3, 8), Window(4, 1, 8), Window(2, 1, 10)], new UiRect(0, 0, 320, 224), 0, 1);
        Assert.Equal(new[] { "2", "4", "7" }, canvas.Runs.Select(run => run.Text));
        Assert.True(canvas.Fills > 0);
        Assert.Empty(canvas.Clips);
    }

    [Theory]
    [InlineData(2048, 64)]
    [InlineData(4096, 128)]
    [InlineData(65535, 0)]
    public void Signed_intensity_uses_truncation_and_native_128_normal_brightness(int intensity, int expectedRed)
    {
        RecordingCanvas canvas = new();
        NativeScanWindowText window = Window(0, 0, 8) with { Intensity = (ushort)intensity, Lines = [new(new DialogueLine([new("x", new Color(128, 0, 0))], 0), 2, 2)] };
        NativeScanWindowDrawing.Draw(canvas, new UiTheme(), [window], new UiRect(0, 0, 320, 224), 0, 1);
        Assert.Equal((byte)expectedRed, Assert.Single(canvas.Runs).Color.R);
    }

    [Fact]
    public void Drawing_failure_restores_callers_clip_stack()
    {
        RecordingCanvas canvas = new() { ThrowOnRun = true };
        canvas.PushClip(new UiRect(0, 0, 320, 224));
        Assert.Throws<InvalidOperationException>(() => NativeScanWindowDrawing.Draw(canvas, new UiTheme(), [Window(0, 0, 8)], new UiRect(0, 0, 320, 224), 0, 1));
        Assert.Single(canvas.Clips);
    }

    private static NativeScanWindowText Window(int index, byte priority, byte flags)
    {
        return new(index, new(110, 44, 116, 88), new(120, 50, 100, 50), 4096, 4096, priority, [new(new DialogueLine([new(index.ToString(), Color.White)], 0), 2, 2)])
        {
            OriginalRectangle = new(100, 40, 136, 128),
            BackendFlags38 = flags
        };
    }

    private sealed record Run(string Text, float Size, float X, float Y, UiColor Color, UiRect Clip);

    private sealed class RecordingCanvas : IUiCanvas, INativeScanTextCanvas
    {
        public List<Run> Runs { get; } = [];
        public Stack<UiRect> Clips { get; } = new();
        public int Fills { get; private set; }
        public bool ThrowOnRun { get; init; }

        public float ScanTextAdvance(string value, float size)
        {
            return value.Length * 12;
        }

        public void ScanTextRun(string value, float size, float x, float y, UiColor color)
        {
            if (ThrowOnRun)
            {
                throw new InvalidOperationException();
            }
            Runs.Add(new(value, size, x, y, color, Clips.Peek()));
        }

        public void Fill(UiRect bounds, UiColor color)
        {
            Fills++;
        }

        public void PushClip(UiRect bounds)
        {
            Clips.Push(Clips.Count == 0 ? bounds : bounds.Intersect(Clips.Peek()));
        }

        public void PopClip()
        {
            Clips.Pop();
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color)
        {
            throw new NotSupportedException();
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment)
        {
            throw new NotSupportedException();
        }

        public void Line(float x1, float y1, float x2, float y2, UiColor color, float thickness)
        {
        }

        public void MenuPointer(UiRect bounds)
        {
        }

        public void Image(UiImageSource source, UiRect bounds)
        {
        }
    }
}
