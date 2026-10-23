using System.Drawing;
using System.Drawing.Imaging;
using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class ModelBrowserAssetSelectionTests
{
    [Fact]
    public void Filtering_preserves_selections_and_bulk_operations_have_distinct_scope()
    {
        ModelBrowserController browser = Browser();
        browser.SelectAssets(false, clear: true);
        browser.SetAssetSearch("Body");
        browser.SelectAssets(visibleOnly: true);
        Assert.True(browser.SelectedAssets.SetEquals(["body"]));
        browser.SetAssetSearch("Weapon");
        Assert.Equal(1, browser.HiddenSelectedCount);
        browser.SelectAssets(visibleOnly: false);
        Assert.Equal(4, browser.SelectedAssets.Count);
        Assert.DoesNotContain("unsupported", browser.SelectedAssets);
        browser.SelectAssets(false, clear: true);
        Assert.Empty(browser.SelectedAssets);
    }

    [Fact]
    public void Clip_groups_are_tristate_and_do_not_select_geometry_or_preview_weapon()
    {
        ModelBrowserController browser = Browser();
        browser.SelectAssets(false, clear: true);
        browser.SetAssetTab(true);
        browser.ToggleAsset("body-clip");
        Assert.Equal(1, browser.GroupSelection("Base / Clip 000"));
        browser.ToggleGroup("Base / Clip 000");
        Assert.Equal(2, browser.GroupSelection("Base / Clip 000"));
        Assert.False(browser.SelectedAssets.Contains("body"));
        Assert.False(browser.SelectedAssets.Contains("weapon"));
        Assert.Equal("body", browser.ActiveObject);
        browser.ToggleGroup("Base / Clip 000");
        Assert.Equal(0, browser.GroupSelection("Base / Clip 000"));
    }

    [Fact]
    public void Late_discoveries_do_not_change_manually_edited_selection()
    {
        ModelBrowserController browser = Browser();
        browser.ToggleAsset("body");
        browser.AddAsset(new("late", "New compatible clip", "External", true));
        Assert.DoesNotContain("late", browser.SelectedAssets);
        Assert.Contains(browser.Assets, row => row.Id == "late");
    }

    [Fact]
    public void Focus_routes_arrows_and_cancel_discards_pending_action()
    {
        ModelBrowserController browser = Browser();
        int calls = 0;
        browser.OpenAssetPanel(() => calls++);
        browser.Navigate(MenuInput.Down);
        Assert.Equal(0, browser.RotationX);
        Assert.Equal(1, browser.AssetIndex);
        browser.Navigate(MenuInput.Tab);
        browser.Navigate(MenuInput.Up);
        Assert.True(browser.RotationX < 0);
        browser.Cancel();
        browser.ContinueAssetSelection();
        Assert.False(browser.AssetsOpen);
        Assert.Equal(0, calls);
        Assert.Equal(ModelBrowserMode.Viewer, browser.Mode);
    }

    [Fact]
    public void Playback_preserves_native_time_when_speed_or_loop_changes()
    {
        ModelBrowserController browser = Browser();
        browser.SetClipTiming(1, 0.1f);
        browser.ToggleLoop();
        browser.AdvancePlayback(2);
        Assert.Equal(1, browser.PlaybackSeconds);
        browser.StepFrame(-1);
        Assert.Equal(0.9f, browser.PlaybackSeconds, 5);
        browser.CyclePlaybackSpeed();
        Assert.Equal(2, browser.PlaybackSpeed);
        Assert.Equal(0.1f, browser.ClipFrameSeconds);
        browser.Seek(0);
        browser.TogglePlayback();
        browser.AdvancePlayback(0.2f);
        Assert.Equal(0.4f, browser.PlaybackSeconds, 5);
    }

    [Theory]
    [InlineData(640, 448)]
    [InlineData(960, 672)]
    [InlineData(1920, 1344)]
    public void Asset_panel_renders_beside_preview_and_pointer_toggles_only_checkbox(int width, int height)
    {
        ModelBrowserController browser = Browser();
        browser.SetDiscoveryStatus("Complete for supported sources");
        UiView view = browser.BuildView();
        view.Resize(width, height);
        using Bitmap bitmap = new(width, height, PixelFormat.Format32bppArgb);
        using (BattlePreviewCanvas canvas = new(bitmap))
        {
            float scale = width / 960f;
            UiRect preview = browser.PreviewBounds;
            canvas.Fill(new UiRect(preview.X * scale, preview.Y * scale, preview.Width * scale, preview.Height * scale), new UiColor(12, 34, 56));
            view.Draw(canvas, new UiTheme());
        }
        Assert.Equal(Color.FromArgb(12, 34, 56).ToArgb(), bitmap.GetPixel((int)(70 * width / 960f), (int)(220 * height / 672f)).ToArgb());
        view.Pointer(530 * width / 960f, 179 * height / 672f, true);
        view.Pointer(530 * width / 960f, 179 * height / 672f, false);
        Assert.DoesNotContain("body", browser.SelectedAssets);
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_MODEL_BROWSER_UI_OUTPUT");
        if (!string.IsNullOrWhiteSpace(output))
        {
            Directory.CreateDirectory(output);
            bitmap.Save(Path.Combine(output, $"asset-selection-{width}.png"), ImageFormat.Png);
        }
    }

    private static ModelBrowserController Browser()
    {
        ModelBrowserController browser = new([new("battle/body", "Squall", "Battle", "Squall", [new("original", "Original")])], () => { }, (_, _) => { }, (_, _) => { });
        browser.OpenViewer();
        browser.AddAsset(new("body", "Body", "Objects", false));
        browser.AddAsset(new("weapon", "Weapon alternative", "Weapons", false));
        browser.AddAsset(new("body-clip", "Body component", "Base / Clip 000", true));
        browser.AddAsset(new("weapon-clip", "Weapon component", "Base / Clip 000", true));
        browser.AddAsset(new("unsupported", "Unsupported source", "Diagnostics", false, false, "Decoder unavailable"));
        browser.OpenAssetPanel();
        return browser;
    }
}
