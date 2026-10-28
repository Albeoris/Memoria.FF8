using Memoria.FF8.GameUi;
using Memoria.FF8.Mods;
using Memoria.FF8.Ui;
using Bitmap = SkiaSharp.SKBitmap;

namespace Memoria.FF8.App.Tests;

public sealed class ModCatalogUiTests
{
    [Fact]
    public void InstalledKeepsPendingDownloadActivityReachable()
    {
        Library service = new();
        service.Publish(jobs: [Job(Guid.NewGuid(), service.View.Catalog.Entries[0], "Downloading", pause: true)]);
        using ModRuntime mods = new(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), watch: false);
        MenuController menu = new();
        menu.ConnectMods(mods);
        menu.ConnectModLibrary(service);
        menu.Open("mods");
        UiView view = Arrange(menu);
        UiButton activity = Assert.IsType<UiButton>(Element(view, "mod-library-activity"));
        Assert.True(activity.Bounds.Width > 0);
        Assert.Contains("Downloading", activity.Text);
        Assert.True(activity.Bounds.Y >= Element(view, "mod-auto").Bounds.Y);
        activity.Key(UiKey.Confirm);
        Assert.Equal("mods-downloads", menu.ScreenId);
        menu.DisconnectMods();
    }

    [Fact]
    public void SelectionAndConfirmationInspectWithoutDownloadingAndInstallIsSeparate()
    {
        Library service = new();
        MenuController menu = Menu(service);
        UiView view = Arrange(menu);
        UiElement table = Element(view, "commands");
        table.Pointer(table.Bounds.X + 5, table.Bounds.Y + 70);
        table.Key(UiKey.Confirm);
        Assert.Empty(service.Commands);
        Assert.Contains("Package 02", menu.CurrentScreen.Details(menu));
        Element(Arrange(menu), "library-primary").Key(UiKey.Confirm);
        Assert.Equal(["queue:Package 02"], service.Commands);

        RemoteModEntry entry = service.View.Catalog.Entries[1];
        Guid id = Guid.NewGuid();
        service.Publish(jobs: [Job(id, entry, "Ready to install", install: true)]);
        menu.RefreshModView();
        Assert.Equal("Install", Assert.IsType<UiButton>(Element(Arrange(menu), "library-primary")).Text);
        Assert.Single(service.Commands);
        Element(Arrange(menu), "library-primary").Key(UiKey.Confirm);
        Assert.Equal("install:" + id, service.Commands.Last());
    }

    [Fact]
    public void SnapshotRevisionPreservesSelectionAndDoesNotRebuildOnUnchangedFrame()
    {
        Library service = new();
        MenuController menu = Menu(service);
        UiView view = Arrange(menu);
        Element(view, "commands").Key(UiKey.Down);
        view = Arrange(menu);
        menu.RefreshModView();
        Assert.Same(view, menu.BuildView());
        service.Publish(entries: service.View.Catalog.Entries.Reverse().ToArray());
        menu.RefreshModView();
        Assert.Contains("Package 02", menu.CurrentScreen.Details(menu));
        Assert.Empty(service.Commands);
        service.Publish(entries: service.View.Catalog.Entries.Where(entry => entry.Name != "Package 02").ToArray());
        menu.RefreshModView();
        Arrange(menu);
        Assert.Contains("Package 02", menu.CurrentScreen.Details(menu));
    }

    [Fact]
    public void SearchOwnsTextAndFiltersSavedCatalogWithoutRefresh()
    {
        Library service = new();
        MenuController menu = Menu(service);
        menu.Push("mods-catalog-search");
        Assert.True(menu.IsModSearchEditing);
        foreach (char letter in "Author 02")
        {
            Assert.True(menu.EnterModSearchCharacter(letter));
        }
        menu.Navigate(MenuInput.Cancel);
        Assert.Equal("Package 02", Assert.Single(menu.Entries).Label);
        Assert.Empty(service.Commands);
    }

    [Fact]
    public void HorizontalListPagingAndShoulderTabsHaveSeparateMeanings()
    {
        Library service = new();
        MenuController menu = Menu(service);
        UiView view = Arrange(menu);
        Element(view, "commands").Key(UiKey.Right);
        Assert.Equal("mods-catalog", menu.ScreenId);
        Assert.DoesNotContain("Package 01", menu.CurrentScreen.Details(menu));
        menu.Navigate(MenuInput.PageRight);
        Assert.Equal("mods-downloads", menu.ScreenId);
        menu.Navigate(MenuInput.PageLeft);
        Assert.Equal("mods-catalog", menu.ScreenId);
        Assert.Empty(service.Commands);
    }

    [Fact]
    public void DownloadActionsRespectCapabilitiesAndUnknownTotalRemainsUnknown()
    {
        Library service = new();
        Guid id = Guid.NewGuid();
        service.Publish(jobs: [Job(id, service.View.Catalog.Entries[0], "Downloading", pause: true, cancel: true)]);
        MenuController menu = Menu(service, "mods-downloads");
        Arrange(menu);
        Assert.Contains(menu.CurrentScreen.Details(menu), line => line.Contains("total unknown"));
        Element(Arrange(menu), "commands").Key(UiKey.Confirm);
        Element(Arrange(menu), "library-secondary").Key(UiKey.Confirm);
        Element(Arrange(menu), "library-cancel").Key(UiKey.Confirm);
        Assert.Equal(["pause:" + id, "cancel:" + id], service.Commands);
    }

    [Fact]
    public void OpeningStaleCatalogRefreshesOnceAndFramePollingDoesNotRetry()
    {
        Library service = new();
        service.MarkStale();
        MenuController menu = Menu(service);
        Assert.Equal(["refresh"], service.Commands);
        for (int frame = 0; frame < 10; frame++)
        {
            menu.RefreshModView();
            Arrange(menu);
        }
        Assert.Equal(["refresh"], service.Commands);
    }

    [Fact]
    public void InteractiveProviderPageRequiresExplicitLabeledAction()
    {
        Library service = new();
        Guid id = Guid.NewGuid();
        service.Publish(jobs: [Job(id, service.View.Catalog.Entries[0], "Needs user action") with { CanOpenDownloadPage = true }]);
        MenuController menu = Menu(service, "mods-downloads");
        Element(Arrange(menu), "commands").Key(UiKey.Confirm);
        Assert.Empty(service.Commands);
        UiButton action = Assert.IsType<UiButton>(Element(Arrange(menu), "library-secondary"));
        Assert.Equal("Open download page", action.Text);
        action.Key(UiKey.Confirm);
        Assert.Equal(["open:" + id], service.Commands);
    }

    [Theory]
    [InlineData("Installed")]
    [InlineData("Paused")]
    [InlineData("Cancelled")]
    public void ManagedComparableUpdateDownloadsForReviewWithoutInstalling(string oldJobPhase)
    {
        Library service = new();
        RemoteModEntry entry = service.View.Catalog.Entries[0];
        service.Managed(entry, "1.0");
        service.Publish(jobs: [Job(Guid.NewGuid(), entry with { Version = "1.0" }, oldJobPhase)]);
        MenuController menu = Menu(service);
        UiView view = Arrange(menu);
        Element(view, "commands").Key(UiKey.Confirm);
        UiButton primary = Assert.IsType<UiButton>(Element(Arrange(menu), "library-primary"));
        Assert.Equal("Download update & check", primary.Text);
        primary.Key(UiKey.Confirm);
        Assert.Equal(["queue:Package 01"], service.Commands);
    }

    [Theory]
    [InlineData(640, 448)]
    [InlineData(1280, 896)]
    [InlineData(1920, 1080)]
    [InlineData(1280, 800)]
    public void CatalogAndDownloadsRenderBothThemesAndTextScalesWithoutOverlappingControls(int width, int height)
    {
        string? output = Environment.GetEnvironmentVariable("MEMORIA_MOD_CATALOG_PREVIEW_OUTPUT");
        foreach (bool vector in new[] { false, true })
        {
            foreach (string scale in new[] { "100%", "150%" })
            {
                foreach (string screen in new[] { "mods-catalog", "mods-downloads" })
                {
                    Library service = new();
                    service.Publish(jobs: [Job(Guid.NewGuid(), service.View.Catalog.Entries[0], "Downloading", pause: true, cancel: true), Job(Guid.NewGuid(), service.View.Catalog.Entries[1], "Ready to install", install: true)]);
                    MenuController menu = Menu(service, screen);
                    menu.Presentation = vector ? MenuPresentation.Vector : MenuPresentation.Classic;
                    menu.Data.Settings["Text scale"] = scale;
                    UiView view = Arrange(menu, width, height);
                    UiElement table = Element(view, "commands");
                    Assert.True(Element(view, "library-refresh").Bounds.Bottom <= table.Bounds.Y);
                    Assert.True(table.Bounds.Height > 64);
                    Assert.True(Element(view, "library-prev").Bounds.Y >= table.Bounds.Bottom);
                    UiElement[] visible = Descendants(view.Root).Where(element => element is UiButton or UiChoiceRow && element.Visible && !element.Bounds.IsEmpty).ToArray();
                    for (int left = 0; left < visible.Length; left++)
                    {
                        for (int right = left + 1; right < visible.Length; right++)
                        {
                            Assert.False(Overlap(visible[left].Bounds, visible[right].Bounds), visible[left].Id + " overlaps " + visible[right].Id);
                        }
                    }
                    using Bitmap bitmap = new(width, height, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Unpremul);
                    using (PreviewCanvas canvas = new(bitmap))
                    {
                        view.Draw(canvas, vector ? new VectorUiTheme() : new UiTheme());
                    }
                    if (!string.IsNullOrWhiteSpace(output))
                    {
                        Directory.CreateDirectory(output);
                        bitmap.SavePng(Path.Combine(output, $"{screen}-{(vector ? "vector" : "classic")}-{width}x{height}-{scale.TrimEnd('%')}.png"));
                    }
                    table.Key(UiKey.Confirm);
                    view = Arrange(menu, width, height);
                    Assert.True(Element(view, "library-primary").Visible);
                    if (width == 640)
                    {
                        Assert.False(Element(view, "commands").Visible);
                        Assert.True(Element(view, "library-list").Visible);
                        Assert.True(Element(view, "library-primary").Bounds.Y >= Element(view, "library-refresh").Bounds.Bottom);
                    }
                    using (PreviewCanvas canvas = new(bitmap))
                    {
                        view.Draw(canvas, vector ? new VectorUiTheme() : new UiTheme());
                    }
                    if (!string.IsNullOrWhiteSpace(output))
                    {
                        bitmap.SavePng(Path.Combine(output, $"{screen}-details-{(vector ? "vector" : "classic")}-{width}x{height}-{scale.TrimEnd('%')}.png"));
                    }
                    if (width == 640)
                    {
                        menu.Navigate(MenuInput.Cancel);
                        Assert.Equal(screen, menu.ScreenId);
                        Assert.True(Element(Arrange(menu, width, height), "commands").Visible);
                    }
                    Assert.Empty(service.Commands);
                }
            }
        }
    }

    private static bool Overlap(UiRect a, UiRect b) => a.X < b.Right && a.Right > b.X && a.Y < b.Bottom && a.Bottom > b.Y;
    private static MenuController Menu(Library service, string screen = "mods-catalog")
    {
        MenuController menu = new();
        menu.ConnectModLibrary(service);
        menu.Open(screen);
        return menu;
    }

    private static UiView Arrange(MenuController menu, int width = 1280, int height = 896)
    {
        UiView view = menu.BuildView();
        view.Resize(width, height);
        return view;
    }

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

    private static ModLibraryJob Job(Guid id, RemoteModEntry entry, string phase, bool pause = false, bool cancel = false, bool install = false) => new(id, entry, phase, 1234567, null, 0, null, "Checked package report. Install disabled by preference.", pause, false, cancel, install, false);

    private sealed class PreviewCanvas(Bitmap bitmap) : IUiCanvas, IDisposable
    {
        private readonly BattlePreviewCanvas canvas = new(bitmap, Path.GetTempPath());
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
        public void Image(UiImageSource source, UiRect bounds) => throw new InvalidOperationException("Synthetic previews must not read game artwork.");
        public void MenuPointer(UiRect bounds)
        {
            canvas.Line(bounds.X, bounds.Y, bounds.Right, bounds.Y + bounds.Height / 2, new UiColor(255, 255, 255), 2);
            canvas.Line(bounds.Right, bounds.Y + bounds.Height / 2, bounds.X, bounds.Bottom, new UiColor(255, 255, 255), 2);
        }
        public void Dispose() => canvas.Dispose();
    }

    private sealed class Library : IModLibraryService
    {
        public ModLibrarySnapshot View { get; private set; } = new(1, new(Enumerable.Range(1, 24).Select(index => new RemoteModEntry("fixture", new Guid(index, 0, 0, new byte[8]), $"Package {index:D2}", "2.4")
        {
            Author = $"Author {index:D2}", Category = index % 2 == 0 ? "World" : "Audio", Description = "A synthetic catalog package with a readable description and no remote artwork dependency.", Links = ["https://example.invalid/package.iroj"]
        }).ToArray(), DateTimeOffset.Parse("2026-01-01T00:00:00Z"), false, false, null), [], "Steam 2013 · English", true);
        public List<string> Commands { get; } = [];
        public void MarkStale() => View = View with { Catalog = View.Catalog with { IsStale = true, Error = "Source unavailable" } };
        public void Managed(RemoteModEntry entry, string version) => View = View with { Installations = [new(entry.SourceId, entry.Id, version, "local-package", false)] };
        public void Publish(IReadOnlyList<RemoteModEntry>? entries = null, IReadOnlyList<ModLibraryJob>? jobs = null) => View = View with { Revision = View.Revision + 1, Catalog = View.Catalog with { Entries = entries ?? View.Catalog.Entries }, Jobs = jobs ?? View.Jobs };
        public void RefreshCatalog() => Commands.Add("refresh");
        public void Queue(RemoteModEntry entry) => Commands.Add("queue:" + entry.Name);
        public void Pause(Guid id) => Commands.Add("pause:" + id);
        public void Resume(Guid id) => Commands.Add("resume:" + id);
        public void Cancel(Guid id) => Commands.Add("cancel:" + id);
        public void Install(Guid id) => Commands.Add("install:" + id);
        public void Retry(Guid id) => Commands.Add("retry:" + id);
        public void OpenDownloadPage(Guid id) => Commands.Add("open:" + id);
    }
}


