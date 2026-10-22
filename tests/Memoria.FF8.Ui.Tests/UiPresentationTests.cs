using Memoria.FF8.Ui;

namespace Memoria.FF8.Ui.Tests;

public sealed class UiPresentationTests
{
    [Fact]
    public void Classic_primitives_keep_original_fill_colors_and_native_pointer()
    {
        RecordingCanvas canvas = new();
        UiTheme theme = new();
        UiRect bounds = new(0, 0, 100, 20);
        theme.Selection(canvas, bounds);
        theme.RowSeparator(canvas, bounds);
        theme.Progress(canvas, bounds, 0.5f, true);
        theme.Pointer(canvas, 4, 6);

        Assert.Null(theme.FontFamily);
        Assert.False(theme.AlwaysHighlightSelection);
        Assert.Equal(4, canvas.Fills.Count);
        Assert.Equal((bounds, theme.Highlight), canvas.Fills[0]);
        Assert.Equal((bounds, theme.BorderDark), canvas.Fills[1]);
        Assert.Equal(new UiColor(60, 60, 60), canvas.Fills[2].Color);
        Assert.Equal(new UiRect(1, 1, 49, 18), canvas.Fills[3].Bounds);
        Assert.Equal(1, canvas.Pointers);
        Assert.Empty(canvas.Lines);
    }

    [Fact]
    public void Vector_panels_are_translucent_with_finite_caption_divider_and_shared_band_edges()
    {
        RecordingCanvas canvas = new();
        new VectorUiTheme().Panel(canvas, new UiRect(0, 0, 300, 140), "JUNCTION");

        Assert.Equal(32, canvas.Fills.Count);
        Assert.All(canvas.Fills, fill => Assert.InRange((int)fill.Color.A, 180, 205));
        for (int index = 1; index < canvas.Fills.Count; index++)
        {
            Assert.Equal(canvas.Fills[index - 1].Bounds.Bottom, canvas.Fills[index].Bounds.Y);
        }
        Assert.Equal(5, canvas.Lines.Count);
        Assert.Contains(canvas.Texts, text => text.Value == "JUNCTION");
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)128)]
    [InlineData((byte)255)]
    public void Vector_panel_respects_script_background_opacity(byte opacity)
    {
        RecordingCanvas canvas = new();
        new VectorUiTheme { PanelBackgroundAlpha = opacity }.Panel(canvas, new UiRect(0, 0, 100, 50));
        Assert.All(canvas.Fills, fill => Assert.InRange((int)fill.Color.A, 0, 202 * opacity / 255));
    }

    [Fact]
    public void Vector_horizontal_selection_uses_only_a_red_bottom_underline()
    {
        RecordingCanvas canvas = new();
        new VectorUiTheme().Selection(canvas, new UiRect(10, 20, 100, 30), underline: true);

        var fill = Assert.Single(canvas.Fills);
        Assert.Equal(new UiRect(10, 47, 100, 3), fill.Bounds);
        Assert.True(fill.Color.R > fill.Color.B);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(float.NaN)]
    public void Vector_progress_clamps_invalid_ratios_to_the_track(float ratio)
    {
        RecordingCanvas canvas = new();
        UiRect bounds = new(10, 20, 100, 10);
        new VectorUiTheme().Progress(canvas, bounds, ratio, true);

        Assert.All(canvas.Fills, fill =>
        {
            Assert.True(fill.Bounds.X >= bounds.X);
            Assert.True(fill.Bounds.Right <= bounds.Right);
            Assert.True(fill.Bounds.Y >= bounds.Y);
            Assert.True(fill.Bounds.Bottom <= bounds.Bottom);
        });
        Assert.Equal(ratio > 1 ? 35 : 2, canvas.Fills.Count);
    }

    [Fact]
    public void Theme_selection_precedes_text_even_when_classic_row_highlight_is_disabled()
    {
        RecordingCanvas canvas = new();
        UiList list = new() { Items = ["Junction"], HighlightSelection = false, RowHeight = 30 };
        list.Arrange(new UiRect(0, 0, 150, 30));
        list.Draw(canvas, new VectorUiTheme());

        Assert.True(canvas.Calls.IndexOf("fill") < canvas.Calls.IndexOf("text"));
        Assert.True(canvas.Fills.Count > 0);
        canvas = new RecordingCanvas();
        list.Draw(canvas, new UiTheme());
        Assert.Empty(canvas.Fills);
    }

    [Fact]
    public void Presentation_font_and_explicit_font_flow_through_scaled_drawing_and_measurement()
    {
        RecordingCanvas canvas = new();
        UiView view = new(new FontProbe()) { DesignWidth = 100, DesignHeight = 50 };
        view.Resize(200, 100);
        view.Draw(canvas, new VectorUiTheme());

        Assert.Contains(canvas.Texts, text => text.Value == "Default" && text.Family == "Georgia" && text.Size == 20 && text.Flow == UiTextFlow.SingleLine);
        Assert.Contains(canvas.Texts, text => text.Value == "Explicit" && text.Family == "Arial" && text.Size == 20);
        Assert.Contains("Georgia", canvas.MeasuredFamilies);
        Assert.Contains("Arial", canvas.MeasuredFamilies);
    }

    private sealed class FontProbe : UiElement
    {
        protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
        {
            canvas.MeasureText("Default", 10, 100);
            canvas.MeasureText("Explicit", 10, 100, "Arial");
            canvas.Text("Default", 10, Bounds, theme.Foreground, UiAlignment.Center, UiTextFlow.SingleLine);
            canvas.Text("Explicit", 10, Bounds, theme.Foreground, "Arial");
        }
    }

    private sealed class RecordingCanvas : IUiCanvas
    {
        public List<(UiRect Bounds, UiColor Color)> Fills { get; } = [];
        public List<UiColor> Lines { get; } = [];
        public List<string> Calls { get; } = [];
        public List<(string Value, string? Family, float Size, UiTextFlow Flow)> Texts { get; } = [];
        public List<string?> MeasuredFamilies { get; } = [];
        public int Pointers { get; private set; }

        public void Fill(UiRect bounds, UiColor color)
        {
            Calls.Add("fill");
            Fills.Add((bounds, color));
        }

        public UiSize MeasureText(string text, float fontSize, float maximumWidth)
        {
            return MeasureText(text, fontSize, maximumWidth, null);
        }

        public UiSize MeasureText(string text, float fontSize, float maximumWidth, string? fontFamily)
        {
            MeasuredFamilies.Add(fontFamily);
            return new UiSize(Math.Min(maximumWidth, text.Length * fontSize * 0.6f), fontSize * 1.5f);
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color)
        {
            Text(text, fontSize, bounds, color, null, UiAlignment.Start, UiTextFlow.Auto);
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color, string? fontFamily)
        {
            Text(text, fontSize, bounds, color, fontFamily, UiAlignment.Start, UiTextFlow.Auto);
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment)
        {
            Text(text, fontSize, bounds, color, null, verticalAlignment, UiTextFlow.Auto);
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment, UiTextFlow flow)
        {
            Text(text, fontSize, bounds, color, null, verticalAlignment, flow);
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color, string? fontFamily, UiAlignment verticalAlignment, UiTextFlow flow)
        {
            Calls.Add("text");
            Texts.Add((text, fontFamily, fontSize, flow));
        }

        public void MenuPointer(UiRect bounds)
        {
            Pointers++;
        }

        public void Image(UiImageSource source, UiRect bounds)
        {
        }

        public void Line(float x1, float y1, float x2, float y2, UiColor color, float thickness)
        {
            Lines.Add(color);
        }

        public void PushClip(UiRect bounds)
        {
        }

        public void PopClip()
        {
        }
    }
}
