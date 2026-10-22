using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class AdaptiveWindowLayoutTests
{
    [Theory]
    [InlineData(false, 1.25f)]
    [InlineData(true, 1.25f)]
    [InlineData(false, 3f)]
    [InlineData(true, 3f)]
    public void SaveCaptionsAndMetadataFitAfterMeasuredLayoutWithoutChangingTheSelectedSlot(bool vector, float scale)
    {
        string directory = Path.Combine(Path.GetTempPath(), "adaptive-save-" + Guid.NewGuid().ToString("N"));
        PlayerSaveMenu menu = new(new PlayerSaveStore(Path.Combine(directory, "checkpoint.json"), Path.Combine(directory, "saves")));
        menu.Open(PlayerSaveMenuMode.Save);
        menu.SelectSlot(17);
        UiView view = PlayerSaveView.Build(menu, "Transfer", "Bank / Page", textScale: scale);
        UiElement? selectedBefore = view.FocusedElement;
        UiTheme theme = vector ? new VectorUiTheme() : new UiTheme();
        RecordingCanvas canvas = new();
        view.Draw(canvas, theme);

        Assert.Equal(17, menu.SelectedSlot);
        Assert.Same(selectedBefore, view.FocusedElement);
        Assert.Contains(menu.VisibleEntries, entry => entry.Slot == 17);
        UiElement[] elements = Descendants(view.Root).ToArray();
        foreach (UiPanel panel in elements.OfType<UiPanel>().Where(panel => !string.IsNullOrEmpty(panel.Caption)))
        {
            UiLabel label = Assert.Single(panel.Children.OfType<UiLabel>());
            Assert.True(label.Bounds.Y >= panel.Bounds.Y + theme.CaptionContentHeight(panel.CaptionFontSize));
            Assert.True(label.Bounds.Height >= canvas.MeasureText(label.Text, label.FontSize, float.PositiveInfinity).Height - 0.01f);
            Assert.True(label.Bounds.Bottom <= panel.Bounds.Bottom + 0.01f);
        }
        UiButton selected = Assert.IsType<UiButton>(view.FocusedElement);
        Assert.True(selected.Bounds.Height >= 60 * scale + 6 - 0.01f);
        foreach (UiLabel label in Descendants(selected).OfType<UiLabel>())
        {
            Assert.True(label.Bounds.Y >= selected.Bounds.Y);
            Assert.True(label.Bounds.Bottom <= selected.Bounds.Bottom + 0.01f);
            Assert.True(label.Bounds.Height >= label.FontSize * 1.5f - 0.01f);
        }
        UiButton next = Assert.Single(elements.OfType<UiButton>(), button => button.Id == "save-next");
        Assert.Same(next, view.Root.HitTest(next.Bounds.X + next.Bounds.Width / 2, next.Bounds.Y + next.Bounds.Height / 2));
        Assert.True(selected.Bounds.Bottom <= next.Bounds.Y);
        Assert.Contains(elements.OfType<UiLabel>(), label => label.Text.StartsWith($"Page {menu.Page + 1} / {menu.PageCount}"));
        int capacity = menu.PageCapacity;
        PlayerSaveView.ConfigureTextScale(menu, scale);
        Assert.Equal(capacity, menu.PageCapacity);
    }

    [Theory]
    [InlineData(1.25f)]
    [InlineData(3f)]
    public void VectorPartyHelpReservesItsCaptionAndReflowsTheBodyAboveTheFixedFooter(float scale)
    {
        NativePartyMenuState menu = new(0, string.Empty, string.Empty, 0, 0, 0, 0, 0, NativePartyMenuPhase.Active, 2, [0, 1, 5], 0xFF, 1, 1, false, NativeFieldMenuKind.PartySelection, 2, 0, null, null, null, [2, 255, 3, 4, 255, 6, 7, 255], 2);
        MenuData data = new();
        for (int row = 0; row < 8; row++)
        {
            data.Characters.Add(new MenuCharacter { Id = $"character-{row}", NativeId = row, Name = $"Member {row}", Level = 100, CurrentHp = 9999, MaximumHp = 9999 });
        }
        UiView view = PartySelectionView.Build(menu, data, 0, "Cancel", scale);
        view.Resize(960, 672);
        VectorUiTheme theme = new();
        RecordingCanvas canvas = new();
        view.Draw(canvas, theme);

        var help = Assert.Single(canvas.Texts, text => text.Text == "Select a character to exchange.");
        Assert.True(help.Bounds.Y >= 18 + theme.CaptionContentHeight(UiTheme.DefaultCaptionFontSize * scale));
        Assert.True(help.Bounds.Height >= 25 * scale * 1.5f);
        Assert.All(canvas.Images, image => Assert.True(image.Y >= help.Bounds.Bottom && image.Bottom <= 588));
        Assert.Contains(canvas.Texts, text => text.Text == "Confirm" && text.Bounds.Y == 610);
        for (int first = 0; first < canvas.Texts.Count; first++)
        {
            for (int second = first + 1; second < canvas.Texts.Count; second++)
            {
                Assert.True(canvas.Texts[first].Bounds.Intersect(canvas.Texts[second].Bounds).IsEmpty);
            }
        }
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

    private sealed class RecordingCanvas : IUiCanvas
    {
        public List<(string Text, UiRect Bounds)> Texts { get; } = [];
        public List<UiRect> Images { get; } = [];

        public UiSize MeasureText(string text, float fontSize, float maximumWidth)
        {
            string[] lines = text.Split('\n');
            return new UiSize(lines.Max(line => line.Length) * fontSize / 2, lines.Length * fontSize * 1.5f);
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color)
        {
            if (!string.IsNullOrEmpty(text))
            {
                Texts.Add((text, bounds));
            }
        }

        public void MenuPointer(UiRect bounds)
        {
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment)
        {
            Text(text, fontSize, bounds, color);
        }

        public void Fill(UiRect bounds, UiColor color)
        {
        }
        public void Image(UiImageSource source, UiRect bounds) => Images.Add(bounds);
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
