using System.Drawing;
using System.Drawing.Imaging;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class TripleTriadRulesViewTests
{
    private const TripleTriadRules AllRules = TripleTriadRules.Open | TripleTriadRules.Same | TripleTriadRules.Plus | TripleTriadRules.SameWall | TripleTriadRules.Elemental | TripleTriadRules.SuddenDeath | TripleTriadRules.Random;

    [Fact]
    public void RulesWindowUsesOriginalBackgroundAndGrowsToFitUnwrappedContent()
    {
        RecordingCanvas single = Draw(Create(TripleTriadRules.Open, "One"));
        RecordingCanvas multiple = Draw(Create(AllRules, "All cards from the losing hand"));
        UiRect singlePanel = single.PanelBounds;
        UiRect multiplePanel = multiple.PanelBounds;
        Assert.True(multiplePanel.Width > singlePanel.Width);
        Assert.True(multiplePanel.Height > singlePanel.Height);
        foreach (RecordingCanvas canvas in new[] { single, multiple })
        {
            UiRect panel = canvas.PanelBounds;
            Assert.Equal(480f, panel.X + panel.Width / 2);
            Assert.Equal(336f, panel.Y + panel.Height / 2);
            Assert.True(panel.Width < 960 && panel.Height < 672);
            Assert.Equal(UiImageKind.TripleTriadBackground, Assert.Single(canvas.Images).Kind);
            Assert.Equal(1, canvas.Texts.Count(line => line.Text == "INFO."));
            Assert.DoesNotContain(canvas.Texts, line => line.Text == "TRIPLE TRIAD" || line.Text.StartsWith("Review the rules", StringComparison.Ordinal));
            Assert.All(canvas.Texts, line =>
            {
                Assert.DoesNotContain('\n', line.Text);
                Assert.True(line.Bounds.Width >= NaturalWidth(line.Text, line.FontSize), line.Text);
                Assert.True(line.Bounds.X >= panel.X && line.Bounds.Right <= panel.Right, line.Text);
                Assert.True(line.Bounds.Y >= panel.Y && line.Bounds.Bottom <= panel.Bottom, line.Text);
            });
            Assert.All(canvas.MeasurementWidths, width => Assert.True(float.IsPositiveInfinity(width)));
            Assert.NotEmpty(canvas.MeasurementWidths);
        }
        string[] expected = ["Rules:", "·  Open", "·  Same", "·  Plus", "·  Same Wall", "·  Elemental", "·  Sudden Death", "·  Random", "·  Trade Rule: All cards from the losing hand", "Play", "Quit"];
        Assert.Equal(expected, multiple.Texts.Where(line => line.Text != "INFO.").Select(line => line.Text));
        UiRect[] rows = multiple.Texts.Where(line => line.Text != "INFO.").Select(line => line.Bounds).ToArray();
        for (int index = 1; index < rows.Length; index++)
        {
            Assert.True(rows[index].Y >= rows[index - 1].Bottom);
        }
    }

    [Theory]
    [InlineData(false, 960, 672, 0)]
    [InlineData(false, 1440, 1008, 1)]
    [InlineData(true, 1200, 672, 0)]
    [InlineData(true, 1440, 1008, 1)]
    public void PointerUsesRenderedChoiceBoundsAfterWindowResizes(bool allRules, int width, int height, int choice)
    {
        TripleTriadController controller = Create(allRules ? AllRules : TripleTriadRules.Open, "One");
        List<int> choices = [];
        controller.RuleChoiceRequested = choices.Add;
        UiView view = controller.BuildView();
        view.Resize(width, height);
        RecordingCanvas canvas = new();
        view.Draw(canvas, new UiTheme());
        UiRect target = Assert.Single(canvas.Texts, line => line.Text == (choice == 0 ? "Play" : "Quit")).Bounds;
        view.Pointer(2, 2, true);
        Assert.Empty(choices);
        view.Pointer(2, 2, false);
        view.Pointer(target.X + target.Width / 2, target.Y + target.Height / 2, true);
        Assert.Equal(new[] { choice }, choices);
    }

    [Fact]
    public void KeyboardSelectsQuitAndBeginningAnotherPromptResetsToPlay()
    {
        TripleTriadController controller = Create(AllRules, "One");
        int accepted = 0;
        int cancelled = 0;
        controller.BeginRulesPrompt("One", () => accepted++, () => cancelled++);
        Assert.Equal(0, controller.RulesSelection);
        controller.Navigate(UiKey.Down);
        Assert.Equal(1, controller.RulesSelection);
        controller.Navigate(UiKey.Confirm);
        Assert.Equal(1, cancelled);
        Assert.Equal(0, accepted);
        controller.BeginRulesPrompt("All", () => accepted++, () => cancelled++);
        Assert.Equal(0, controller.RulesSelection);
        controller.Navigate(UiKey.Up);
        Assert.Equal(1, controller.RulesSelection);
        controller.Navigate(UiKey.Down);
        Assert.Equal(0, controller.RulesSelection);
        controller.Navigate(UiKey.Confirm);
        Assert.Equal(1, accepted);
        Assert.False(controller.AwaitingRules);
    }

    [Fact]
    public void OriginalArtworkRendersOneRuleAndAllRules()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Original resources required.");
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_TRIAD_UI_OUTPUT_DIRECTORY") ?? Path.Combine(Path.GetTempPath(), "ff8-triad-rules-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        Render("rules-one", TripleTriadRules.Open);
        Render("rules-all", AllRules);
        Console.WriteLine($"Triple Triad rules original-resource renders: {output}");

        void Render(string name, TripleTriadRules rules)
        {
            using Bitmap bitmap = new(960, 672, PixelFormat.Format32bppArgb);
            using BattlePreviewCanvas canvas = new(bitmap, directory);
            UiView view = Create(rules, "One").BuildView();
            view.Resize(960, 672);
            view.Draw(canvas, new UiTheme());
            bitmap.Save(Path.Combine(output, name + ".png"), ImageFormat.Png);
        }
    }

    private static TripleTriadController Create(TripleTriadRules rules, string tradeRule)
    {
        TripleTriadCard[] cards = Enumerable.Range(0, 110).Select(id => new TripleTriadCard(id, 1, 1, 1, 1, TripleTriadElement.None)).ToArray();
        TripleTriadController controller = new(cards, Enumerable.Range(0, 5).ToDictionary(id => id, _ => 1), [5, 6, 7, 8, 9], id => id.ToString(), rules);
        controller.BeginRulesPrompt(tradeRule, () =>
        {
        }, () =>
        {
        });
        return controller;
    }

    private static RecordingCanvas Draw(TripleTriadController controller)
    {
        UiView view = controller.BuildView();
        view.Resize(960, 672);
        RecordingCanvas canvas = new();
        view.Draw(canvas, new UiTheme());
        return canvas;
    }

    private static float NaturalWidth(string text, float fontSize) => text.Length * fontSize * 0.5f;

    private sealed class RecordingCanvas : IUiCanvas
    {
        public List<(string Text, float FontSize, UiRect Bounds)> Texts { get; } = [];
        public List<UiImageSource> Images { get; } = [];
        public List<float> MeasurementWidths { get; } = [];
        private readonly List<UiRect> fills = [];
        public UiRect PanelBounds => new(fills.Min(rect => rect.X), fills.Min(rect => rect.Y), fills.Max(rect => rect.Right) - fills.Min(rect => rect.X), fills.Max(rect => rect.Bottom) - fills.Min(rect => rect.Y));

        public void Fill(UiRect bounds, UiColor color)
        {
            fills.Add(bounds);
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color)
        {
            Texts.Add((text, fontSize, bounds));
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment)
        {
            Text(text, fontSize, bounds, color);
        }

        public void OutlinedText(string text, float fontSize, UiRect bounds, UiColor color, UiColor outline, float thickness)
        {
            Text(text, fontSize, bounds, color);
        }

        public UiSize MeasureText(string text, float fontSize, float maximumWidth)
        {
            MeasurementWidths.Add(maximumWidth);
            return new UiSize(NaturalWidth(text, fontSize), fontSize);
        }

        public void Image(UiImageSource source, UiRect bounds)
        {
            Images.Add(source);
        }

        public void MenuPointer(UiRect bounds)
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
