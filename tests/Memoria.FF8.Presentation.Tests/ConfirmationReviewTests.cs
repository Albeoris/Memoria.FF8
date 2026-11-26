using Memoria.FF8.Ui;

namespace Memoria.FF8;

public sealed class ConfirmationReviewTests
{
    [Fact]
    public async Task Large_model_review_draws_visible_lines_and_can_scroll_to_the_import_question()
    {
        using GameConfirmationOverlay overlay = new();
        const string firstWarning = "Clip 0001 changed duration.";
        const string lastWarning = "Clip 2663 changed duration.";
        const string question = "Import this model anyway?";
        string warnings = string.Join("\n", Enumerable.Range(1, 2663).Select(index => $"Clip {index:0000} changed duration."));
        Task<bool> decision = overlay.ShowAsync(warnings + "\n\n" + question, "Model import warning", TestContext.Current.CancellationToken);
        ReviewCanvas canvas = new();
        UiRect viewport = new(0, 0, 960, 672);
        VectorUiTheme theme = new();
        string family = SystemFontCatalog.DefaultFamily;

        overlay.Draw(canvas, theme, viewport, family);
        Assert.Contains(firstWarning, canvas.BodyRows);
        Assert.DoesNotContain(lastWarning, canvas.BodyRows);
        Assert.InRange(canvas.BodyRows.Count, 2, 40);

        for (int index = 0; index < 1000; index++)
        {
            overlay.Handle(UiKey.PageDown);
        }
        canvas.BodyRows.Clear();
        overlay.Draw(canvas, theme, viewport, family);
        Assert.DoesNotContain(firstWarning, canvas.BodyRows);
        Assert.Contains(lastWarning, canvas.BodyRows);
        Assert.Equal(question, canvas.BodyRows[^1]);
        Assert.InRange(canvas.BodyRows.Count, 2, 40);

        Assert.False(decision.IsCompleted);
        overlay.Handle(UiKey.Confirm);
        Assert.False(await decision);
    }

    private sealed class ReviewCanvas : IUiCanvas
    {
        private bool clipped;
        public List<string> BodyRows { get; } = [];

        public void Text(string text, float fontSize, UiRect bounds, UiColor color)
        {
            if (clipped)
            {
                Assert.DoesNotContain('\n', text);
                BodyRows.Add(text);
            }
        }

        public UiSize MeasureText(string text, float fontSize, float maximumWidth)
        {
            System.Numerics.Vector2 size = PortableTextRasterizer.MeasureWrapped(text, fontSize, maximumWidth);
            return new(size.X, size.Y);
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment)
        {
            Text(text, fontSize, bounds, color);
        }

        public void PushClip(UiRect bounds)
        {
            clipped = true;
        }

        public void PopClip()
        {
            clipped = false;
        }

        public void Fill(UiRect bounds, UiColor color)
        {
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
