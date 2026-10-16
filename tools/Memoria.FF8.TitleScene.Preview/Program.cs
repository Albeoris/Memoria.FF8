using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.TitleScene;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8.TitleScene.Preview;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            PreviewOptions options = PreviewOptions.Parse(args);
            using PreviewGame game = new(options);
            game.Run();
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}

internal sealed record PreviewOptions(int Width, int Height, double Time, string Output, bool Benchmark, bool BackgroundOnly)
{
    public static PreviewOptions Parse(string[] args)
    {
        PreviewOptions options = new(1440, 1080, 4, Path.GetFullPath("title-scene.png"), false, false);
        for (int index = 0; index < args.Length; index++)
        {
            string argument = args[index];
            string Value()
            {
                if (++index >= args.Length)
                {
                    throw new ArgumentException($"Missing value for {argument}.");
                }
                return args[index];
            }

            switch (argument)
            {
                case "--resolution":
                    string[] size = Value().Split('x');
                    if (size.Length != 2 || !int.TryParse(size[0], out int width) || !int.TryParse(size[1], out int height) || width < 64 || height < 64 || width > 7680 || height > 4320)
                    {
                        throw new ArgumentException("Resolution must be WxH within 64x64..7680x4320.");
                    }
                    options = options with { Width = width, Height = height };
                    break;
                case "--time":
                    double time = double.Parse(Value(), CultureInfo.InvariantCulture);
                    if (!double.IsFinite(time) || time < 0)
                    {
                        throw new ArgumentException("Time must be finite and nonnegative.");
                    }
                    options = options with { Time = time };
                    break;
                case "--output":
                    options = options with { Output = Path.GetFullPath(Value()) };
                    break;
                case "--benchmark":
                    options = options with { Benchmark = true };
                    break;
                case "--background-only":
                    options = options with { BackgroundOnly = true };
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {argument}. Options: --resolution WxH --time seconds --output path.png --benchmark --background-only.");
            }
        }
        return options;
    }
}

/// <summary>Independent capture and fenced throughput probe; no game installation or save files required.</summary>
internal sealed class PreviewGame : Game
{
    private readonly PreviewOptions options;
    private readonly GraphicsDeviceManager graphics;
    private TitleSceneRenderer scene = null!;
    private TitleArtworkRenderer? artwork;
    private Color[] pixels = [];
    private int frame;
    private long started;
    private long allocatedAtStart;
    private double initializationMilliseconds;

    public PreviewGame(PreviewOptions options)
    {
        this.options = options;
        graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = options.Width,
            PreferredBackBufferHeight = options.Height,
            SynchronizeWithVerticalRetrace = false,
            GraphicsProfile = GraphicsProfile.HiDef,
            PreferMultiSampling = true
        };
        IsFixedTimeStep = false;
        InactiveSleepTime = TimeSpan.Zero;
        Window.Title = "Memoria — procedural Title preview";
    }

    protected override void LoadContent()
    {
        long initializationStarted = Stopwatch.GetTimestamp();
        scene = new TitleSceneRenderer(GraphicsDevice);
        if (!options.BackgroundOnly)
        {
            artwork = new TitleArtworkRenderer(GraphicsDevice);
        }
        pixels = new Color[GraphicsDevice.PresentationParameters.BackBufferWidth * GraphicsDevice.PresentationParameters.BackBufferHeight];
        initializationMilliseconds = Stopwatch.GetElapsedTime(initializationStarted).TotalMilliseconds;
    }

    protected override void Draw(GameTime gameTime)
    {
        if (frame == 30 && options.Benchmark)
        {
            GraphicsDevice.GetBackBufferData(pixels);
            started = Stopwatch.GetTimestamp();
            allocatedAtStart = GC.GetAllocatedBytesForCurrentThread();
        }
        double seconds = options.Time + (options.Benchmark ? frame / 60d : 0);
        Render(seconds);
        if (frame == (options.Benchmark ? 149 : 2))
        {
            GraphicsDevice.GetBackBufferData(pixels);
            double? milliseconds = options.Benchmark ? Stopwatch.GetElapsedTime(started).TotalMilliseconds / 120 : null;
            double? allocatedBytesPerFrame = options.Benchmark ? (GC.GetAllocatedBytesForCurrentThread() - allocatedAtStart) / 120d : null;
            // Capture the exact requested instant, independently of warm-up and benchmark duration.
            Render(options.Time);
            GraphicsDevice.GetBackBufferData(pixels);
            Color[] reference = (Color[])pixels.Clone();
            string hash = PixelHash(pixels);
            Render(options.Time + 8);
            GraphicsDevice.GetBackBufferData(pixels);
            int changedPixels = reference.Where((color, index) => color != pixels[index]).Count();
            if (changedPixels == 0)
            {
                throw new InvalidOperationException("Animation check failed: time + 8 produced an identical frame.");
            }
            Render(options.Time);
            GraphicsDevice.GetBackBufferData(pixels);
            int repeatDifferentPixels = 0;
            int maximumRepeatDifference = 0;
            bool alphaMatches = true;
            for (int index = 0; index < pixels.Length; index++)
            {
                Color first = reference[index];
                Color repeat = pixels[index];
                if (first != repeat)
                {
                    repeatDifferentPixels++;
                }
                maximumRepeatDifference = Math.Max(maximumRepeatDifference, Math.Max(Math.Abs(first.R - repeat.R), Math.Max(Math.Abs(first.G - repeat.G), Math.Abs(first.B - repeat.B))));
                alphaMatches &= first.A == repeat.A;
            }
            int repeatTolerance = options.BackgroundOnly ? 0 : 1;
            if (!alphaMatches || maximumRepeatDifference > repeatTolerance)
            {
                SaveImage(Path.ChangeExtension(options.Output, ".first.png"), reference);
                SaveImage(Path.ChangeExtension(options.Output, ".repeat.png"), pixels);
                throw new InvalidOperationException($"Repeat check failed: {repeatDifferentPixels} different pixels, maximum RGB difference {maximumRepeatDifference}, tolerance {repeatTolerance}, alpha matches {alphaMatches}; diagnostic images written.");
            }
            Directory.CreateDirectory(Path.GetDirectoryName(options.Output)!);
            SaveImage(options.Output, pixels);
            var report = new
            {
                Scene = "Title",
                Field = (int?)null,
                Frame = 0,
                AnimationSeconds = options.Time,
                options.Width,
                options.Height,
                options.BackgroundOnly,
                Adapter = GraphicsDevice.Adapter.Description,
                MultiSampleCount = GraphicsDevice.PresentationParameters.MultiSampleCount,
                AverageFrameMilliseconds = milliseconds,
                InitializationMilliseconds = initializationMilliseconds,
                Measurement = "120 frames after 30 warm-up frames, vsync off, one readback fence at each boundary; includes CPU, GPU, presentation and final readback, not isolated GPU timing",
                GraphicsThreadAllocatedBytesPerFrame = allocatedBytesPerFrame,
                AllocationScope = "Entire preview draw/presentation loop, including framework and driver; not isolated library allocations",
                PixelSha256 = PixelHash(pixels),
                FirstPassPixelSha256 = hash,
                ChangedPixelsAfterEightSeconds = changedPixels,
                ExactRepeat = repeatDifferentPixels == 0,
                RepeatDifferentPixels = repeatDifferentPixels,
                MaximumRepeatRgbDifference = maximumRepeatDifference,
                RepeatRgbTolerance = repeatTolerance,
                RepeatAlphaMatches = alphaMatches,
                Output = options.Output
            };
            string json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.ChangeExtension(options.Output, ".json"), json);
            Console.WriteLine(json);
            Exit();
        }
        frame++;
        base.Draw(gameTime);
    }

    private void Render(double seconds)
    {
        GraphicsDevice.Clear(Color.Black);
        Rectangle bounds = GraphicsDevice.Viewport.Bounds;
        scene.Draw(bounds, seconds);
        artwork?.Draw(bounds, seconds);
    }

    private static string PixelHash(Color[] colors)
    {
        ReadOnlySpan<byte> bytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(colors.AsSpan());
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private void SaveImage(string path, Color[] colors)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using Texture2D texture = new(GraphicsDevice, options.Width, options.Height);
        using Stream output = File.Create(path);
        texture.SetData(colors);
        texture.SaveAsPng(output, options.Width, options.Height);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            artwork?.Dispose();
            scene?.Dispose();
        }
        base.Dispose(disposing);
    }
}
