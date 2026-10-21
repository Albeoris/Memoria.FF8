using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class WindowCaptionTests
{
    [Theory]
    [InlineData(640, 448, 24, 100)]
    [InlineData(960, 672, 24, 150)]
    [InlineData(1920, 1080, 48, 150)]
    public void MenuWindowCaptionsMatchJunctionHelpAtEveryViewportAndTextScale(int width, int height, int fontSize, int textScale)
    {
        UiPreferences preferences = new() { UiFontSize = fontSize, TextScale = textScale };
        float viewportScale = Math.Min(width / 960f, height / 672f);
        float expected = 13 * viewportScale * fontSize / 24f * textScale / 100f;
        foreach (string screen in new MenuController().ScreenIds)
        {
            MenuController menus = new();
            menus.ConnectPreferences(() => preferences, _ => { });
            menus.Open(screen, demo: true);
            menus.Data.Settings["Text scale"] = textScale + "%";
            if (screen == "magazine")
            {
                MagazineMenuFixture.AddOwnedPage(menus);
            }
            UiView view = menus.BuildView();
            view.Resize(width, height);
            CaptionCanvas canvas = new();
            view.Draw(canvas, new UiTheme { FontSize = 48 });
            Assert.All(canvas.Captions, caption => Assert.True(Math.Abs(caption.Size - expected) < 0.001f, $"{screen}: {caption.Text} has caption size {caption.Size}, expected {expected}."));
            if (screen == "junction")
            {
                Assert.Contains(canvas.Captions, caption => caption.Text == "HELP");
            }
        }
    }

    [Fact]
    public void DefaultCaptionsUseJunctionSizeAndExplicitCaptionSizesRemainScalable()
    {
        CaptionCanvas canvas = new();
        UiTheme theme = new() { FontSize = 48 };
        theme.Panel(canvas, new UiRect(0, 20, 200, 100), "Default");
        UiPanel panel = new() { Caption = "Panel" };
        UiView view = new(panel) { DesignWidth = 200, DesignHeight = 100 };
        view.Resize(400, 200);
        view.Draw(canvas, theme);
        theme.Caption(canvas, new UiRect(0, 20, 200, 100), "Scaled", 19.5f);

        Assert.Equal(new[] { ("Default", 13f), ("Panel", 26f), ("Scaled", 19.5f) }, canvas.Captions);
    }

    private sealed class CaptionCanvas : IUiCanvas
    {
        public List<(string Text, float Size)> Captions { get; } = [];

        public UiSize MeasureText(string text, float fontSize, float maximumWidth)
        {
            float width = Math.Max(1, maximumWidth);
            string[] lines = text.Split('\n');
            int count = lines.Sum(line => Math.Max(1, (int)MathF.Ceiling(line.Length * fontSize * 0.6f / width)));
            return new UiSize(Math.Min(width, lines.Max(line => line.Length) * fontSize * 0.6f), count * fontSize * 1.5f);
        }

        public void OutlinedText(string text, float fontSize, UiRect bounds, UiColor color, UiColor outline, float thickness)
        {
            Captions.Add((text, fontSize));
        }

        public void Fill(UiRect bounds, UiColor color)
        {
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color)
        {
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment)
        {
        }

        public void Image(UiImageSource source, UiRect bounds)
        {
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
