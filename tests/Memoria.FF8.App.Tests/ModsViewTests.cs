using Bitmap = SkiaSharp.SKBitmap;
using Color = SkiaSharp.SKColor;

using Memoria.FF8.GameUi;
using Memoria.FF8.Mods;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class ModsViewTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "ff8-mods-view-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void PackageRowSelectsWithoutChangingSettingsAndOnlyCheckboxToggles()
    {
        AddPackages(3);
        using ModRuntime runtime = Runtime();
        MenuController menu = Menu(runtime);
        UiView view = Arrange(menu);
        UiElement table = Element(view, "commands");
        float y = table.Bounds.Y + 28 + RowHeight(table) * 1.5f;
        table.Pointer(table.Bounds.X + 90, y);
        Assert.Equal("Demaster/Package02", menu.SelectedEntry?.Id);
        Assert.False(runtime.IsReloadPending);
        Assert.True(runtime.View.Packages.Single(package => package.Package.Id == "Demaster/Package02").Package.RequestedEnabled);
        Assert.Contains("Package02", menu.CurrentScreen.Details(menu));

        table.Pointer(table.Bounds.X + 15, y);
        Assert.True(runtime.IsReloadPending);
        Assert.False(runtime.View.Packages.Single(package => package.Package.Id == "Demaster/Package02").Package.RequestedEnabled);
        Assert.Equal("Demaster/Package02", menu.SelectedEntry?.Id);
    }

    [Fact]
    public void IncompatibleRowCanBeInspectedWhileItsCheckboxCannotChangePreference()
    {
        Write("Tonberry/Incompatible/mod.ini", "enabled = 0\n");
        Write("Tonberry/Incompatible/hashmap/test.csv", "unused");
        using ModRuntime runtime = Runtime(format => format != ModFormat.Tonberry);
        MenuController menu = Menu(runtime);
        UiView view = Arrange(menu);
        UiElement table = Element(view, "commands");
        table.Pointer(table.Bounds.X + 90, table.Bounds.Y + 45);
        Assert.Contains("In use: Incompatible edition", menu.CurrentScreen.Details(menu));
        table.Pointer(table.Bounds.X + 15, table.Bounds.Y + 45);
        Assert.False(runtime.IsReloadPending);
        Assert.False(Assert.Single(runtime.View.Packages).Package.RequestedEnabled);
        Assert.Contains("enabled = 0", File.ReadAllText(Path.Combine(root, "Mods/Tonberry/Incompatible/mod.ini")));
    }

    [Fact]
    public void PageCommandsAdvanceFullPagesPreserveSlotAndStopAtEnds()
    {
        AddPackages(27);
        using ModRuntime runtime = Runtime();
        MenuController menu = Menu(runtime);
        UiView view = Arrange(menu);
        UiElement table = Element(view, "commands");
        table.Key(UiKey.Down);
        table.Key(UiKey.Down);
        string selected = menu.SelectedEntry!.Id;
        int original = PackageIndex(menu, selected);
        Assert.Equal(2, original);
        table.Key(UiKey.PageDown);
        int next = PackageIndex(menu, menu.SelectedEntry!.Id);
        int pageSize = next - original;
        Assert.InRange(pageSize, 2, 25);
        view = Arrange(menu);
        Element(view, "mod-prev").Pointer(Element(view, "mod-prev").Bounds.X + 1, Element(view, "mod-prev").Bounds.Y + 1);
        Assert.Equal(selected, menu.SelectedEntry?.Id);
        view = Arrange(menu);
        Element(view, "mod-next").Pointer(Element(view, "mod-next").Bounds.X + 1, Element(view, "mod-next").Bounds.Y + 1);
        Assert.Equal(next, PackageIndex(menu, menu.SelectedEntry!.Id));
        for (int count = 0; count < 30; count++)
        {
            Element(Arrange(menu), "commands").Key(UiKey.Down);
        }
        Assert.Equal(26, PackageIndex(menu, menu.SelectedEntry!.Id));
        Element(Arrange(menu), "commands").Key(UiKey.PageDown);
        Assert.Equal(26, PackageIndex(menu, menu.SelectedEntry!.Id));
        for (int count = 0; count < 30; count++)
        {
            Element(Arrange(menu), "commands").Key(UiKey.Up);
        }
        Assert.Equal(0, PackageIndex(menu, menu.SelectedEntry!.Id));
        Element(Arrange(menu), "commands").Key(UiKey.PageUp);
        Assert.Equal(0, PackageIndex(menu, menu.SelectedEntry!.Id));
        Assert.False(runtime.IsReloadPending);
    }

    [Fact]
    public void FiltersComposeAndInspectorRetainsPackageWhileToolbarHasFocus()
    {
        AddPackages(3);
        Write("Tonberry/Other/hashmap/test.csv", "unused");
        using ModRuntime runtime = Runtime(format => format != ModFormat.Tonberry);
        MenuController menu = Menu(runtime);
        UiView view = Arrange(menu);
        UiElement table = Element(view, "commands");
        table.Pointer(table.Bounds.X + 90, table.Bounds.Y + 28 + RowHeight(table) * (PackageIndex(menu, "Demaster/Package02") + 0.5f));
        Assert.Contains("Package02", menu.CurrentScreen.Details(menu));
        view = Arrange(menu);
        view.Focus(Element(view, "mod-search"));
        Assert.Contains("Package02", menu.CurrentScreen.Details(menu));
        menu.Execute(new("mod-filter"));
        Assert.Contains("Package02", menu.CurrentScreen.Details(menu));
        menu.Execute(new("mod-format"));
        Assert.Single(PackageEntries(menu));
        Assert.Equal("Tonberry/Other", PackageEntries(menu)[0].Id);
        menu.Execute(new("mod-clear"));
        Assert.Equal(4, PackageEntries(menu).Length);
        menu.SelectEntry("mods", "mod-search");
        menu.Navigate(MenuInput.Confirm);
        foreach (char character in "Package02")
        {
            Assert.True(menu.EnterModSearchCharacter(character));
        }
        menu.Navigate(MenuInput.Cancel);
        Assert.Equal("Demaster/Package02", Assert.Single(PackageEntries(menu)).Id);
        menu.Execute(new("mod-filter"));
        menu.Execute(new("mod-filter"));
        Assert.Empty(PackageEntries(menu));
        menu.Execute(new("mod-clear"));
        Assert.Equal(4, PackageEntries(menu).Length);
        menu.Execute(new("mod-filter"));
        menu.Execute(new("mod-filter"));
        menu.Execute(new("mod-filter"));
        Assert.Equal("Tonberry/Other", Assert.Single(PackageEntries(menu)).Id);
        Assert.False(runtime.IsReloadPending);
    }

    [Fact]
    public void DiagnosticBannerKeepsAtLeastTwelveRowsAtDesktopHeight()
    {
        AddPackages(27);
        using ModRuntime runtime = Runtime();
        runtime.ReportFailure(new("replacement-failed", "Synthetic image update could not be applied.", "Demaster/Package01", "image.png"));
        MenuController menu = Menu(runtime);
        UiView view = Arrange(menu, 2048, 788);
        Element(view, "commands").Key(UiKey.PageDown);
        Assert.InRange(PackageIndex(menu, menu.SelectedEntry!.Id), 12, 26);
    }

    [Theory]
    [InlineData(640, 448)]
    [InlineData(1280, 896)]
    [InlineData(1920, 1080)]
    public void RenderedBrowserKeepsToolbarTableAndInspectorSeparateAfterResize(int width, int height)
    {
        AddPackages(27);
        Write("Tonberry/Incompatible/hashmap/test.csv", "unused");
        Write("Demaster/Package02/mod.ini", "enabled = 0\n");
        Write("Demaster/Package00/mod.ini", "enabled = 1\n");
        using ModRuntime runtime = Runtime(format => format != ModFormat.Tonberry);
        runtime.ReportFailure(new("replacement-failed", "Synthetic image update could not be applied.", "Demaster/Package03", "image.png"));
        MenuController menu = Menu(runtime);
        UiView view = Arrange(menu, width, height);
        string? selected = menu.SelectedEntry?.Id;
        UiElement table = Element(view, "commands");
        UiElement inspector = Element(view, "mod-inspector");
        UiElement search = Element(view, "mod-search");
        Assert.True(search.Bounds.Bottom <= table.Bounds.Y);
        if (inspector.Visible)
        {
            Assert.True(inspector.Bounds.X >= table.Bounds.Right || inspector.Bounds.Y >= table.Bounds.Bottom);
        }
        Assert.True(table.Bounds.Height >= 62);
        Assert.DoesNotContain(table.Children, child => child.Id is "mod-search" or "mod-refresh" or "create-tab");
        using Bitmap bitmap = new(width, height, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Unpremul);
        using (BattlePreviewCanvas canvas = new(bitmap, root))
        {
            view.Draw(canvas, new PreviewTheme());
        }
        string? output = Environment.GetEnvironmentVariable("MEMORIA_MODS_PREVIEW_OUTPUT");
        if (!string.IsNullOrWhiteSpace(output))
        {
            Directory.CreateDirectory(output);
            bitmap.SavePng(Path.Combine(output, $"mods-{width}x{height}.png"));
        }
        view.Resize(960, 672);
        Assert.Equal(selected, menu.SelectedEntry?.Id);
        Assert.True(Element(view, "commands").Bounds.Height >= 62);
        Assert.True(runtime.IsReloadPending);
    }

    [Fact]
    public void RepresentativeBrowserStatesRenderWithBothThemesAndEnlargedText()
    {
        string? output = Environment.GetEnvironmentVariable("MEMORIA_MODS_PREVIEW_OUTPUT");
        if (string.IsNullOrWhiteSpace(output))
        {
            return;
        }
        Directory.CreateDirectory(output);
        foreach (string scenario in new[] { "two-packages", "full-page", "full-page-warning", "partial-page", "requested-mismatch", "review-required", "changes-blocked", "create-mods" })
        {
            string scenarioRoot = Path.Combine(root, scenario);
            int count = scenario is "full-page" or "full-page-warning" or "partial-page" ? 27 : 2;
            for (int index = 1; index <= count; index++)
            {
                string directory = Path.Combine(scenarioRoot, "Mods", "Demaster", $"Package{index:D2}");
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "image.png"), "synthetic metadata");
            }
            bool reject = false;
            string code = scenario == "review-required" ? "Memoria.GuardianForceReview" : scenario == "changes-blocked" ? "Memoria.GuardianForceBoundary" : "validation-failed";
            using ModRuntime runtime = new(scenarioRoot, prepare: _ => reject ? new(false, Diagnostics: [new(code, "Synthetic proposed gameplay changes require a safe review boundary.")]) : ModPreparationResult.Accepted, watch: false, debounce: TimeSpan.Zero);
            Assert.True(runtime.PreparePending(force: true));
            Assert.True(runtime.CommitPending());
            if (scenario == "full-page-warning")
            {
                runtime.ReportFailure(new("replacement-failed", "Synthetic image update could not be applied.", "Demaster/Package01", "image.png"));
            }
            if (scenario is "requested-mismatch" or "review-required" or "changes-blocked")
            {
                reject = true;
                Assert.True(runtime.SetEnabled("Demaster/Package01", false).Success);
                Assert.False(runtime.PreparePending(force: true));
            }
            foreach (bool vector in new[] { false, true })
            {
                foreach (var (width, height) in new[] { (1280, 720), (1920, 1080), (2048, 788) })
                {
                    foreach (string scale in new[] { "100%", "150%" })
                    {
                        MenuController menu = Menu(runtime);
                        menu.Presentation = vector ? MenuPresentation.Vector : MenuPresentation.Classic;
                        menu.Data.Settings["Text scale"] = scale;
                        if (scenario == "create-mods")
                        {
                            ConnectAuthoring(menu);
                            menu.Open("mods-create");
                        }
                        UiView view = Arrange(menu, width, height);
                        if (scenario == "partial-page")
                        {
                            for (int index = 1; index < count; index++)
                            {
                                Element(view, "commands").Key(UiKey.Down);
                            }
                            view = Arrange(menu, width, height);
                        }
                        using Bitmap bitmap = new(width, height, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Unpremul);
                        using (PreviewCanvas canvas = new(bitmap, scenarioRoot))
                        {
                            view.Draw(canvas, vector ? new VectorUiTheme() : new UiTheme());
                        }
                        bitmap.SavePng(Path.Combine(output, $"{scenario}-{(vector ? "vector" : "classic")}-{width}x{height}-{scale.TrimEnd('%')}.png"));
                    }
                }
            }
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }

    private void AddPackages(int count)
    {
        for (int index = 1; index <= count; index++)
        {
            Write($"Demaster/Package{index:D2}/image.png", "synthetic package metadata");
        }
    }

    private void Write(string relative, string contents)
    {
        string path = Path.Combine(root, "Mods", relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
    }

    private ModRuntime Runtime(Func<ModFormat, bool>? compatible = null)
    {
        ModRuntime runtime = new(root, compatible, watch: false, debounce: TimeSpan.Zero);
        Assert.True(runtime.PreparePending(force: true));
        Assert.True(runtime.CommitPending());
        return runtime;
    }

    private static MenuController Menu(IModService service)
    {
        MenuController menu = new();
        menu.ConnectMods(service);
        menu.Open("mods");
        return menu;
    }

    private static void ConnectAuthoring(MenuController menu)
    {
        var source = GuardianForceAbilityEditorTests.Source();
        menu.ConnectGuardianForceEditor(() => new(source, null, [], [], []), id => "GF" + id, id => "Ability" + id, id => "Description" + id, action => action.ToString(),
            (_, _, _) => throw new InvalidOperationException("Preview rendering must not start a review."),
            _ => throw new InvalidOperationException("Preview rendering must not apply gameplay changes."),
            (_, _, _) => throw new InvalidOperationException("Preview rendering must not export a package."));
    }

    private static UiView Arrange(MenuController menu, int width = 960, int height = 672)
    {
        UiView view = menu.BuildView();
        view.Resize(width, height);
        return view;
    }

    private static float RowHeight(UiElement table) => table.Bounds.Width < 800 ? 48 : 34;

    private static MenuEntry[] PackageEntries(MenuController menu) => menu.Entries.Where(entry => entry.Action?.Kind == "mod-toggle").ToArray();

    private static int PackageIndex(MenuController menu, string id) => Array.FindIndex(PackageEntries(menu), entry => entry.Id == id);

    private static UiElement Element(UiView view, string id) => Assert.Single(Descendants(view.Root), element => element.Id == id);

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

    private sealed class PreviewCanvas(Bitmap bitmap, string gameDirectory) : IUiCanvas, IDisposable
    {
        private readonly BattlePreviewCanvas canvas = new(bitmap, gameDirectory);

        public void Fill(UiRect bounds, UiColor color) => canvas.Fill(bounds, color);
        public void Paper(UiRect bounds, UiColor color) => canvas.Paper(bounds, color);
        public void Text(string text, float size, UiRect bounds, UiColor color) => canvas.Text(text, size, bounds, color);
        public void Text(string text, float size, UiRect bounds, UiColor color, UiAlignment alignment) => canvas.Text(text, size, bounds, color, alignment);
        public void Text(string text, float size, UiRect bounds, UiColor color, UiAlignment alignment, UiTextFlow flow) => canvas.Text(text, size, bounds, color, alignment, flow);
        public void Text(string text, float size, UiRect bounds, UiColor color, string? family) => canvas.Text(text, size, bounds, color, family);
        public void Text(string text, float size, UiRect bounds, UiColor color, string? family, UiAlignment alignment, UiTextFlow flow) => canvas.Text(text, size, bounds, color, family, alignment, flow);
        public UiSize MeasureText(string text, float size, float width) => canvas.MeasureText(text, size, width);
        public UiSize MeasureText(string text, float size, float width, string? family) => canvas.MeasureText(text, size, width, family);
        public void Line(float x1, float y1, float x2, float y2, UiColor color, float thickness) => canvas.Line(x1, y1, x2, y2, color, thickness);
        public void PushClip(UiRect bounds) => canvas.PushClip(bounds);
        public void PopClip() => canvas.PopClip();
        public void Image(UiImageSource source, UiRect bounds) => throw new InvalidOperationException("Synthetic mod previews must not depend on installed artwork.");
        public void MenuPointer(UiRect bounds)
        {
            canvas.Line(bounds.X, bounds.Y, bounds.Right, bounds.Y + bounds.Height / 2, new UiColor(255, 255, 255), 2);
            canvas.Line(bounds.Right, bounds.Y + bounds.Height / 2, bounds.X, bounds.Bottom, new UiColor(255, 255, 255), 2);
        }
        public void Dispose() => canvas.Dispose();
    }

    private sealed class PreviewTheme : UiTheme
    {
        public override void Pointer(IUiCanvas canvas, float x, float y, float size = 10)
        {
            canvas.Line(x, y, x + size, y + size / 2, Foreground, 2);
            canvas.Line(x + size, y + size / 2, x, y + size, Foreground, 2);
        }
    }
}
