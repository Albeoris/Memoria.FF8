namespace Memoria.FF8.Ui.Tests;

public sealed class UiAlignedTextTests
{
    [Theory]
    [InlineData(UiAlignment.Center, 70, 140)]
    [InlineData(UiAlignment.End, 130, 80)]
    public void AlignmentUsesCanvasGlyphMeasurement(UiAlignment alignment, float expectedX, float expectedWidth)
    {
        RecordingCanvas recording = new() { MeasuredWidth = 80 };
        IUiCanvas canvas = recording;
        UiColor color = new(255, 240, 220);

        canvas.AlignedText("WW narrow", 24, new UiRect(10, 20, 200, 40), color, alignment);

        Assert.Equal(("WW narrow", 24f, 200f), Assert.Single(recording.Measurements));
        Assert.Equal(("WW narrow", 24f, new UiRect(expectedX, 20, expectedWidth, 40), color), Assert.Single(recording.Texts));
    }

    [Theory]
    [InlineData(UiAlignment.Start)]
    [InlineData(UiAlignment.Stretch)]
    public void LeadingAlignmentDoesNotRequireTextMeasurement(UiAlignment alignment)
    {
        RecordingCanvas recording = new() { ThrowOnMeasure = true };
        IUiCanvas canvas = recording;
        UiRect bounds = new(10, 20, 200, 40);
        UiColor color = new(255, 255, 255);

        canvas.AlignedText("Text", 18, bounds, color, alignment);

        Assert.Empty(recording.Measurements);
        Assert.Equal(("Text", 18f, bounds, color), Assert.Single(recording.Texts));
    }

    [Theory]
    [InlineData(UiAlignment.Center)]
    [InlineData(UiAlignment.End)]
    public void OversizedGlyphMeasurementPreservesSafeOriginalClippingBounds(UiAlignment alignment)
    {
        RecordingCanvas recording = new() { MeasuredWidth = 500 };
        IUiCanvas canvas = recording;
        UiRect bounds = new(10, 20, 100, 40);

        canvas.AlignedText("Oversized", 24, bounds, new UiColor(255, 255, 255), alignment);

        Assert.Equal(100f, Assert.Single(recording.Measurements).MaximumWidth);
        Assert.Equal(bounds, Assert.Single(recording.Texts).Bounds);
    }

    private sealed class RecordingCanvas : IUiCanvas
    {
        public float MeasuredWidth { get; init; }
        public bool ThrowOnMeasure { get; init; }
        public List<(string Text, float FontSize, float MaximumWidth)> Measurements { get; } = [];
        public List<(string Text, float FontSize, UiRect Bounds, UiColor Color)> Texts { get; } = [];

        public UiSize MeasureText(string text, float fontSize, float maximumWidth)
        {
            if (ThrowOnMeasure)
            {
                throw new InvalidOperationException("Measurement was not expected.");
            }
            Measurements.Add((text, fontSize, maximumWidth));
            return new UiSize(MeasuredWidth, fontSize);
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color)
        {
            Texts.Add((text, fontSize, bounds, color));
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment)
        {
            Text(text, fontSize, bounds, color);
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
