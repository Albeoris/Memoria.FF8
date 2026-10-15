using System.Globalization;
using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class SystemFontTests
{
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
        MenuController menus = new();
        menus.ConnectPreferences(() => preferences, action => preferences = preferences.WithSetting(action.SubjectId, action.Value));
        menus.Open("debug-ui", true);
        Assert.Contains(menus.Entries, entry => entry.Id == "UI font");
        Assert.Contains(menus.Entries, entry => entry.Id == "Dialogue font");
        Assert.All(MenuSettings.Definitions.Single(setting => setting.Id == "UI font").Values, family => Assert.Contains(family, SystemFontCatalog.Families));
        menus.Execute(new MenuAction("host-config", "Dialogue font size", Value: "12"));
        menus.Execute(new MenuAction("host-config", "UI font size", Value: "28"));
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
    public void Ordinary_config_changes_live_fonts_and_shows_their_previews()
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
        menus.Open("config", false);
        int fontIndex = menus.Entries.Select((entry, index) => (entry, index)).Single(pair => pair.entry.Id == "UI font").index;
        for (int index = 0; index < fontIndex; index++)
        {
            menus.Navigate(MenuInput.Down);
        }
        string before = preferences.UiFontFamily;
        menus.Navigate(MenuInput.Right);
        Assert.NotEqual(before, preferences.UiFontFamily);
        Assert.Contains(preferences.UiFontFamily, menus.SelectedEntry!.Label);
        FontPreviewElement[] previews = Descendants(menus.BuildView().Root).OfType<FontPreviewElement>().ToArray();
        Assert.Equal(2, previews.Length);
        Assert.Equal(preferences.UiFontFamily, previews.Single(preview => preview.Id == "ui-font-preview").FontFamily);
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
