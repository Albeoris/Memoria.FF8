using Memoria.FF8.Resources;
using System.Diagnostics;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Media;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class OpeningMoviePresentationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NewGameHostPresentsVisibleOpeningMovieOnTheBackBuffer(bool softwareRenderer)
    {
        string game = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(game), "Installed original opening movie and field corpus is required.");
        string root = Path.Combine(Path.GetTempPath(), "memoria-opening-host-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "requests"));
        string settings = Path.Combine(root, "settings");
        Directory.CreateDirectory(settings);
        File.WriteAllText(Path.Combine(settings, "ui-settings.json"), JsonSerializer.Serialize(new { MusicVolume = 0, SoundVolume = 0, VoiceVolume = 0 }));
        string capture = Path.Combine(root, "opening.png");
        File.WriteAllText(Path.Combine(root, "requests", "001.json"), "{\"Id\":\"new-game\",\"Command\":\"new-game\"}");
        MovieClock clock = new(GameSession.CreateColdProcess(), game, Environment.GetEnvironmentVariable("MEMORIA_FF8_MEDIA_TOOLS"));
        ProcessStartInfo start = new(Path.Combine(AppContext.BaseDirectory, "Memoria.FF8.exe")) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in new[] { "--game-dir", game, "--media-tools", clock.ToolsDirectory, "--control-dir", root, "--settings-dir", settings, "--resolution", "320x240", "--exit-after", "40" })
        {
            start.ArgumentList.Add(argument);
        }
        if (softwareRenderer)
        {
            start.ArgumentList.Add("--software-renderer");
        }
        using Process process = Process.Start(start)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        Task<string> errors = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        try
        {
            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(45));
            int sequence = 2;
            JsonElement state;
            do
            {
                state = await Request("status");
                if (state.GetProperty("Scene").GetString() == "Movie" && state.GetProperty("MoviePlayback") is { ValueKind: JsonValueKind.Object } playback && playback.GetProperty("FrameIndex").GetInt32() >= 150)
                {
                    break;
                }
                await Task.Delay(20, timeout.Token);
            }
            while (!process.HasExited);
            Assert.Equal("Movie", state.GetProperty("Scene").GetString());
            Assert.True(state.GetProperty("MoviePlayback").GetProperty("FrameIndex").GetInt32() >= 150);
            state = await Request("status", compact: false);
            await Request("capture", capture);
            string quit = Path.Combine(root, "requests", $"{sequence:D3}.json");
            File.WriteAllText(quit + ".tmp", JsonSerializer.Serialize(new { Id = "quit", Command = "quit" }));
            File.Move(quit + ".tmp", quit);
            await process.WaitForExitAsync(timeout.Token);

            async Task<JsonElement> Request(string command, string? path = null, bool compact = true)
            {
                string id = $"{sequence++:D3}";
                string request = Path.Combine(root, "requests", id + ".json");
                string response = Path.Combine(root, "responses", id + ".json");
                File.WriteAllText(request + ".tmp", JsonSerializer.Serialize(new { Id = id, Command = command, Compact = compact, Path = path }));
                File.Move(request + ".tmp", request);
                while (!File.Exists(response) && !process.HasExited)
                {
                    await Task.Delay(20, timeout.Token);
                }
                Assert.True(File.Exists(response), $"Host exited before {command} receipt {id}.");
                using JsonDocument receipt = JsonDocument.Parse(File.ReadAllText(response));
                Assert.True(receipt.RootElement.GetProperty("Ok").GetBoolean(), receipt.RootElement.GetRawText());
                return receipt.RootElement.GetProperty("Result").Clone();
            }

            string stdout = await output;
            string stderr = await errors;
            Assert.True(process.ExitCode == 0, stdout + Environment.NewLine + stderr);
            Assert.True(string.IsNullOrWhiteSpace(stderr), stderr);
            Assert.Contains("movie-frame=0", stdout);
            Assert.True(File.Exists(capture), stdout);

            Assert.False(state.GetProperty("FieldFaulted").GetBoolean());
            Assert.False(state.GetProperty("FieldRenderFaulted").GetBoolean());
            Assert.Equal(JsonValueKind.Null, state.GetProperty("Error").ValueKind);
            Assert.Equal("Movie", state.GetProperty("Scene").GetString());
            Assert.True(state.GetProperty("MoviePlayback").GetProperty("FrameIndex").GetInt32() >= 150);
            using SkiaSharp.SKBitmap image = SkiaSharp.SKBitmap.Decode(capture);
            int visible = 0;
            // Window preferences can select a larger backbuffer. Sample the actual
            // native movie viewport, excluding letterboxing and the skip hint.
            PixelBounds content = ViewportLayout.Fit(320, 224, image.Width, image.Height);
            for (int y = content.Y + 40 * content.Height / 224; y < content.Y + 180 * content.Height / 224; y++)
            {
                for (int x = content.X + 20 * content.Width / 320; x < content.X + 300 * content.Width / 320; x++)
                {
                    SkiaSharp.SKColor pixel = image.GetPixel(x, y);
                    if (pixel.R > 16 || pixel.G > 16 || pixel.B > 16)
                    {
                        visible++;
                    }
                }
            }
            if (visible <= 100 && Environment.GetEnvironmentVariable("MEMORIA_FF8_OPENING_MOVIE_EVIDENCE") is string evidenceDirectory)
            {
                Directory.CreateDirectory(evidenceDirectory);
                string evidence = Path.Combine(evidenceDirectory, "opening-host-" + (softwareRenderer ? "software-" : "gpu-") + Guid.NewGuid().ToString("N") + ".png");
                File.Copy(capture, evidence);
                File.WriteAllText(Path.ChangeExtension(evidence, ".json"), state.GetRawText());
                Console.WriteLine("Opening movie failure capture: " + evidence);
            }
            Assert.True(visible > 100, $"The actual host presented a black movie: {visible} visible pixels. Status before capture: {state.GetRawText()} {stdout}");
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
            }
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FreshOpeningDisplaysDecodedMovieDespiteRetainedBlackFieldFade()
    {
        using IResourceSource movieSource = GameDataSourceFactory.Open(TestResources.GameDirectory);
        IGameFileSource movieFiles = Assert.IsAssignableFrom<IGameFileSource>(movieSource);

        string game = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(game), "Installed original opening movie and field corpus is required.");
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        MovieClock clock = new(session, game, Environment.GetEnvironmentVariable("MEMORIA_FF8_MEDIA_TOOLS"));
        using GameWorld world = new(session, game, clock);
        for (int callback = 0; !session.IsMoviePlaying && callback < 60; callback++)
        {
            world.EnsureField();
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        }
        Assert.True(session.IsMoviePlaying);
        Assert.Equal(74, session.State.FieldId);
        Assert.Equal(30, session.State.MovieIndex);
        MovieInformation information = clock.Prepare();
        Assert.Equal(2925, information.FrameCount);
        world.SetRenderSize(320, 224);
        NativeFieldFadeState fade = Assert.IsType<NativeFieldFadeState>(world.Events.FieldFade);
        Assert.Equal(NativeFadeDrawMode.Black, fade.Draw.Mode);
        byte[] cameraBytes = File.ReadAllBytes(MovieClock.GetCameraPath(movieFiles, clock.SourceDiscIndex, session.State.MovieIndex));
        using FfmpegMovieDecoder decoder = new(clock.ToolsDirectory, clock.SourcePath, information: information);
        Stopwatch timeout = Stopwatch.StartNew();
        HashSet<int> rendered = [];
        while (rendered.Count < 3 && timeout.Elapsed < TimeSpan.FromSeconds(15))
        {
            while (decoder.TryGetAudio(out _))
            {
            }
            if (!decoder.TryGetNextFrame(double.MaxValue, out MovieFrame? frame))
            {
                Assert.Null(decoder.Error);
                Assert.False(decoder.VideoFinished, "Opening decoder ended before the observed movie frames.");
                Thread.Sleep(1);
                continue;
            }
            int index = checked((int)frame!.Index);
            Assert.True(world.AdvanceMovieFrame(new(index, information.FrameCount - 1, true, false, false), index / information.FramesPerSecond));
            if (index is not (0 or 75 or 150))
            {
                continue;
            }
            Assert.True(world.CanPresentCurrentField);
            Assert.Equal(fade, world.Events.FieldFade);
            JsonElement before = world.Events.Capture();
            FieldCamera camera = FieldCamera.Read(cameraBytes.AsSpan(8 + Math.Max(1, index) * 44, 40).ToArray());
            FieldRenderer reference = new(world.Field!, 320, 224, nativeMovieViewport: true);
            byte[] expected = reference.RenderMovieFrame(frame.Rgba, information.Width, information.Height, camera, [], null).ToArray();
            if (index == 150)
            {
                Assert.Contains(Enumerable.Range(0, expected.Length / 4), pixel => expected[pixel * 4] > 16 || expected[pixel * 4 + 1] > 16 || expected[pixel * 4 + 2] > 16);
            }
            Assert.Equal(expected, world.RenderMovieFrame(frame.Rgba, information.Width, information.Height));
            Assert.Equal(expected, world.RenderMovieFrame(frame.Rgba, information.Width, information.Height));
            Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));
            rendered.Add(index);
        }
        Assert.Equal(3, rendered.Count);
    }
}
