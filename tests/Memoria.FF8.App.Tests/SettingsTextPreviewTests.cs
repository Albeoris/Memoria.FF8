using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class SettingsTextPreviewTests
{
    [Theory]
    [InlineData(640, 448)]
    [InlineData(1280, 720)]
    [InlineData(1920, 1080)]
    [InlineData(2560, 1080)]
    public void MaximumTextPreviewsHaveRoomForTheActualRequestedLineHeight(int width, int height)
    {
        string family = SystemFontCatalog.Families.OrderByDescending(family => family.Length).First();
        foreach (MenuPresentation presentation in Enum.GetValues<MenuPresentation>())
        {
            UiPreferences preferences = new() { MenuPresentation = presentation, TextScale = 150, UiFontSize = 32, ClassicUiFontFamily = family, VectorUiFontFamily = family, DialogueFontFamily = family, DialogueFontSize = 16 };
            MenuData data = MenuDemoData.Create();
            data.SupportedActions.Add("config");
            MenuController menus = new(() => data);
            menus.ConnectPreferences(() => preferences, _ => throw new InvalidOperationException("Preview must not write preferences."));
            menus.Presentation = presentation;
            menus.Open("config-text");
            UiView view = menus.BuildView();
            view.Resize(width, height);
            FontPreviewElement[] previews = Descendants(view.Root).OfType<FontPreviewElement>().ToArray();
            Assert.Equal(2, previews.Length);
            foreach (FontPreviewElement preview in previews)
            {
                Assert.Equal(48, preview.FontSize);
                float lineHeight = SystemTextLayout.LineHeight(preview.FontSize, preview.FontFamily);
                Assert.True(preview.Bounds.Height >= lineHeight, $"{presentation} {width}x{height}: {preview.Id} has {preview.Bounds.Height}px; actual font needs {lineHeight}px.");
            }
        }
    }

    private static IEnumerable<UiElement> Descendants(UiElement parent)
    {
        yield return parent;
        foreach (UiElement child in parent.Children)
        {
            foreach (UiElement descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }
}
