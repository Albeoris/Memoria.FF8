using System.Buffers.Binary;
using System.Drawing;
using System.Drawing.Imaging;
using Albeoris.Games.FF8.TextEncoding.Tags;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class NativeTutorialTextRenderTests
{
    [Theory]
    [InlineData(TextTagKey.JunctionMarker, 192)]
    [InlineData(TextTagKey.JunctionAbility, 216)]
    [InlineData(TextTagKey.CommandAbility, 217)]
    [InlineData(TextTagKey.ActionAbility, 218)]
    [InlineData(TextTagKey.CharacterAbility, 219)]
    [InlineData(TextTagKey.PartyAbility, 220)]
    [InlineData(TextTagKey.GuardianForceAbility, 221)]
    [InlineData(TextTagKey.MenuAbility, 222)]
    [InlineData(TextTagKey.FireElement, 288)]
    [InlineData(TextTagKey.IceElement, 289)]
    [InlineData(TextTagKey.ThunderElement, 290)]
    [InlineData(TextTagKey.EarthElement, 291)]
    [InlineData(TextTagKey.PoisonElement, 292)]
    [InlineData(TextTagKey.WindElement, 293)]
    [InlineData(TextTagKey.WaterElement, 294)]
    [InlineData(TextTagKey.HolyElement, 295)]
    public void NamedAndNumericTagsDrawTheOriginalIconWithoutLiteralTagText(TextTagKey key, int icon)
    {
        foreach (string parameter in new[] { key.ToString(), ((byte)key).ToString(System.Globalization.CultureInfo.InvariantCulture) })
        {
            NativeTutorialTextElement element = new($"Before {{Key {parameter}}} After", 384, null);
            element.Arrange(new UiRect(10, 20, 768, 512));
            RecordingCanvas canvas = new();

            element.Draw(canvas, new UiTheme());

            var image = Assert.Single(canvas.Images);
            Assert.Equal(new UiImageSource(UiImageKind.Icon, icon), image.Source);
            Assert.Equal(32, image.Bounds.Width);
            Assert.Equal(32, image.Bounds.Height);
            Assert.Equal(20, image.Bounds.Y);
            Assert.Equal(new[] { "Before ", "After" }, canvas.Texts.Select(text => text.Value));
            Assert.All(canvas.Texts, text => Assert.DoesNotContain("{Key", text.Value));
        }
    }

    [Theory]
    [InlineData(640, 480)]
    [InlineData(1280, 720)]
    public void OriginalStatsColumnsKeepNineAndFourSingleLineRowsOnTheArtworkGrid(int width, int height)
    {
        using GameAssets assets = InstalledAssets();
        UiView view = PageView(assets, 66, width, height, out NativeTutorialBrowserPage page);
        UiImage artwork = Assert.Single(Descendants(view.Root).OfType<UiImage>(), image => image.Source?.Kind == UiImageKind.NativeTutorialPage);
        NativeTutorialTextElement[] labels = Descendants(view.Root).OfType<NativeTutorialTextElement>().ToArray();
        NativeTutorialTextElement left = Assert.Single(labels, label => label.Text.Split('\n').Count(line => !string.IsNullOrWhiteSpace(line)) == 9);
        NativeTutorialTextElement right = Assert.Single(labels, label => label.Text.Split('\n').Count(line => !string.IsNullOrWhiteSpace(line)) == 4);
        Assert.True(left.Bounds.X < right.Bounds.X);
        foreach ((NativeTutorialTextElement column, int count) in new[] { (left, 9), (right, 4) })
        {
            NativeTutorialPageText placement = Assert.Single(page.Placements, placement => column.Id == $"native-tutorial-text-{placement.TextIndex}");
            int originX = BinaryPrimitives.ReadInt16LittleEndian(page.Metadata.AsSpan());
            int originY = BinaryPrimitives.ReadInt16LittleEndian(page.Metadata.AsSpan().Slice(2, 2));
            Assert.Equal(artwork.Bounds.X + artwork.Bounds.Width * (originX + placement.X) / 384, column.Bounds.X, precision: 3);
            Assert.Equal(artwork.Bounds.Y + artwork.Bounds.Height * (originY + placement.Y) / 256, column.Bounds.Y, precision: 3);
            Assert.Equal(count, column.Children.Count);
            float rowHeight = artwork.Bounds.Height * 16 / 256;
            RecordingCanvas canvas = new();
            column.Draw(canvas, new UiTheme());
            Assert.Equal(count, canvas.Texts.Count);
            for (int row = 0; row < count; row++)
            {
                Assert.Equal(column.Bounds.Y + row * rowHeight, column.Children[row].Bounds.Y, precision: 3);
                Assert.Equal(rowHeight, column.Children[row].Bounds.Height, precision: 3);
                Assert.DoesNotContain('\n', canvas.Texts[row].Value);
            }
        }
    }

    [Fact]
    public void RepresentativeOriginalPagesRenderThroughTheSoftwareCanvas()
    {
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_TUTORIAL_RENDER_DIR");
        Assert.SkipUnless(output is not null, "Set MEMORIA_FF8_TUTORIAL_RENDER_DIR to save original tutorial page render evidence.");
        Directory.CreateDirectory(output!);
        using GameAssets assets = InstalledAssets();
        foreach ((int width, int height) in new[] { (640, 480), (1280, 720) })
        {
            foreach (int id in Enumerable.Range(64, 4))
            {
                UiView view = PageView(assets, id, width, height, out _);
                using Bitmap bitmap = new(width, height);
                using (BattlePreviewCanvas canvas = new(bitmap, assets.GameDirectory))
                {
                    view.Draw(canvas, new UiTheme());
                }
                bitmap.Save(Path.Combine(output!, $"tutorial-{id}-{width}x{height}.png"), ImageFormat.Png);
            }
        }
    }

    private static UiView PageView(GameAssets assets, int id, int width, int height, out NativeTutorialBrowserPage page)
    {
        NativeTutorialBrowserCatalog browser = assets.ReadNativeTutorialBrowserCatalog();
        NativeTutorialBrowserCategory category = Assert.Single(browser.Categories, category => category.Pages.Any(page => page.Id == id));
        MenuController menu = new();
        menu.ConnectNativeTutorials(assets.ReadNativeTutorialCatalog(), browser, () => new HashSet<int>(), _ => throw new InvalidOperationException("A static page cannot start a demo."));
        menu.Open("tutorial");
        menu.SelectEntry("tutorial", $"category-{category.Id}");
        menu.Navigate(MenuInput.Confirm);
        while (menu.CurrentNativeTutorialPage!.Id != id)
        {
            menu.Navigate(MenuInput.Right);
        }
        page = menu.CurrentNativeTutorialPage!;
        UiView view = menu.BuildView();
        view.Resize(width, height);
        return view;
    }

    private static GameAssets InstalledAssets()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(File.Exists(Path.Combine(directory, "Data", "lang-en", "menu.fs")), "Installed original tutorial resources are required.");
        return new GameAssets(directory);
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
        public List<(string Value, UiRect Bounds)> Texts { get; } = [];
        public List<(UiImageSource Source, UiRect Bounds)> Images { get; } = [];

        public void Text(string text, float fontSize, UiRect bounds, UiColor color)
        {
            Texts.Add((text, bounds));
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment)
        {
            Text(text, fontSize, bounds, color);
        }

        public void Image(UiImageSource source, UiRect bounds)
        {
            Images.Add((source, bounds));
        }

        public void Fill(UiRect bounds, UiColor color)
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
