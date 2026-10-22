using System.Drawing;
using System.Drawing.Imaging;
using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class ModelBrowserTests
{
    [Theory]
    [InlineData("Squall field character", "QUALL", true)]
    [InlineData("Squall field character", "squ?ll*character", true)]
    [InlineData("Squall field character", "squ??ll", false)]
    [InlineData("*field model", "model", true)]
    [InlineData("anything", "", true)]
    [InlineData("anything", "***", true)]
    public void Search_supports_case_insensitive_substrings_and_wildcards(string text, string query, bool expected)
    {
        Assert.Equal(expected, ModelBrowserController.MatchesSearch(text, query));
    }

    [Fact]
    public void Filter_searches_metadata_and_retains_selection_when_still_visible()
    {
        ModelBrowserController browser = Create([Entry("one"), Entry("two") with { Details = ["Balamb Garden library"] }]);
        browser.Select(1);
        browser.SetSearch("garden*lib?ary");
        Assert.Equal("two", Assert.Single(browser.Entries).Id);
        Assert.Equal("two", browser.Selected!.Id);
        browser.SetSearch("no matches");
        Assert.Empty(browser.Entries);
        browser.OpenViewer();
        Assert.Equal(ModelBrowserMode.Catalog, browser.Mode);
    }

    [Fact]
    public void Text_input_only_changes_search_when_focused_and_backspace_removes_one_character()
    {
        ModelBrowserController browser = Create([Entry("Squall")]);
        browser.TextInput('q');
        Assert.Equal("", browser.Search);
        browser.Navigate(MenuInput.Tab);
        browser.TextInput('q');
        browser.TextInput('u');
        browser.TextInput('\r');
        Assert.Equal("qu", browser.Search);
        browser.Backspace();
        Assert.Equal("q", browser.Search);
        browser.OpenViewer();
        browser.TextInput('x');
        Assert.Equal("q", browser.Search);
    }

    [Fact]
    public void Viewer_rotates_zooms_steps_the_filtered_list_and_switches_variants()
    {
        ModelBrowserController browser = Create([Entry("one"), Entry("pick-two"), Entry("pick-three")]);
        browser.SetSearch("pick");
        browser.OpenViewer();
        browser.Navigate(MenuInput.Right);
        browser.Navigate(MenuInput.Up);
        browser.Navigate(MenuInput.PageUp);
        Assert.True(browser.RotationY > 0);
        Assert.True(browser.RotationX < 0);
        Assert.True(browser.Zoom > 1);
        browser.Navigate(MenuInput.Alternate);
        Assert.Equal("mod", browser.CurrentVariant!.Id);
        browser.Navigate(MenuInput.PageRight);
        Assert.Equal("pick-three", browser.Selected!.Id);
        Assert.Equal("original", browser.CurrentVariant!.Id);
        Assert.Equal(1f, browser.Zoom);
        browser.Navigate(MenuInput.PageRight);
        Assert.Equal("pick-two", browser.Selected!.Id);
        browser.ZoomBy(float.PositiveInfinity);
        browser.ZoomBy(1000);
        Assert.Equal(10f, browser.Zoom);
        browser.ZoomBy(0.00001f);
        Assert.Equal(0.1f, browser.Zoom);
    }

    [Fact]
    public void Commands_use_the_selected_variant_and_cancel_unwinds_each_level()
    {
        int closed = 0;
        List<string> commands = [];
        ModelBrowserController browser = new([Entry("one")], () => closed++, (entry, variant) => commands.Add("export:" + variant.Id), (entry, variant) => commands.Add("import:" + entry.Id));
        browser.OpenViewer();
        browser.CycleVariant();
        browser.Navigate(MenuInput.Confirm);
        browser.Navigate(MenuInput.Confirm);
        Assert.Equal("export:mod", Assert.Single(commands));
        browser.OpenCommands();
        browser.Navigate(MenuInput.Down);
        browser.Navigate(MenuInput.Confirm);
        Assert.Equal("import:one", commands[1]);
        browser.OpenCommands();
        browser.Cancel();
        Assert.Equal(ModelBrowserMode.Viewer, browser.Mode);
        browser.Cancel();
        Assert.Equal(ModelBrowserMode.Catalog, browser.Mode);
        browser.Cancel();
        Assert.Equal(1, closed);
    }

    [Fact]
    public void Refresh_preserves_entry_and_variant_identity_when_order_changes()
    {
        ModelBrowserEntry one = Entry("one");
        ModelBrowserEntry two = Entry("two");
        ModelBrowserController browser = Create([one, two]);
        browser.Select(1);
        browser.CycleVariant();
        browser.Refresh([two with { Variants = two.Variants.Reverse().ToArray() }, one]);
        Assert.Equal("two", browser.Selected!.Id);
        Assert.Equal("mod", browser.CurrentVariant!.Id);
    }

    [Fact]
    public void Scaled_pointer_selects_rows_opens_preview_and_activates_commands_once()
    {
        int exported = 0;
        ModelBrowserController browser = new([Entry("one"), Entry("two")], () => { }, (_, _) => exported++, (_, _) => { });
        UiView view = browser.BuildView();
        view.Resize(1920, 1344);
        Click(view, 1000, 366);
        Assert.Equal("two", browser.Selected!.Id);
        Click(view, 200, 400);
        Assert.Equal(ModelBrowserMode.Viewer, browser.Mode);
        Click(view, 1000, 1100);
        Assert.Equal(ModelBrowserMode.Commands, browser.Mode);
        view.Pointer(700, 620, true);
        view.Pointer(700, 620, true);
        Assert.Equal(1, exported);
    }

    [Fact]
    public void Clicking_selected_row_opens_viewer_and_command_popup_blocks_controls_behind_it()
    {
        ModelBrowserController browser = Create([Entry("one"), Entry("two")]);
        UiView view = browser.BuildView();
        Click(view, 500, 156);
        Assert.Equal(ModelBrowserMode.Viewer, browser.Mode);
        browser.OpenCommands();
        Click(view, 750, 550);
        Assert.Equal("one", browser.Selected!.Id);
        Assert.Equal(ModelBrowserMode.Commands, browser.Mode);
        view.Key(UiKey.Cancel);
        Assert.Equal(ModelBrowserMode.Viewer, browser.Mode);
        view.Key(UiKey.Cancel);
        Assert.Equal(ModelBrowserMode.Catalog, browser.Mode);
    }

    [Theory]
    [InlineData(640, 448)]
    [InlineData(960, 672)]
    [InlineData(1920, 1344)]
    public void Catalog_viewer_and_commands_render_without_covering_the_gpu_preview(int width, int height)
    {
        ModelBrowserController browser = Create(Enumerable.Range(0, 16).Select(index => Entry("Field character " + index)).ToArray());
        browser.Select(13);
        browser.SetMessage("Model ready. Select a variant to compare your mod.");
        foreach (ModelBrowserMode mode in Enum.GetValues<ModelBrowserMode>())
        {
            if (mode == ModelBrowserMode.Viewer)
            {
                browser.OpenViewer();
            }
            else if (mode == ModelBrowserMode.Commands)
            {
                browser.OpenCommands();
            }
            using Bitmap bitmap = new(width, height, PixelFormat.Format32bppArgb);
            using (BattlePreviewCanvas canvas = new(bitmap))
            {
                UiView view = browser.BuildView();
                view.Resize(width, height);
                float scale = width / 960f;
                UiRect preview = browser.PreviewBounds;
                canvas.Fill(new UiRect(preview.X * scale, preview.Y * scale, preview.Width * scale, preview.Height * scale), new UiColor(12, 34, 56));
                view.Draw(canvas, new UiTheme());
            }
            // The top-left preview interior remains available even with the command popup open.
            int x = (int)((browser.PreviewBounds.X + 20) * width / 960f);
            int y = (int)((browser.PreviewBounds.Y + 20) * height / 672f);
            Assert.Equal(Color.FromArgb(12, 34, 56).ToArgb(), bitmap.GetPixel(x, y).ToArgb());
            string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_MODEL_BROWSER_UI_OUTPUT");
            if (!string.IsNullOrWhiteSpace(output))
            {
                Directory.CreateDirectory(output);
                bitmap.Save(Path.Combine(output, $"{mode.ToString().ToLowerInvariant()}-{width}.png"), ImageFormat.Png);
            }
        }
    }
    private static void Click(UiView view, float x, float y)
    {
        view.Pointer(x, y, true);
        view.Pointer(x, y, false);
    }

    private static ModelBrowserController Create(ModelBrowserEntry[] entries)
    {
        return new ModelBrowserController(entries, () => { }, (_, _) => { }, (_, _) => { });
    }

    private static ModelBrowserEntry Entry(string name)
    {
        return new ModelBrowserEntry(name, name, "Field", "character", [new("original", "Original"), new("mod", "Example mod")]);
    }
}
