using System.Diagnostics;
using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class OpeningMovieCompletionTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Host_presents_the_white_terminal_surface_after_a_control_skip()
    {
        string game = TestResources.GameDirectory;
        string source = TestResources.FixturePath("opening-completion-admission");
        string root = Path.Combine(Path.GetTempPath(), "memoria-movie-completion-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "requests"));
        string capture = Path.Combine(root, "terminal.png");
        File.WriteAllText(Path.Combine(root, "requests", "001.json"), "{\"Id\":\"skip\",\"Command\":\"action\",\"Action\":{\"Kind\":\"SkipMovie\"}}");
        MovieClock clock = new(new GameSession(GameSession.Load(source)), game, Environment.GetEnvironmentVariable("MEMORIA_FF8_MEDIA_TOOLS"));
        ProcessStartInfo start = new(Path.Combine(AppContext.BaseDirectory, "Memoria.FF8.exe")) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in new[] { "--state", source, "--game-dir", game, "--media-tools", clock.ToolsDirectory, "--control-dir", root, "--control-step", "--resolution", "320x240", "--capture-frame", capture, "--capture-after", "1", "--exit-after", "3" })
        {
            start.ArgumentList.Add(argument);
        }
        using Process process = Process.Start(start)!;
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        Task<string> stderr = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        try
        {
            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            await process.WaitForExitAsync(timeout.Token);
            Assert.Equal(0, process.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(await stderr), await stderr);
            Assert.True(File.Exists(capture), await stdout);
            using JsonDocument response = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "responses", "001.json")));
            Assert.True(response.RootElement.GetProperty("Ok").GetBoolean());
            using SkiaSharp.SKBitmap image = SkiaSharp.SKBitmap.Decode(capture);
            for (int y = 20; y < 220; y++)
            {
                for (int x = 20; x < 300; x++)
                {
                    SkiaSharp.SKColor pixel = image.GetPixel(x, y);
                    Assert.True(pixel.R >= 240 && pixel.G >= 240 && pixel.B >= 240, $"Host retained a nonwhite pixel at {x},{y}: {pixel}.");
                }
            }
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(TestContext.Current.CancellationToken);
            }
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Skipped_opening_retains_its_actual_white_video_frame_before_the_infirmary_fades_in()
    {
        string game = TestResources.GameDirectory;
        GameSession session = new(GameSession.Load(TestResources.FixturePath("opening-completion-admission")));
        MovieClock clock = new(session, game, Environment.GetEnvironmentVariable("MEMORIA_FF8_MEDIA_TOOLS"));
        using GameWorld world = new(session, game, clock);
        world.EnsureField();
        Assert.Equal(74, session.State.FieldId);
        Assert.Equal(30, session.State.MovieIndex);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.SkipMovie)).Accepted);
        Assert.Equal(2896, session.State.MoviePlayback!.FrameIndex);
        JsonElement state = JsonSerializer.SerializeToElement(session.State);
        JsonElement vm = world.Events.Capture();
        Stopwatch timer = Stopwatch.StartNew();
        MovieCompletionFrame retained = Assert.IsType<MovieCompletionFrame>(clock.ReadSkippedCompletionFrame());
        output.WriteLine($"Terminal frame seek: {timer.Elapsed.TotalMilliseconds:F1} ms; frame {retained.FrameIndex}.");
        Assert.Equal(2896, retained.FrameIndex);
        Assert.True(JsonElement.DeepEquals(state, JsonSerializer.SerializeToElement(session.State)));
        Assert.True(JsonElement.DeepEquals(vm, world.Events.Capture()));
        int white = 0;
        for (int index = 0; index < retained.Pixels.Length; index += 4)
        {
            if (retained.Pixels[index] >= 240 && retained.Pixels[index + 1] >= 240 && retained.Pixels[index + 2] >= 240)
            {
                white++;
            }
        }
        Assert.True(white > retained.Width * retained.Height * 0.99, $"Terminal video surface has only {white} white pixels.");
        for (int callback = 0; callback < 10 && (session.State.FieldId != 180 || world.Events.Frame < 2); callback++)
        {
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
            world.EnsureField();
        }
        Assert.Equal(180, session.State.FieldId);
        Assert.True(world.CanPresentCurrentField);
        Assert.Equal(new NativeFadeDraw(NativeFadeDrawMode.Add, 255, 255, 255), world.Events.FieldFade!.Draw);
        byte[] pixels = world.Render(TimeSpan.Zero);
        for (int index = 0; index < pixels.Length; index += 4)
        {
            Assert.Equal((byte)255, pixels[index]);
            Assert.Equal((byte)255, pixels[index + 1]);
            Assert.Equal((byte)255, pixels[index + 2]);
        }
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.Equal(new NativeFadeDraw(NativeFadeDrawMode.Add, 254, 254, 254), world.Events.FieldFade.Draw);
        Assert.Null(world.Events.LastFault);
    }
}
