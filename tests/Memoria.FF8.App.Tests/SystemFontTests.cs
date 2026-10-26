using System.Globalization;
using System.Security.Cryptography;
using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class SystemFontTests
{
    [Fact]
    public void Dialogue_keyboard_bindings_rasterize_as_distinct_glyphs_at_dialogue_size()
    {
        string[] buttons = ["Triangle", "Square", "Circle", "Cross"];
        using var missing = SystemTextLayout.Rasterize("\U0010FFFF", 8, float.PositiveInfinity, "Arial");
        List<string> signatures = [];
        foreach (string button in buttons)
        {
            using var bitmap = SystemTextLayout.Rasterize(DialogueTextFormatter.Format($"{{Key {button}}}"), 8, float.PositiveInfinity, "Arial");
            Assert.Contains(SystemTextLayout.PremultipliedPixels(bitmap), pixel => pixel.A > 0);
            Assert.NotEqual(Signature(missing), Signature(bitmap));
            signatures.Add(Signature(bitmap));
        }
        Assert.Equal(4, signatures.Distinct().Count());
    }

    [Theory]
    [InlineData("Arial")]
    [InlineData("Segoe UI Emoji")]
    public void Supplementary_emoji_are_distinct_real_glyphs_with_selected_font_and_os_fallback(string family)
    {
        Assert.Contains("Segoe UI Emoji", SystemFontCatalog.Families);
        using var woman = SystemTextLayout.Rasterize("👩", 32, float.PositiveInfinity, family);
        using var rocket = SystemTextLayout.Rasterize("🚀", 32, float.PositiveInfinity, family);
        using var missing = SystemTextLayout.Rasterize("\U0010FFFF", 32, float.PositiveInfinity, family);
        using var joined = SystemTextLayout.Rasterize("👩🏽‍🚀", 32, float.PositiveInfinity, family);
        using var squares = SystemTextLayout.Rasterize("□□□", 32, float.PositiveInfinity, family);
        Assert.True(woman.Width > 1 && woman.Height > 1);
        Assert.True(rocket.Width > 1 && rocket.Height > 1);
        Assert.NotEqual(Signature(woman), Signature(rocket));
        Assert.NotEqual(Signature(woman), Signature(missing));
        Assert.NotEqual(Signature(rocket), Signature(missing));
        Assert.NotEqual(Signature(joined), Signature(squares));
    }

    private static string Signature(SkiaSharp.SKBitmap bitmap)
    {
        byte[] alpha = SystemTextLayout.PremultipliedPixels(bitmap).Select(pixel => pixel.A).ToArray();
        return $"{bitmap.Width}x{bitmap.Height}:" + Convert.ToHexString(SHA256.HashData(alpha));
    }

    [Theory]
    [InlineData("𐐀𐐁𐐂")]
    [InlineData("e\u0301a\u0308o\u0302")]
    [InlineData("👩🏽‍🚀👨‍👩‍👧‍👦🇩🇪")]
    [InlineData("क्‍षक्‍षक्‍ष")]
    public void Wrapping_keeps_extended_graphemes_intact_even_when_one_exceeds_the_limit(string value)
    {
        string[] elements = Elements(value);
        List<string> lines = SystemTextLayout.Wrap(value, candidate => candidate.Length, 1);
        Assert.Equal(elements, lines);
        Assert.Equal(value, string.Concat(lines));
    }

    [Fact]
    public void Wrapping_measures_shaped_candidates_and_preserves_explicit_blank_lines()
    {
        List<string> measured = [];
        List<string> lines = SystemTextLayout.Wrap("سلام عالم\n\n日本語", candidate =>
        {
            measured.Add(candidate);
            return Elements(candidate).Length;
        }, 4);
        Assert.Equal(new[] { "سلام", "عالم", "", "日本語" }, lines);
        Assert.Contains("سلام عالم", measured);
    }

    [Fact]
    public void Full_line_raster_retains_blank_space_and_common_baseline_metrics()
    {
        string family = SystemFontCatalog.DefaultFamily;
        using var capitals = SystemTextLayout.Rasterize("É", 20, float.PositiveInfinity, family, preserveLineMetrics: true);
        using var descenders = SystemTextLayout.Rasterize("gyp", 20, float.PositiveInfinity, family, preserveLineMetrics: true);
        using var blank = SystemTextLayout.Rasterize(" ", 20, float.PositiveInfinity, family, preserveLineMetrics: true);
        Assert.Equal(capitals.Height, descenders.Height);
        Assert.Equal(capitals.Height, blank.Height);
        Assert.True(SystemTextLayout.MeasureAdvance("A ", 20, family) > SystemTextLayout.MeasureAdvance("A", 20, family));
        Assert.True(SystemTextLayout.LineHeight(30, family) > SystemTextLayout.LineHeight(20, family));
        using SystemTextMeasurer measurer = SystemTextLayout.CreateMeasurer(20, family);
        Assert.Equal(SystemTextLayout.MeasureAdvance("A ", 20, family), measurer.Measure("A "));
        Assert.Equal(SystemTextLayout.LineHeight(20, family), measurer.LineHeight);
        Assert.All(SystemTextLayout.PremultipliedPixels(blank), pixel => Assert.Equal(0, pixel.A));
    }

    [Fact]
    public void Font_preferences_are_independent_and_round_trip_as_local_preferences()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ff8-font-settings-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "ui-settings.json");
        try
        {
            string family = SystemFontCatalog.Families.First(family => family != SystemFontCatalog.DefaultFamily);
            UiPreferences preferences = new UiPreferences().WithSetting("UI font", family).WithSetting("UI font size", "28").WithSetting("Dialogue font size", "10");
            Assert.Equal(SystemFontCatalog.DefaultFamily, preferences.DialogueFontFamily);
            Assert.Equal(28, preferences.UiFontSize);
            Assert.Equal(10, preferences.DialogueFontSize);
            preferences.Save(path);
            Assert.Equal(preferences, UiPreferences.Load(path));
            Assert.Throws<ArgumentException>(() => preferences.WithSetting("UI font", "A nonexistent FF8 test font"));
            Assert.Throws<ArgumentException>(() => preferences.WithSetting("Dialogue font size", "0"));
            File.WriteAllText(path, "{\"UiFontFamily\":\"\"}");
            Assert.Throws<InvalidDataException>(() => UiPreferences.Load(path));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    [Fact]
    public void Settings_offer_installed_families_and_both_live_unicode_previews()
    {
        UiPreferences preferences = new() { DialogueFontFamily = SystemFontCatalog.Families.First(family => family != SystemFontCatalog.DefaultFamily) };
        MenuData data = new();
        data.SupportedActions.Add("config");
        MenuController menus = new(() => data, action => preferences = preferences.WithSetting(action.SubjectId, action.Value));
        menus.ConnectPreferences(() => preferences, action => preferences = preferences.WithSetting(action.SubjectId, action.Value));
        menus.Open("config-text", false);
        Assert.Contains(menus.Entries, entry => entry.Id == "UI font");
        Assert.Contains(menus.Entries, entry => entry.Id == "Dialogue font");
        Assert.All(MenuSettings.Definitions.Single(setting => setting.Id == "UI font").Values, family => Assert.Contains(family, SystemFontCatalog.Families));
        menus.Execute(new MenuAction("config", "Dialogue font size", Value: "12"));
        menus.Execute(new MenuAction("config", "UI font size", Value: "28"));
        UiView view = menus.BuildView();
        FontPreviewElement[] previews = Descendants(view.Root).OfType<FontPreviewElement>().ToArray();
        Assert.Equal(2, previews.Length);
        Assert.Equal(28, previews.Single(preview => preview.Id == "ui-font-preview").FontSize);
        Assert.Equal(36, previews.Single(preview => preview.Id == "dialogue-font-preview").FontSize);
        Assert.Equal(28 / 24f, menus.TextScale);
        view.Resize(1920, 1344);
        FontCanvas canvas = new();
        view.Draw(canvas, new UiTheme());
        Assert.Contains(canvas.Samples, sample => sample.Family == preferences.UiFontFamily && sample.Size == 56);
        Assert.Contains(canvas.Samples, sample => sample.Family == preferences.DialogueFontFamily && sample.Size == 72);
    }

    [Fact]
    public void Ordinary_config_changes_current_skin_font_and_displays_selected_family()
    {
        UiPreferences preferences = new();
        MenuData ReadData()
        {
            MenuData data = new();
            data.Settings["UI font"] = preferences.UiFontFamily;
            data.Settings["UI font size"] = preferences.UiFontSize.ToString();
            data.Settings["Dialogue font"] = preferences.DialogueFontFamily;
            data.Settings["Dialogue font size"] = preferences.DialogueFontSize.ToString();
            foreach (string key in new[] { "UI font", "UI font size", "Dialogue font", "Dialogue font size" })
            {
                data.SupportedActions.Add("config:" + key);
            }
            return data;
        }
        MenuController menus = new(ReadData, action => preferences = preferences.WithSetting(action.SubjectId, action.Value));
        menus.ConnectPreferences(() => preferences, action => preferences = preferences.WithSetting(action.SubjectId, action.Value));
        menus.Open("config-text", false);
        int fontIndex = menus.Entries.Select((entry, index) => (entry, index)).Single(pair => pair.entry.Id == "UI font").index;
        for (int index = 0; index < fontIndex; index++)
        {
            menus.Navigate(MenuInput.Down);
        }
        string before = preferences.UiFontFamily;
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal("font-picker", menus.ScreenId);
        string nextFamily = SystemFontCatalog.Families.First(family => family != before);
        menus.SelectEntry("font-picker", "family:" + nextFamily);
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal(nextFamily, preferences.UiFontFamily);
        Assert.Equal("config-text", menus.ScreenId);
        Assert.Equal(preferences.UiFontFamily, menus.CreateTheme().FontFamily);
    }

    private sealed class FontCanvas : IUiCanvas
    {
        public List<(string? Family, float Size)> Samples { get; } = [];

        public void Text(string text, float fontSize, UiRect bounds, UiColor color)
        {
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color, string? fontFamily)
        {
            Samples.Add((fontFamily, fontSize));
        }

        public void Fill(UiRect bounds, UiColor color)
        {
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment)
        {
            Text(text, fontSize, bounds, color);
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

    private static string[] Elements(string value)
    {
        TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(value);
        List<string> elements = [];
        while (enumerator.MoveNext())
        {
            elements.Add(enumerator.GetTextElement());
        }
        return elements.ToArray();
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
}
