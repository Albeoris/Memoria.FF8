using Memoria.FF8.GameUi;
using Memoria.FF8.Mods;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class BundledModPresentationTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "ff8-bundled-mod-presentation-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void BundledPackagesLeadTheMenuWithoutChangingRuntimeOrderOrUserPackageIdentity()
    {
        Write("Mods/Demaster/First/image.png", "image");
        Write("Mods/Memoria/Authored/data.json", "{}");
        Write("BundledMods/Memoria/BuiltIn/data.json", "{}");
        Write("Mods/Memoria/BuiltIn/mod.ini", "enabled = 0\n");
        using ModRuntime runtime = Runtime();
        string[] runtimeOrder = runtime.View.Packages.Select(view => view.Package.Id).ToArray();
        MenuController menu = Menu(runtime);

        Assert.Equal(new[] { "Memoria/BuiltIn", "Demaster/First", "Memoria/Authored" }, PackageIds(menu));
        Assert.Equal(runtimeOrder, runtime.View.Packages.Select(view => view.Package.Id));
        Assert.True(runtime.View.Packages.Single(view => view.Package.Id == "Memoria/BuiltIn").Package.IsBundled);
        Assert.False(runtime.View.Packages.Single(view => view.Package.Id == "Memoria/Authored").Package.IsBundled);
        Assert.False(runtime.View.Packages.Single(view => view.Package.Id == "Memoria/BuiltIn").Package.RequestedEnabled);
        menu.Execute(new("mod-filter"));
        Assert.Equal(new[] { "Demaster/First", "Memoria/Authored" }, PackageIds(menu));
        Assert.False(runtime.IsReloadPending);
    }

    [Theory]
    [InlineData(960, 672)]
    [InlineData(2048, 1080)]
    public void BundledRowReusesSaveCrestWithinTheNameColumn(int width, int height)
    {
        Write("BundledMods/Memoria/BuiltIn/data.json", "{}");
        Write("Mods/Memoria/Authored/data.json", "{}");
        using ModRuntime runtime = Runtime();
        MenuController menu = Menu(runtime);
        UiView view = menu.BuildView();
        view.Resize(width, height);
        RecordingCanvas canvas = new();
        view.Draw(canvas, menu.CreateTheme());

        var crest = Assert.Single(canvas.Images, image => image.Source.Kind == UiImageKind.SilverCrest);
        UiRect name = Assert.Single(canvas.Texts, text => text.Text == "BuiltIn").Bounds;
        Assert.True(crest.Bounds.Right < name.X);
        Assert.Equal(crest.Bounds.Width, crest.Bounds.Height);
        float renderScale = Math.Min(width / view.DesignWidth, height / view.DesignHeight);
        Assert.InRange(crest.Bounds.Height, 1, 26 * renderScale);
        Assert.True(crest.Bounds.Y >= name.Y);
        Assert.Contains(canvas.Texts, text => text.Text == "Authored");
    }

    private void Write(string relative, string text)
    {
        string path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private ModRuntime Runtime()
    {
        ModRuntime runtime = new(root, watch: false, debounce: TimeSpan.Zero);
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

    private static string[] PackageIds(MenuController menu) => menu.Entries.Where(entry => entry.Action?.Kind == "mod-toggle").Select(entry => entry.Id).ToArray();

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }

    private sealed class RecordingCanvas : IUiCanvas
    {
        public List<(UiImageSource Source, UiRect Bounds)> Images { get; } = [];
        public List<(string Text, UiRect Bounds)> Texts { get; } = [];

        public void Fill(UiRect bounds, UiColor color)
        {
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color)
        {
            Texts.Add((text, bounds));
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment)
        {
            Text(text, fontSize, bounds, color);
        }

        public UiSize MeasureText(string text, float fontSize, float maximumWidth)
        {
            return new UiSize(Math.Min(maximumWidth, text.Length * fontSize * 0.5f), fontSize * 1.2f);
        }

        public void Image(UiImageSource source, UiRect bounds)
        {
            Images.Add((source, bounds));
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
