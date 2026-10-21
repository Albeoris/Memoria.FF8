using Memoria.FF8.Ui;

namespace Memoria.FF8.Ui.Tests;

public sealed class SettingRowTests
{
    [Fact]
    public void ConstrainedChoicesReserveColumnGapAndShowOneMeasuredOptionWithWorkingNavigation()
    {
        UiChoiceRow choices = new() { Text = "Smart Attack", Items = ["Manual attack", "Automatic attack"], FontSize = 72, ColumnGap = 36 };
        choices.Arrange(new UiRect(0, 0, 400, 80));
        RecordingCanvas canvas = new() { SupportsMeasurement = true };

        choices.Draw(canvas, new UiTheme());

        Assert.Equal(36, choices.ValueBounds.X - choices.LabelBounds.Right);
        Assert.Equal(new[] { "Smart Attack", "Manual attack" }, canvas.Texts.Select(text => text.Text));
        Assert.Equal(4, canvas.Lines);
        choices.Pointer(choices.ValueBounds.Right - 1, 40);
        Assert.Equal(1, choices.SelectedIndex);
        choices.Key(UiKey.Left);
        Assert.Equal(0, choices.SelectedIndex);

        choices.FontSize = 6;
        canvas.Texts.Clear();
        choices.Draw(canvas, new UiTheme());
        Assert.Equal(new[] { "Smart Attack", "Manual attack", "Automatic attack" }, canvas.Texts.Select(text => text.Text));
        Assert.Equal(choices.OptionGap, canvas.Texts[2].Bounds.X - canvas.Texts[1].Bounds.Right);
        choices.Pointer(choices.ValueBounds.Right - 1, 40);
        Assert.Equal(1, choices.SelectedIndex);
    }

    [Fact]
    public void Choice_rows_align_with_slider_columns_and_keep_unselected_options_muted()
    {
        UiChoiceRow choices = new() { Text = "Sound", Items = new List<string> { "Stereo", "Mono" }, SelectedIndex = 1, FontSize = 20 };
        UiNumericSlider slider = new() { Text = "Volume", Max = 100, Value = 75, ValueWidth = 66 };
        UiRect bounds = new(20, 10, 600, 32);
        choices.Arrange(bounds);
        slider.Arrange(bounds);
        RecordingCanvas canvas = new();
        UiTheme theme = new();
        choices.Draw(canvas, theme);

        Assert.Equal(slider.LabelBounds, choices.LabelBounds);
        Assert.Equal(slider.ValueBounds, choices.ValueBounds);
        Assert.Equal(290, choices.ValueBounds.X);
        Assert.Equal(new[] { "Sound", "Stereo", "Mono" }, canvas.Texts.Select(text => text.Text));
        Assert.Equal(theme.Muted, canvas.Texts[1].Color);
        Assert.Equal(theme.Foreground, canvas.Texts[2].Color);
        Assert.Equal(canvas.Texts[1].Bounds.Width, canvas.Texts[2].Bounds.Width);
        Assert.Equal(canvas.Texts[1].Bounds.Right + choices.OptionGap, canvas.Texts[2].Bounds.X);
        Assert.All(canvas.Texts, text => Assert.Equal(20, text.FontSize));
        Assert.Empty(canvas.Fills);
    }

    [Fact]
    public void Choices_change_only_on_input_and_label_clicks_only_focus()
    {
        UiChoiceRow choices = new() { Items = new List<string> { "Stereo", "Mono" } };
        List<int> changes = new();
        List<bool> focusChanges = new();
        choices.SelectionChanged = changes.Add;
        choices.FocusChanged = focusChanges.Add;
        UiView view = new(choices);
        view.Resize(400, 40);
        choices.SelectedIndex = 1;
        view.Focus(choices);
        view.Focus(choices);
        view.Pointer(20, 20, true);
        Assert.Empty(changes);
        Assert.Equal(new[] { true }, focusChanges);
        view.Pointer(20, 20, false);
        view.Pointer(200, 20, true);
        Assert.Equal(0, choices.SelectedIndex);
        Assert.True(view.Key(UiKey.Left));
        Assert.True(view.Key(UiKey.Right));
        Assert.True(view.Key(UiKey.Right));
        Assert.True(view.Key(UiKey.Confirm));
        Assert.Equal(new[] { 0, 1, 0 }, changes);
        choices.Enabled = false;
        Assert.False(choices.Key(UiKey.Right));
        choices.Pointer(390, 20);
        Assert.Equal(0, choices.SelectedIndex);
        Assert.Null(view.FocusedElement);
        Assert.Equal(new[] { true, false }, focusChanges);
    }

    [Fact]
    public void Large_choice_sets_show_a_single_value_and_allow_stepped_pointer_navigation()
    {
        UiChoiceRow choices = new() { Text = "Font", Items = Enumerable.Range(0, 30).Select(index => $"Family {index}").ToList(), SelectedIndex = 12 };
        choices.Arrange(new UiRect(0, 0, 400, 30));
        RecordingCanvas canvas = new();
        choices.Draw(canvas, new UiTheme());

        Assert.Equal(new[] { "Font", "Family 12" }, canvas.Texts.Select(text => text.Text));
        Assert.Equal(4, canvas.Lines);
        choices.Pointer(185, 15);
        Assert.Equal(11, choices.SelectedIndex);
        choices.Pointer(390, 15);
        Assert.Equal(12, choices.SelectedIndex);
        choices.Items.Clear();
        Assert.Equal(-1, choices.SelectedIndex);
        Assert.False(choices.Key(UiKey.Confirm));
        choices.Pointer(390, 15);
    }

    [Fact]
    public void Numeric_slider_shows_beveled_fraction_and_numeric_value_with_shared_range_contract()
    {
        UiNumericSlider slider = new() { Text = "Volume", Max = 100, Step = 25, Value = 50, ValueWidth = 66, FontSize = 20, TrackHeight = 22 };
        slider.Arrange(new UiRect(0, 0, 600, 32));
        UiTheme theme = new();
        RecordingCanvas canvas = new();
        slider.Draw(canvas, theme);

        Assert.Equal(new[] { "Volume", "50" }, canvas.Texts.Select(text => text.Text));
        Assert.Equal(66, canvas.Texts[1].Bounds.Width);
        Assert.Equal(slider.ValueBounds.Right, canvas.Texts[1].Bounds.Right);
        var track = canvas.Fills[0];
        var gradient = canvas.Fills.Where(fill => fill.Bounds.X == track.Bounds.X + 4 && fill.Bounds.Height <= 1 && fill.Color.B > fill.Color.R + 8).ToList();
        Assert.Equal(slider.ValueBounds.X, track.Bounds.X);
        Assert.Equal(16, gradient.Count);
        Assert.All(gradient, band => Assert.Equal((track.Bounds.Width - 8) / 2, band.Bounds.Width));
        Assert.True(gradient.Select(band => band.Color).Distinct().Count() >= 8);
        Assert.Equal(theme.SliderLight, gradient[0].Color);
        Assert.Equal(theme.SliderLight, gradient[^1].Color);
        Assert.Contains(gradient, band => band.Color.R < 60 && band.Color.B > 100);
        Assert.Contains(canvas.Fills, fill => fill.Color == new UiColor(248, 248, 248));
        Assert.Contains(canvas.Fills, fill => fill.Color == new UiColor(112, 112, 128));
        Assert.Equal(track.Bounds.Y + 4, gradient[0].Bounds.Y);
        Assert.True(track.Bounds.Right < canvas.Texts[1].Bounds.X);
        List<float> changes = new();
        slider.ValueChanged = changes.Add;
        slider.Pointer(20, 15);
        Assert.Empty(changes);
        slider.Pointer(track.Bounds.X, 15);
        slider.Key(UiKey.Left);
        slider.Key(UiKey.Right);
        slider.Pointer(track.Bounds.Right - 1, 15);
        slider.Key(UiKey.Right);
        Assert.Equal(new float[] { 0, 25, 100 }, changes);
        slider.Value = 500;
        Assert.Equal(100, slider.Value);
        Assert.Throws<ArgumentOutOfRangeException>(() => slider.Step = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => slider.Value = float.NaN);
    }

    [Fact]
    public void Slider_pointer_and_draw_use_the_same_scaled_and_letterboxed_coordinates()
    {
        UiNumericSlider slider = new() { Text = "Volume", Max = 100, Step = 25, ValueWidth = 66, PointerSize = 20, TextInset = 35 };
        UiView view = new(slider) { DesignWidth = 600, DesignHeight = 40 };
        view.Resize(1200, 200);
        RecordingCanvas canvas = new();
        UiTheme theme = new();
        view.Draw(canvas, theme);
        UiRect track = canvas.Fills[0].Bounds;
        view.Pointer(track.X + track.Width / 2, track.Y + track.Height / 2, true);

        Assert.Equal(50, slider.Value);
        Assert.True(track.Y >= 60);
        Assert.Equal(2, Assert.Single(canvas.Pointers).Width / (slider.PointerSize * 1.5f));
        Assert.Equal(2 * slider.PointerSize, Assert.Single(canvas.Pointers).Height);
        Assert.Equal(2 * (slider.LabelBounds.X - 4), Assert.Single(canvas.Pointers).Right);
        slider.Arrange(new UiRect(0, 0, 40, 12));
        canvas = new RecordingCanvas();
        slider.Draw(canvas, theme);
        Assert.Empty(canvas.Fills);
        Assert.All(canvas.Texts, text => Assert.True(text.Bounds.Right <= 40));
    }

    private sealed class RecordingCanvas : IUiCanvas
    {
        public bool SupportsMeasurement { get; init; }
        public List<(string Text, float FontSize, UiRect Bounds, UiColor Color)> Texts { get; } = new();
        public List<(UiRect Bounds, UiColor Color)> Fills { get; } = new();
        public List<UiRect> Pointers { get; } = new();
        public int Lines { get; private set; }

        public UiSize MeasureText(string text, float fontSize, float maximumWidth)
        {
            if (!SupportsMeasurement)
            {
                throw new NotSupportedException();
            }
            return new UiSize(text.Length * fontSize * 0.5f, fontSize);
        }

        public void Fill(UiRect bounds, UiColor color)
        {
            Fills.Add((bounds, color));
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color)
        {
            Texts.Add((text, fontSize, bounds, color));
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment)
        {
            Text(text, fontSize, bounds, color);
        }

        public void MenuPointer(UiRect bounds)
        {
            Pointers.Add(bounds);
        }

        public void Image(UiImageSource source, UiRect bounds)
        {
        }

        public void Line(float x1, float y1, float x2, float y2, UiColor color, float thickness)
        {
            Lines++;
        }

        public void PushClip(UiRect bounds)
        {
        }

        public void PopClip()
        {
        }
    }
}
