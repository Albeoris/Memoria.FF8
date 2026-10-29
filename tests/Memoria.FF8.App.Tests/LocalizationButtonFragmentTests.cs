using Memoria.FF8.GameUi;
using Memoria.FF8.Resources.Localization;
using Memoria.FF8.Ui;
using Memoria.FF8.Resources.Text;

namespace Memoria.FF8.App.Tests;

[Collection("Localization tracing")]
public sealed class LocalizationButtonFragmentTests
{
    [Fact]
    public void NativePromptLayoutUsesTheSyntheticAtlasAdvancesAndNeverLooksUpPrivateMarkers()
    {
        Trace(() =>
        {
            string text = LocalizationTextComposition.Concat(Bound("first", "A"), ButtonPromptText.Mark(Bound("key", "K")), Bound("last", "B"));
            NativeCanvas canvas = new();
            ButtonPromptText.Draw(canvas, text, 16, new(0, 0, 25, 100), new(255, 255, 255), "Arial", UiAlignment.Start, new UiTextOverflow(), UiTextFlow.Wrap);
            Assert.Contains(canvas.Texts, item => item.Text == "A" && item.Bounds.Y == 0);
            Assert.Contains(canvas.Texts, item => item.Text == "B" && item.Bounds.Y == 16);
            Assert.Contains(canvas.Texts, item => item.Text == "K" && item.Keys.Contains("key"));
            Assert.Equal(3, Assert.Single(canvas.Buttons).Bounds.X);
            Assert.DoesNotContain(canvas.Requests, ButtonPromptText.Contains);
        });
    }

    [Fact]
    public void SliceClipsCanonicalLeafRangesAndDoesNotOwnUnboundSeparators()
    {
        Trace(() =>
        {
            string text = LocalizationTextComposition.Concat(Bound("left", "Same"), ":", Bound("right", "Same"));
            string sliced = LocalizationTextComposition.Slice(text, 3, 4);
            Assert.Equal("e:Sa", sliced);
            LocalizationTextBinding binding = LocalizationTextTrace.Find(sliced)!;
            Assert.Empty(binding.Key);
            Assert.Equal(new[] { (0, 1, "left"), (2, 2, "right") }, binding.Fragments.Select(fragment => (fragment.Start, fragment.Length, fragment.Binding.Key)));
            Assert.Throws<ArgumentOutOfRangeException>(() => LocalizationTextComposition.Slice(text, 0, 99));
        });
    }

    [Fact]
    public void CompoundPromptWrapPreservesDistinctIdenticalWordingAndTheDynamicButtonLeaf()
    {
        Trace(() =>
        {
            string text = LocalizationTextComposition.Concat(Bound("first", "Same"), " ", ButtonPromptText.Mark(Bound("key", "K")), " ", Bound("last", "Same"));
            Canvas canvas = new();
            ButtonPromptText.Draw(canvas, text, 20, new(10, 10, 95, 120), new(255, 255, 255), "Arial", UiAlignment.Start, new UiTextOverflow(), UiTextFlow.Wrap);
            Drawn first = Assert.Single(canvas.Texts, item => item.Keys.Contains("first"));
            Drawn last = Assert.Single(canvas.Texts, item => item.Keys.Contains("last"));
            Assert.Equal("Same ", first.Text);
            Assert.Equal("Same", last.Text);
            Assert.Equal("Same", OwnedText(first, "first"));
            Assert.Equal("Same", OwnedText(last, "last"));
            Assert.DoesNotContain("last", first.Keys);
            Assert.DoesNotContain("first", last.Keys);
            Assert.Contains(canvas.Texts, item => item.Text == "K" && item.Keys.Contains("key"));
            Assert.Equal(new[] { "key" }, Assert.Single(canvas.Buttons).Keys);
            Assert.True(last.Bounds.Y > first.Bounds.Y);
        });
    }

    [Fact]
    public void ControllerMarkerRemovalPreservesOnlyTheLabelLeafAndTextOutput()
    {
        Trace(() =>
        {
            string text = ButtonPromptText.MarkController(Microsoft.Xna.Framework.Input.Buttons.A, Bound("confirm", "Same"));
            Assert.Equal("Same", ButtonPromptText.PlainText(text));
            Assert.Equal(new[] { "confirm" }, Keys(ButtonPromptText.PlainText(text)));
            DialogueRun run = Assert.Single(ButtonPromptText.Runs(text));
            Assert.True(run.IsButton);
            Assert.Equal(new[] { "confirm" }, Keys(run.Text));
        });
    }

    [Fact]
    public void MetadataPreservationDoesNotChangePromptWrappingOrRunPositions()
    {
        string text = "Same " + ButtonPromptText.Mark("Left Ctrl") + " Same\r\nNext";
        bool previous = LocalizationTextTrace.Enabled;
        LocalizationTextTrace.Enabled = false;
        try
        {
            var baseline = Shape(ButtonPromptText.Lines(text, 20, 90, "Arial"));
            Trace(() =>
            {
                string bound = Bound("template", text);
                Assert.Equal(baseline, Shape(ButtonPromptText.Lines(bound, 20, 90, "Arial")));
            });
        }
        finally
        {
            LocalizationTextTrace.Enabled = previous;
        }
    }

    private static object[] Shape(IEnumerable<DialogueVisualLine> lines) => lines.SelectMany(line => line.Runs.Select(run => (object)(line.SourceLineIndex, line.Width, run.X, run.Run.Text, run.Run.IsButton))).ToArray();
    private static string Bound(string key, string text) => LocalizationTextTrace.Attach(text, new(key, text, text, text));
    private static string OwnedText(Drawn draw, string key) => string.Concat(draw.Binding!.Fragments.Where(fragment => fragment.Binding.Key == key).OrderBy(fragment => fragment.Start).Select(fragment => draw.Text.Substring(fragment.Start, fragment.Length)));
    private static string[] Keys(string text)
    {
        LocalizationTextBinding? binding = LocalizationTextTrace.Find(text);
        return binding is null ? [] : binding.Fragments.Select(fragment => fragment.Binding.Key).Append(binding.Key).Where(key => key.Length != 0).Distinct().Order().ToArray();
    }

    private static void Trace(Action test)
    {
        bool previous = LocalizationTextTrace.Enabled;
        LocalizationTextTrace.Enabled = true;
        try
        {
            test();
        }
        finally
        {
            LocalizationTextTrace.Enabled = previous;
        }
    }

    private sealed record Drawn(string Text, string[] Keys, UiRect Bounds, LocalizationTextBinding? Binding);
    private class Canvas : IUiCanvas, IUiTextFragmentInspector
    {
        public List<Drawn> Texts { get; } = [];
        public List<Drawn> Buttons { get; } = [];
        private readonly Stack<UiRect> clips = new();
        public void InspectTextFragment(string value, UiRect bounds) => Buttons.Add(new(value, Keys(value), bounds.Intersect(clips.Peek()), LocalizationTextTrace.Find(value)));
        public void Text(string text, float fontSize, UiRect bounds, UiColor color) => Texts.Add(new(text, Keys(text), bounds.Intersect(clips.Peek()), LocalizationTextTrace.Find(text)));
        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment alignment) => Text(text, fontSize, bounds, color);
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
        public void PushClip(UiRect bounds) => clips.Push(clips.Count == 0 ? bounds : bounds.Intersect(clips.Peek()));
        public void PopClip() => clips.Pop();
    }

    private sealed class NativeCanvas : Canvas, INativeButtonTextMetrics
    {
        private readonly NativeFontProfile profile = new()
        {
            Id = "synthetic",
            DisplayName = "Synthetic native prompt",
            LineHeight = 16,
            Atlases = [new("atlas", "atlas.png", 32, 16)],
            Images = [new(1, "atlas", 0, 0, 5, 8, 3), new(2, "atlas", 5, 0, 8, 8, 7), new(3, "atlas", 13, 0, 4, 8, 4)],
            Glyphs = [new("A", 1), new("K", 2), new("B", 3)]
        };
        public List<string> Requests { get; } = [];
        public bool UsesNativeButtonMetrics => true;
        public float NativeButtonAdvance(string value, float size) => Layout(value).FinalAdvance * size / 16;
        public UiSize NativeButtonMetrics(string value, float size)
        {
            NativeFontLayoutResult layout = Layout(value);
            return new(layout.Width * size / 16, layout.Height * size / 16);
        }
        private NativeFontLayoutResult Layout(string value)
        {
            Requests.Add(value);
            NativeFontLayoutResult layout = NativeFontLayout.Layout(profile, value, new("Classic", "System"));
            Assert.True(layout.Success, string.Join("; ", layout.Diagnostics.Select(diagnostic => diagnostic.Message)));
            return layout;
        }
    }
}
