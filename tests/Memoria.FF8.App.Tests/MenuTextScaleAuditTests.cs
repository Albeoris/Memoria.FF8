using Bitmap = SkiaSharp.SKBitmap;
using Color = SkiaSharp.SKColor;

using Memoria.FF8.Runtime;
using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class MenuTextScaleAuditTests
{
    [Theory]
    [InlineData(6, 100)]
    [InlineData(6, 150)]
    [InlineData(20, 125)]
    [InlineData(24, 100)]
    [InlineData(24, 150)]
    [InlineData(48, 100)]
    [InlineData(48, 150)]
    public void RegisteredMenusDrawAtSupportedFontAndTextScaleExtremes(int fontSize, int textScale)
    {
        foreach (string screen in new MenuController().ScreenIds)
        {
            MenuController controller = Create(screen, fontSize, textScale);
            if (screen == "magazine")
            {
                MagazineMenuFixture.AddOwnedPage(controller);
            }
            UiView view = controller.BuildView();
            AuditCanvas canvas = new();
            view.Draw(canvas, controller.CreateTheme());
            view.Resize(1280, 720);
            view.Draw(canvas, controller.CreateTheme());
            Assert.Equal(0, canvas.ClipDepth);
            Assert.True(canvas.TextCount > 0, screen);
            Assert.All(Descendants(view.Root), element => Validate(element.Bounds));
        }
    }

    [Theory]
    [InlineData(20, 125)]
    [InlineData(24, 100)]
    [InlineData(24, 150)]
    [InlineData(48, 150)]
    public void RegisteredMenusRenderWithInstalledArtworkAtDefaultAndLargeSizes(int fontSize, int textScale)
    {
        string gameDirectory = LaunchOptions.ConfiguredGameDirectory;
        if (string.IsNullOrEmpty(gameDirectory))
        {
            Assert.Skip("Configure MEMORIA_FF8_GAME_DIR for installed menu rendering.");
        }
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_MENU_UI_OUTPUT");
        using Bitmap bitmap = new(1280, 720);
        using BattlePreviewCanvas canvas = new(bitmap, gameDirectory);
        foreach (string screen in new MenuController().ScreenIds)
        {
            MenuController controller = Create(screen, fontSize, textScale);
            if (screen == "magazine")
            {
                MagazineMenuFixture.AddOwnedPage(controller);
            }
            UiView view = controller.BuildView();
            view.Resize(bitmap.Width, bitmap.Height);
            foreach (int seconds in new[] { 0, 3 })
            {
                canvas.BeginFrame(TimeSpan.FromSeconds(seconds));
                canvas.Fill(new UiRect(0, 0, bitmap.Width, bitmap.Height), new UiColor(0, 0, 0));
                view.Draw(canvas, controller.CreateTheme());
                if (!string.IsNullOrEmpty(output))
                {
                    string directory = Path.Combine(output, $"font{fontSize}-scale{textScale}-time{seconds}");
                    Directory.CreateDirectory(directory);
                    bitmap.SavePng(Path.Combine(directory, screen + ".png"));
                }
            }
        }
    }

    [Fact]
    public void SaveMetadataAndHelpUseWrappedLabelsWithoutSingleLineHeightLimits()
    {
        MenuController controller = Create("save", 48, 150);
        controller.Data.SaveSlots[0].Summary = "First metadata line\nSecond metadata line\nThird metadata line";
        UiView view = controller.BuildView();
        UiLabel metadata = Assert.Single(Descendants(view.Root).OfType<UiLabel>(), label => label.Text == controller.Data.SaveSlots[0].Summary);
        Assert.Equal(UiTextFlow.Wrap, metadata.TextFlow);
        Assert.Null(metadata.Height);
        Assert.All(Descendants(view.Root).OfType<UiPanel>().Where(panel => panel.Caption == "HELP").SelectMany(panel => Descendants(panel).OfType<UiLabel>()), label =>
        {
            Assert.Equal(UiTextFlow.Wrap, label.TextFlow);
            Assert.Null(label.Height);
        });
        view.Draw(new AuditCanvas(), controller.CreateTheme());
    }

    [Theory]
    [InlineData("shop-buy")]
    [InlineData("shop-sell")]
    [InlineData("shop-junk")]
    [InlineData("gf-learn")]
    [InlineData("card")]
    [InlineData("config-keyboard")]
    public void QuantityProgressPriceAndBindingColumnsReserveTextWidth(string screen)
    {
        UiView view = Create(screen, 48, 150).BuildView();
        UiList[] columns = Descendants(view.Root).OfType<UiList>().Where(list => list.SecondaryItems.Count > 0).ToArray();
        Assert.NotEmpty(columns);
        Assert.All(columns, list =>
        {
            if (screen == "config-keyboard")
            {
                Assert.False(list.AutoSecondaryWidth);
                Assert.Equal(0.7f, list.SecondaryWidth);
            }
            else
            {
                Assert.True(list.AutoSecondaryWidth, screen + ": " + list.Id);
            }
        });
    }

    [Theory]
    [InlineData("config-gameplay")]
    [InlineData("config-interface")]
    [InlineData("new-game-options")]
    public void LargeConfigTextHasSeparateColumnsAndFewerRows(string screen)
    {
        MenuController controller = Create(screen, 48, 150);
        UiView view = controller.BuildView();
        UiSettingRow[] rows = Descendants(view.Root).OfType<UiSettingRow>().ToArray();
        Assert.InRange(rows.Length, 1, 3);
        Assert.All(rows, row => Assert.True(row.ValueBounds.X - row.LabelBounds.Right >= 35));
        controller.Navigate(MenuInput.PageDown);
        Assert.True(controller.SelectedIndex > 0);
        UiView next = controller.BuildView();
        Assert.All(Descendants(next.Root).OfType<UiSettingRow>(), row => Assert.True(row.LabelBounds.Width > 0 && row.ValueBounds.Width > 0));
    }

    [Theory]
    [InlineData("gf-learn")]
    [InlineData("status")]
    [InlineData("card")]
    [InlineData("shop-buy")]
    [InlineData("save")]
    [InlineData("tutorial")]
    [InlineData("debug-room")]
    public void LargeHelpPanelsReserveSpaceForTextBelowTheirCaptions(string screen)
    {
        UiView view = Create(screen, 48, 150).BuildView();
        UiLabel[] labels = Descendants(view.Root).OfType<UiPanel>().Where(panel => panel.Caption == "HELP").SelectMany(panel => panel.Children.OfType<UiLabel>()).ToArray();
        Assert.NotEmpty(labels);
        Assert.All(labels, label => Assert.True(label.Bounds.Height >= label.FontSize * 0.5f, $"{screen}: HELP viewport {label.Bounds.Height} is too short for font {label.FontSize}."));
    }

    private static MenuController Create(string screen, int fontSize, int textScale)
    {
        UiPreferences preferences = new() { UiFontSize = fontSize, TextScale = textScale };
        MenuController controller = new();
        controller.ConnectPreferences(() => preferences, _ => { });
        controller.Open(screen, demo: true);
        controller.Data.Settings["Text scale"] = textScale + "%";
        return controller;
    }

    private static IEnumerable<UiElement> Descendants(UiElement root)
    {
        yield return root;
        foreach (UiElement child in root.Children)
        {
            foreach (UiElement descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static void Validate(UiRect bounds)
    {
        Assert.True(float.IsFinite(bounds.X) && float.IsFinite(bounds.Y) && float.IsFinite(bounds.Width) && float.IsFinite(bounds.Height));
        Assert.True(bounds.Width >= 0 && bounds.Height >= 0);
    }

    private sealed class AuditCanvas : IUiCanvas
    {
        public int ClipDepth { get; private set; }
        public int TextCount { get; private set; }

        public UiSize MeasureText(string text, float fontSize, float maximumWidth)
        {
            float width = Math.Max(1, maximumWidth);
            string[] lines = text.Split('\n');
            int count = lines.Sum(line => Math.Max(1, (int)MathF.Ceiling(line.Length * fontSize * 0.6f / width)));
            return new UiSize(Math.Min(width, lines.Max(line => line.Length) * fontSize * 0.6f), count * fontSize * 1.5f);
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color)
        {
            Validate(bounds);
            Assert.True(float.IsFinite(fontSize) && fontSize > 0);
            TextCount++;
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment)
        {
            Text(text, fontSize, bounds, color);
        }

        public void Fill(UiRect bounds, UiColor color)
        {
            Validate(bounds);
        }

        public void Image(UiImageSource source, UiRect bounds)
        {
            Validate(bounds);
        }

        public void MenuPointer(UiRect bounds)
        {
            Validate(bounds);
        }

        public void Line(float x1, float y1, float x2, float y2, UiColor color, float thickness)
        {
            Assert.True(float.IsFinite(x1) && float.IsFinite(y1) && float.IsFinite(x2) && float.IsFinite(y2));
        }

        public void PushClip(UiRect bounds)
        {
            Validate(bounds);
            ClipDepth++;
        }

        public void PopClip()
        {
            ClipDepth--;
            Assert.True(ClipDepth >= 0);
        }
    }
}
