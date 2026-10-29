using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Text;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class NativeVectorMenuLayoutTests
{
    private static readonly NativeTextContext Context = new("Classic", "Menus", DictionaryLanguage: "en");

    [Fact]
    public void SyntheticNativeMetricsFitTheActualVectorMenuAndPreserveTrueOverflowDiagnostics()
    {
        NativeFontProfile profile = SyntheticProfile();
        AssertMenuFits(profile, Context);
        RecordingCanvas canvas = new(profile, Context);
        canvas.Text("A", 16, new(0, 0, 11, 11), new UiColor(255, 255, 255));
        Assert.Single(canvas.Errors);
    }

    [Fact]
    public void NativePromptPaintBoundsDoNotChangePenPlacementOrWrapping()
    {
        NativeFontProfile profile = SyntheticProfile();
        RecordingCanvas canvas = new(profile, Context);
        string value = "AA" + ButtonPromptText.Mark("B") + "AA ";
        DialogueVisualLine line = Assert.Single(ButtonPromptText.Lines(value, 16, 500, "Arial", text => canvas.NativeButtonAdvance(text, 16), 16));
        UiSize measured = ButtonPromptText.Measure(value, 16, 500, "Arial", text => canvas.NativeButtonAdvance(text, 16), 16, text => canvas.NativeButtonMetrics(text, 16).Width);
        ButtonPromptText.Draw(canvas, value, 16, new(0, 0, measured.Width, 16), new UiColor(255, 255, 255), "Arial", UiAlignment.Start, new UiTextOverflow(), UiTextFlow.SingleLine);
        Assert.Empty(canvas.Errors);
        var tail = Assert.Single(canvas.Draws, draw => draw.Text == "AA ");
        Assert.Equal(line.Runs.Last().X, tail.Bounds.X);
        Assert.True(measured.Width >= tail.Bounds.X + canvas.NativeButtonMetrics("AA ", 16).Width);
        DialogueVisualLine after = Assert.Single(ButtonPromptText.Lines(value, 16, 500, "Arial", text => canvas.NativeButtonAdvance(text, 16), 16));
        Assert.Equal(line.Width, after.Width);
        Assert.Equal(line.Runs.Select(run => run.X), after.Runs.Select(run => run.X));
    }

    private static NativeFontProfile SyntheticProfile() => NativeInstalledFontProfile.CreateEuropean("synthetic", "Synthetic", "en", Context, "font.tex", new Albeoris.Games.FF8.Imaging.PixelImage(256, 128), new Albeoris.Games.FF8.Imaging.Tdw.TdwFile(Enumerable.Repeat((byte)7, 200).ToArray(), null).Write());

    [Fact]
    public void InstalledNativeMetricsFitTheActualVectorMenuDrawAllocations()
    {
        using IResourceSource source = GameDataSourceFactory.Open(TestResources.GameDirectory);
        ResourceService resources = new(source);
        NativeTextContext context = new(resources.Source.Edition.ToString(), "Menus", DictionaryLanguage: resources.ResourceLanguage);
        var atlas = NativeInstalledFontProfile.DecodeAtlas(resources.Read(new("menu/sysfnt.tex")), "font.tex");
        NativeFontProfile profile = NativeInstalledFontProfile.CreateEuropean("installed", "Installed", resources.ResourceLanguage, context, "font.tex", atlas, resources.Read(new("main/sysfnt.tdw")));
        AssertMenuFits(profile, context);
    }

    private static void AssertMenuFits(NativeFontProfile profile, NativeTextContext context)
    {
        MenuController menu = new();
        menu.Open("main", demo: true);
        UiView view = menu.BuildView();
        view.Resize(3440, 1334);
        RecordingCanvas canvas = new(profile, context);
        view.Draw(canvas, menu.CreateTheme());
        Assert.True(canvas.Errors.Count == 0, string.Join(Environment.NewLine, canvas.Errors));
        Assert.Contains(canvas.Draws, draw => draw.Text == "Squall");
        Assert.Contains(canvas.Draws, draw => draw.Text == "12:34");
        Assert.Contains(canvas.Draws, draw => draw.Text.Contains("Navigate"));
    }

    private sealed class RecordingCanvas(NativeFontProfile profile, NativeTextContext context) : IUiCanvas, INativeButtonTextMetrics
    {
        public List<string> Errors { get; } = [];
        public List<(string Text, UiRect Bounds)> Draws { get; } = [];
        public bool UsesNativeButtonMetrics => true;
        public float NativeButtonAdvance(string value, float size) => NativeFontLayout.Layout(profile, value, context).FinalAdvance * size / profile.LineHeight;
        public UiSize NativeButtonMetrics(string value, float size)
        {
            NativeFontLayoutResult layout = NativeFontLayout.Layout(profile, value, context);
            return new(layout.Width * size / profile.LineHeight, layout.Height * size / profile.LineHeight);
        }

        public UiSize MeasureText(string text, float size, float width)
        {
            if (ButtonPromptText.Contains(text))
            {
                return ButtonPromptText.Measure(text, size, width, "Arial", value => NativeButtonAdvance(value, size), NativeButtonMetrics("", size).Height, value => NativeButtonMetrics(value, size).Width);
            }
            float scale = size / profile.LineHeight;
            NativeFontLayoutResult layout = NativeFontLayout.Layout(profile, text, context, width / scale);
            return new(layout.Width * scale, layout.Height * scale);
        }
        public void Text(string text, float size, UiRect bounds, UiColor color) => Text(text, size, bounds, color, UiAlignment.Start);
        public void Text(string text, float size, UiRect bounds, UiColor color, UiAlignment alignment)
        {
            if (ButtonPromptText.Contains(text))
            {
                ButtonPromptText.Draw(this, text, size, bounds, color, "Arial", alignment, new UiTextOverflow(), UiTextFlow.SingleLine);
                return;
            }
            Draws.Add((text, bounds));
            float scale = size / profile.LineHeight;
            NativeFontLayoutResult layout = NativeFontLayout.Layout(profile, text, context, bounds.Width / scale, bounds.Height / scale);
            if (layout.Diagnostics.Any(error => error.Code == "overflow"))
            {
                NativeFontLayoutResult unbounded = NativeFontLayout.Layout(profile, text, context);
                float right = unbounded.Glyphs.Select(glyph => glyph.X + glyph.Image.Width).DefaultIfEmpty().Max();
                float bottom = unbounded.Glyphs.Select(glyph => glyph.Y + glyph.Image.Height).DefaultIfEmpty().Max();
                Errors.Add($"'{text}' size={size:R} bounds={bounds} logical={bounds.Width / scale:R}x{bounds.Height / scale:R} ink={right:R}x{bottom:R} advance={unbounded.FinalAdvance:R}");
            }
        }
        public void MenuPointer(UiRect bounds)
        {
        }
        public void Image(UiImageSource image, UiRect bounds)
        {
        }
        public void Fill(UiRect bounds, UiColor color)
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

