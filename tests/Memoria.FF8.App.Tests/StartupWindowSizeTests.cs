using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class StartupWindowSizeTests
{
    [Fact]
    public void Natural_startup_preserves_client_backbuffer_and_viewport_dimensions()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("MEMORIA_FF8_STARTUP_WINDOW_TEST") == "1", "Set MEMORIA_FF8_STARTUP_WINDOW_TEST=1 to run the visible startup window regression without resizing the game.");
        Assert.SkipUnless(OperatingSystem.IsWindows(), "The startup window regression requires Windows.");
        string gameDirectory = TestResources.GameDirectory;
        string toolsDirectory = MediaToolsInstallation.ResolveDirectory();
        Assert.SkipUnless(File.Exists(Path.Combine(toolsDirectory, "ffmpeg.exe")) && File.Exists(Path.Combine(toolsDirectory, "ffprobe.exe")), "Set MEMORIA_FF8_MEDIA_TOOLS to installed FFmpeg tools.");
        string logPath = Environment.GetEnvironmentVariable("MEMORIA_FF8_STARTUP_WINDOW_LOG") ?? TestResources.ArtifactFile("startup-window-size.json");
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                LaunchOptions options = new()
                {
                    GameDirectory = gameDirectory,
                    MediaToolsDirectory = toolsDirectory,
                    SavePath = TestResources.ArtifactFile("startup-window-unused-save.json"),
                    ExitAfterSeconds = 15,
                    MuteAudio = true
                };
                using RecreateGame game = new(options, GameSession.CreateColdProcess());
                using WindowObserver observer = new(game);
                game.Components.Add(observer);
                try
                {
                    game.Run();
                }
                finally
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(logPath))!);
                    File.WriteAllText(logPath, JsonSerializer.Serialize(observer.Samples, new JsonSerializerOptions { WriteIndented = true }));
                }
                Assert.Null(ReadField<string>(game, "error"));
                Assert.Contains(observer.Samples, sample => sample.Stage == StartupStage.SquareEnixMovie && sample.HasMovieFrame);
                Assert.Contains(observer.Samples, sample => sample.Stage == StartupStage.PublisherMovie && sample.HasMovieFrame);
                Assert.Contains(observer.Samples, sample => sample.Stage == StartupStage.SquareLogo);
                Assert.NotEmpty(observer.Samples);
                WindowSample initial = observer.Samples[0];
                Assert.True(initial.ClientWidth > 0 && initial.ClientHeight > 0, $"Invalid initial client dimensions. See {logPath}.");
                Assert.All(observer.Samples, sample =>
                {
                    Assert.Equal((initial.ClientWidth, initial.ClientHeight), (sample.ClientWidth, sample.ClientHeight));
                    Assert.Equal((initial.NativeWindowWidth, initial.NativeWindowHeight), (sample.NativeWindowWidth, sample.NativeWindowHeight));
                    Assert.Equal(initial.Dpi, sample.Dpi);
                    Assert.Equal(initial.DpiAwareness, sample.DpiAwareness);
                    Assert.Equal((sample.ClientWidth, sample.ClientHeight), (sample.BackBufferWidth, sample.BackBufferHeight));
                    Assert.Equal((sample.ClientWidth, sample.ClientHeight), (sample.ViewportWidth, sample.ViewportHeight));
                });
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private static T? ReadField<T>(RecreateGame game, string name)
    {
        return (T?)typeof(RecreateGame).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(game);
    }

    private sealed class WindowObserver(RecreateGame game) : DrawableGameComponent(game)
    {
        private readonly Stopwatch elapsed = Stopwatch.StartNew();

        public List<WindowSample> Samples { get; } = [];

        public override void Initialize()
        {
            ObserveWindow();
            base.Initialize();
        }

        public override void Draw(GameTime gameTime)
        {
            ObserveWindow();
            base.Draw(gameTime);
        }

        private void ObserveWindow()
        {
            StartupSequence startup = ReadField<StartupSequence>(game, "startup")!;
            Rectangle client = game.Window.ClientBounds;
            PresentationParameters presentation = GraphicsDevice.PresentationParameters;
            Viewport viewport = GraphicsDevice.Viewport;
            Assert.True(GetWindowRect(game.Window.Handle, out NativeRectangle nativeWindow));
            Assert.True(GetClientRect(game.Window.Handle, out NativeRectangle nativeClient));
            GraphicsDeviceManager graphics = ReadField<GraphicsDeviceManager>(game, "graphics")!;
            WindowSample sample = new(startup.Stage, ReadField<MoviePresentation>(game, "startupMovie")?.HasFrame == true, client.Width, client.Height, presentation.BackBufferWidth, presentation.BackBufferHeight, viewport.Width, viewport.Height, graphics.PreferredBackBufferWidth, graphics.PreferredBackBufferHeight, nativeWindow.Right - nativeWindow.Left, nativeWindow.Bottom - nativeWindow.Top, nativeClient.Right, nativeClient.Bottom, GetDpiForWindow(game.Window.Handle), GetAwarenessFromDpiAwarenessContext(GetWindowDpiAwarenessContext(game.Window.Handle)));
            if (Samples.Count == 0 || Samples[^1] != sample)
            {
                Samples.Add(sample);
            }
        }

        public override void Update(GameTime gameTime)
        {
            if (elapsed.Elapsed >= TimeSpan.FromSeconds(30))
            {
                game.Exit();
            }
            base.Update(gameTime);
        }
    }

    private sealed record WindowSample(StartupStage Stage, bool HasMovieFrame, int ClientWidth, int ClientHeight, int BackBufferWidth, int BackBufferHeight, int ViewportWidth, int ViewportHeight, int PreferredWidth, int PreferredHeight, int NativeWindowWidth, int NativeWindowHeight, int NativeClientWidth, int NativeClientHeight, uint Dpi, int DpiAwareness);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRectangle
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint window, out NativeRectangle rectangle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(nint window, out NativeRectangle rectangle);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint window);

    [DllImport("user32.dll")]
    private static extern nint GetWindowDpiAwarenessContext(nint window);

    [DllImport("user32.dll")]
    private static extern int GetAwarenessFromDpiAwarenessContext(nint value);
}
