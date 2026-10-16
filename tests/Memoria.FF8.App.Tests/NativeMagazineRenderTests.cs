using System.Runtime.InteropServices;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8.App.Tests;

[CollectionDefinition("Native magazine rendering", DisableParallelization = true)]
public sealed class NativeMagazineRenderingCollection;

[Collection("Native magazine rendering")]
public sealed class NativeMagazineRenderTests
{
    [Fact]
    public void LiveMonoGameCanvasCanRenderToAnOffscreenTarget()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("MEMORIA_FF8_MAGAZINE_RENDER_DIR") is not null, "Set MEMORIA_FF8_MAGAZINE_RENDER_DIR for the installed Windows GPU render check.");
        using GameAssets assets = Assets();
        using RenderDevice render = new(assets.GameDirectory);
        using RenderTarget2D target = new(render.Device, 160, 120);
        render.Device.SetRenderTarget(target);
        render.Device.Clear(Color.Black);
        render.Batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        render.Canvas.Fill(new UiRect(20, 30, 40, 50), new UiColor(220, 30, 60));
        render.Batch.End();
        render.Device.SetRenderTarget(null);
        Color[] pixels = new Color[160 * 120];
        target.GetData(pixels);
        Assert.Equal(new Color(220, 30, 60), pixels[40 * 160 + 30]);
        Assert.Equal(Color.Black, pixels[0]);
    }

    [Theory]
    [InlineData(320, 240)]
    [InlineData(640, 480)]
    [InlineData(1280, 720)]
    [InlineData(1920, 1080)]
    [InlineData(3840, 2160)]
    public void EveryOriginalPageDrawsArtworkAndTextWithinItsFullPageViewport(int width, int height)
    {
        using GameAssets assets = Assets();
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        NativeMagazinePage[] pages = Enumerable.Range(177, 22).SelectMany(catalog.MagazinePages).ToArray();
        Assert.Equal(43, pages.Length);
        foreach (NativeMagazinePage page in pages)
        {
            UiView view = PageView(page, catalog, width, height);
            UiElement element = Assert.Single(Descendants(view.Root), element => element.Id == "magazine-page");
            Assert.Equal(0, element.Bounds.X);
            Assert.Equal(0, element.Bounds.Y);
            Assert.Equal(view.DesignWidth, element.Bounds.Width);
            Assert.InRange(element.Bounds.Height, view.DesignHeight * 0.85f, view.DesignHeight);
            BoundsCanvas canvas = new(width, height, page.PageId);
            view.Draw(canvas, new UiTheme());
            canvas.AssertComplete();
            float scale = Math.Min(width / view.DesignWidth, height / view.DesignHeight);
            UiRect expectedPaper = new((width - view.DesignWidth * scale) / 2, (height - view.DesignHeight * scale) / 2, element.Bounds.Width * scale, element.Bounds.Height * scale);
            Assert.InRange(Math.Abs(canvas.PaperBounds.X - expectedPaper.X), 0, 0.05f);
            Assert.InRange(Math.Abs(canvas.PaperBounds.Y - expectedPaper.Y), 0, 0.05f);
            Assert.InRange(Math.Abs(canvas.PaperBounds.Width - expectedPaper.Width), 0, 0.05f);
            Assert.InRange(Math.Abs(canvas.PaperBounds.Height - expectedPaper.Height), 0, 0.05f);
        }
    }

    [Fact]
    public void AllOriginalPagesRenderThroughTheLiveMonoGameCanvasAndRepresentativeImagesAreSaved()
    {
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_MAGAZINE_RENDER_DIR");
        Assert.SkipUnless(output is not null, "Set MEMORIA_FF8_MAGAZINE_RENDER_DIR for the installed Windows GPU render check.");
        Directory.CreateDirectory(output!);
        using GameAssets assets = Assets();
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        NativeMagazinePage[] pages = Enumerable.Range(177, 22).SelectMany(catalog.MagazinePages).ToArray();
        using RenderDevice render = new(assets.GameDirectory);
        int rendered = 0;
        foreach ((int width, int height) in new[] { (640, 480), (1280, 720), (3840, 2160) })
        {
            using RenderTarget2D target = new(render.Device, width, height, false, SurfaceFormat.Color, DepthFormat.None);
            foreach (NativeMagazinePage page in pages)
            {
                bool representative = page.PageId is 0 or 27 or 28 or 33 or 38 or 39 or 40 or 42;
                if (width != 640 && !representative)
                {
                    continue;
                }
                UiView view = PageView(page, catalog, width, height);
                render.Device.SetRenderTarget(target);
                render.Device.Clear(Color.Black);
                render.Batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
                view.Draw(render.Canvas, new UiTheme());
                render.Batch.End();
                render.Device.SetRenderTarget(null);
                Color[] pixels = new Color[width * height];
                target.GetData(pixels);
                Assert.True(pixels.Count(color => color.R > 10 || color.G > 10 || color.B > 10) > width * height / 4, $"Page {page.PageId} at {width}x{height} did not render a substantial visible page.");
                if (width == 640 || representative)
                {
                    string path = Path.Combine(output!, $"page-{page.PageId:D2}-{width}x{height}.png");
                    using FileStream stream = File.Create(path);
                    target.SaveAsPng(stream, width, height);
                }
                rendered++;
            }
        }
        Console.WriteLine($"Live MonoGameUiCanvas: {rendered} native magazine frames; all 43 pages at 640x480; eight representative pages at 1280x720 and 3840x2160. PNGs: {Path.GetFullPath(output!)}");
    }

    [Fact]
    public void ProceduralPaperSamplesFineGrainAtTheActual4KDestinationSize()
    {
        UiColor color = new(153, 106, 79, 231);
        Assert.Equal(UiPaper.CreatePixels(64, 48, color), UiPaper.CreatePixels(64, 48, color));
        UiColor[] paper = UiPaper.CreatePixels(3840, 2160, color);
        Assert.Equal(3840 * 2160, paper.Length);
        Assert.All(paper.Where((_, index) => index % 4096 == 0), pixel => Assert.Equal(color.A, pixel.A));
        int changedNeighbors = 0;
        int row = 1080 * 3840;
        for (int x = 960; x < 1984; x++)
        {
            if (paper[row + x] != paper[row + x + 1])
            {
                changedNeighbors++;
            }
        }
        Assert.True(changedNeighbors > 512, $"4K paper has only {changedNeighbors}/1024 distinct adjacent samples; it appears to be a scaled low-resolution texture.");
    }
    private static UiView PageView(NativeMagazinePage page, LiveMenuCatalog catalog, int width, int height)
    {
        MenuData data = new();
        data.Items.Add(new MenuItem { Id = $"item-{page.ItemId}", Name = catalog.ItemName(page.ItemId)!, Quantity = 1, NativeMagazinePages = catalog.MagazinePages(page.ItemId) });
        MenuController controller = new(() => data, _ =>
        {
        });
        controller.Open("item-use");
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal("magazine", controller.ScreenId);
        for (int index = 0; index < Array.FindIndex(data.Items[0].NativeMagazinePages, candidate => candidate.PageId == page.PageId); index++)
        {
            controller.Execute(new MenuAction("magazine-page", Value: "1"));
        }
        UiView view = controller.BuildView();
        view.Resize(width, height);
        return view;
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

    private sealed class BoundsCanvas(int width, int height, int pageId) : IUiCanvas
    {
        private readonly Stack<UiRect> clips = new();
        private int images;
        private int texts;
        private int papers;
        private readonly List<(string Value, float Size)> textSizes = [];
        public UiRect PaperBounds { get; private set; }

        public void AssertComplete()
        {
            Assert.Empty(clips);
            Assert.True(images > 0, $"Page {pageId} contains no original artwork.");
            Assert.True(texts > 0, $"Page {pageId} contains no text.");
            Assert.Equal(1, papers);
            if (pageId < 28)
            {
                float bodySize = textSizes.Max(text => text.Size);
                foreach (var count in textSizes.Where(text => text.Value.Length is 1 or 2 && text.Value.All(char.IsAsciiDigit)))
                {
                    Assert.True(count.Size >= bodySize * 0.75f, $"Page {pageId} recipe count {count.Value} is too small to read beside its material name.");
                }
            }
        }

        public void Paper(UiRect bounds, UiColor color)
        {
            Check(bounds);
            papers++;
            PaperBounds = bounds;
        }

        public void Fill(UiRect bounds, UiColor color)
        {
            Check(bounds);
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color)
        {
            Text(text, fontSize, bounds, color, UiAlignment.Start);
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color, string? fontFamily)
        {
            Check(bounds);
            Assert.True(fontSize > 0 && float.IsFinite(fontSize));
            float advance = SystemTextLayout.MeasureAdvance(text, fontSize, fontFamily);
            Assert.True(advance <= bounds.Width + 1, $"Page {pageId} text exceeds its row width: {text}; advance {advance}, bounds {bounds.Width}.");
            if (width == 640 || pageId == 39)
            {
                using var glyphs = SystemTextLayout.Rasterize(text, fontSize, bounds.Width, fontFamily);
                Assert.True(glyphs.Height / (float)SystemTextLayout.RasterScale <= bounds.Height + 1, $"Page {pageId} text is vertically clipped: {text}.");
            }
            textSizes.Add((text, fontSize));
            texts++;
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment)
        {
            Text(text, fontSize, bounds, color, (string?)null);
        }

        public void Image(UiImageSource source, UiRect bounds)
        {
            Check(bounds);
            Assert.True(source.Kind is UiImageKind.Magazine or UiImageKind.Icon or UiImageKind.MagazineButton);
            images++;
        }

        public void MenuPointer(UiRect bounds)
        {
            Check(bounds);
            Assert.True(bounds.Intersect(PaperBounds).IsEmpty, $"Page {pageId} includes a menu pointer over the magazine composition.");
        }

        public void Line(float x1, float y1, float x2, float y2, UiColor color, float thickness)
        {
            Check(new UiRect(Math.Min(x1, x2), Math.Min(y1, y2), Math.Max(thickness, Math.Abs(x2 - x1)), Math.Max(thickness, Math.Abs(y2 - y1))));
        }

        public void PushClip(UiRect bounds)
        {
            Check(bounds);
            clips.Push(clips.Count == 0 ? bounds : bounds.Intersect(clips.Peek()));
        }

        public void PopClip()
        {
            Assert.NotEmpty(clips);
            clips.Pop();
        }

        private void Check(UiRect bounds)
        {
            Assert.True(float.IsFinite(bounds.X) && float.IsFinite(bounds.Y) && float.IsFinite(bounds.Width) && float.IsFinite(bounds.Height), $"Page {pageId} contains non-finite bounds.");
            if (bounds.IsEmpty)
            {
                return;
            }
            Assert.InRange(bounds.X, -0.01f, width + 0.01f);
            Assert.InRange(bounds.Y, -0.01f, height + 0.01f);
            Assert.InRange(bounds.Right, -0.01f, width + 0.01f);
            Assert.InRange(bounds.Bottom, -0.01f, height + 0.01f);
        }
    }
    private static GameAssets Assets()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original magazine resources are required.");
        return new GameAssets(directory);
    }

    private sealed class RenderDevice : IDisposable
    {
        private readonly nint window;
        private readonly Texture2D pixel;
        private readonly SystemTextRenderer text;

        public RenderDevice(string gameDirectory)
        {
            window = CreateWindowExW(0, "STATIC", "Memoria offscreen magazine validation", 0, 0, 0, 64, 64, 0, 0, 0, 0);
            Assert.True(window != 0, $"Cannot create hidden render window: {Marshal.GetLastWin32Error()}.");
            Device = new GraphicsDevice(GraphicsAdapter.DefaultAdapter, GraphicsProfile.HiDef, new PresentationParameters
            {
                DeviceWindowHandle = window,
                BackBufferWidth = 64,
                BackBufferHeight = 64,
                BackBufferFormat = SurfaceFormat.Color,
                DepthStencilFormat = DepthFormat.None,
                IsFullScreen = false,
                PresentationInterval = PresentInterval.Immediate
            });
            Batch = new SpriteBatch(Device);
            pixel = new Texture2D(Device, 1, 1);
            pixel.SetData(new[] { Color.White });
            text = new SystemTextRenderer(Device);
            Canvas = new MonoGameUiCanvas(Device, Batch, pixel, text, gameDirectory);
        }

        public GraphicsDevice Device { get; }
        public SpriteBatch Batch { get; }
        public MonoGameUiCanvas Canvas { get; }

        public void Dispose()
        {
            Canvas.Dispose();
            text.Dispose();
            pixel.Dispose();
            Batch.Dispose();
            Device.Dispose();
            DestroyWindow(window);
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern nint CreateWindowExW(uint extendedStyle, string className, string windowName, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyWindow(nint window);
    }
}











