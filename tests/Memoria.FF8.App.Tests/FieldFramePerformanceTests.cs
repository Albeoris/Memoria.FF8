using System.Diagnostics;
using System.Text.Json;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Memoria.FF8.GameUi;
using Memoria.FF8.Events;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class FieldFramePerformanceTests
{
    [Fact]
    public void Cold_new_game_saves_the_first_confirmable_infirmary_dialogue()
    {
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_FIELD_PERFORMANCE_OUTPUT");
        Assert.SkipUnless(output is not null, "An optional diagnostic output directory is required.");
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original field corpus is required.");
        Directory.CreateDirectory(output!);
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using GameWorld world = new(session, directory);
        for (int input = 0; input < 3000; input++)
        {
            world.EnsureField();
            Assert.Null(world.Events.LastFault);
            DialogueState? dialogue = world.Events.CurrentDialogue;
            if (session.State.Scene == SceneKind.Field && world.Field!.Id == 180 && dialogue is { CanConfirm: true } && world.CanPresentCurrentField && !FieldPresentationReadiness.HasPendingInitialization(world.Events.Scene.NativeFieldInitialization))
            {
                string snapshot = Path.Combine(output!, "opening-infirmary.json");
                session.SetEventState(world.Events.Capture());
                session.Save(snapshot, compact: true);
                using GameWorld restored = new(new GameSession(GameSession.Load(snapshot)), directory);
                restored.EnsureField();
                Assert.Equal(180, restored.Field!.Id);
                Assert.Equal(world.Events.Frame, restored.Events.Frame);
                Assert.True(restored.Events.CurrentDialogue?.CanConfirm);
                Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
                return;
            }
            PlayerAction action = session.IsMoviePlaying ? new PlayerAction(PlayerActionKind.SkipMovie) : dialogue is { CanConfirm: true, IsNamePrompt: true } ? new PlayerAction(PlayerActionKind.EnterName, Text: "Cecilia") : dialogue is { CanConfirm: true, FirstChoice: int choice } ? new PlayerAction(PlayerActionKind.Choose, choice) : dialogue is { CanConfirm: true } ? new PlayerAction(PlayerActionKind.Confirm) : new PlayerAction(PlayerActionKind.Wait);
            PlayerActionResult result = world.Apply(action);
            Assert.True(result.Accepted, $"Opening input {action} at field{world.Events.FieldId}/frame{world.Events.Frame}: {result.Detail}");
            world.CommitFieldPresentation();
        }
        Assert.Fail($"Fresh opening infirmary exceeded its bounded action budget: field{world.Events.FieldId}/frame{world.Events.Frame}, trigger {world.Events.NextTrigger}, dialogue {world.Events.CurrentDialogue}, fault {world.Events.LastFault}.");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void Named_standalone_runs_inside_the_complete_field_graphics_host(int speed)
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_FIELD_PERFORMANCE_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_FIELD_PERFORMANCE_OUTPUT");
        Assert.SkipUnless(Environment.GetEnvironmentVariable("MEMORIA_FF8_FIELD_HOST_PERFORMANCE") == "1" && source is not null && File.Exists(source) && output is not null, "An optional standalone, output directory and interactive graphics diagnostic opt-in are required.");
        output = Path.Combine(output!, $"host-x{speed}");
        Directory.CreateDirectory(output);
        GameSession session = new(GameSession.Load(source!));
        using GameWorld initial = new(new GameSession(GameSession.Load(source!)), TestResources.GameDirectory);
        initial.EnsureField();
        long initialFrame = initial.Events.Frame;
        int initialField = initial.Field!.Id;
        string directory = TestResources.ArtifactFile("field-host");
        Directory.CreateDirectory(directory);
        (int width, int height) = PerformanceResolution();
        bool audio = Environment.GetEnvironmentVariable("MEMORIA_FF8_FIELD_PERFORMANCE_AUDIO") == "1";
        LaunchOptions options = new() { GameDirectory = TestResources.GameDirectory, MediaToolsDirectory = Environment.GetEnvironmentVariable("MEMORIA_FF8_MEDIA_TOOLS"), InitialState = session.State, StatePath = source, SavePath = Path.Combine(directory, "unused.json"), MuteAudio = !audio, ExitAfterSeconds = 12, ResolutionSpecified = true, Width = width, Height = height, CaptureFramePath = Path.Combine(output, "host.png"), CaptureAfterSeconds = 8 };
        using RecreateGame game = new(options, session);
        // Keep the diagnostic GPU workload at the requested size even on a smaller desktop.
        Get<Microsoft.Xna.Framework.GraphicsDeviceManager>(game, "graphics").PreparingDeviceSettings += (_, settings) =>
        {
            settings.GraphicsDeviceInformation.PresentationParameters.BackBufferWidth = width;
            settings.GraphicsDeviceInformation.PresentationParameters.BackBufferHeight = height;
        };
        Set(game, "globalSettings", new GlobalSettingsPaths(directory));
        Set(game, "uiPreferences", new UiPreferences { WindowWidth = width, WindowHeight = height, FieldFps = 60, ShowFps = true, VSync = Environment.GetEnvironmentVariable("MEMORIA_FF8_FIELD_PERFORMANCE_VSYNC") == "1", TextScale = 125, UiFontSize = 24, DialogueFontFamily = "Arial", DialogueFontSize = 8 });
        if (speed == 3)
        {
            Get<GameplayClock>(game, "gameplayClock").ToggleSpeed();
        }
        game.Run();
        GameWorld world = Get<GameWorld>(game, "world");
        GameLoopSample? sample = Get<GameLoopMetrics>(game, "metrics").Latest;
        VisualFrameRate? rate = Get<VisualFrameRate?>(game, "visualFrameRate");
        rate?.ResolveCompletedPresentations();
        var presentation = game.GraphicsDevice.PresentationParameters;
        session.SetEventState(world.Events.Capture());
        session.Save(Path.Combine(output!, "host-after.json"));
        File.WriteAllText(Path.Combine(output!, "host-measurement.json"), JsonSerializer.Serialize(new { Field = initialField, Width = width, Height = height, FieldFps = 60, Speed = speed, Audio = audio, InitialFrame = initialFrame, FinalFrame = world.Events.Frame, Sample = sample, ActualWidth = presentation.BackBufferWidth, ActualHeight = presentation.BackBufferHeight, presentation.MultiSampleCount, game.GraphicsDevice.Adapter.Description, PresentationFps = rate?.PresentationsPerSecond, VisualChangesPerSecond = rate?.FramesPerSecond }, new JsonSerializerOptions { WriteIndented = true }));
        Assert.NotNull(sample);
        Assert.Equal(width, presentation.BackBufferWidth);
        Assert.Equal(height, presentation.BackBufferHeight);
        Assert.True(rate!.PresentationsPerSecond is > 0);
        Assert.InRange(Math.Abs(rate.PresentationsPerSecond.Value - sample.DrawsPerSecond), 0, 5);
        Assert.False(Get<bool>(game, "fieldFaulted"), Get<string?>(game, "error"));
        if (audio)
        {
            Assert.Null(Get<FieldAudioPresentation>(game, "fieldAudio").Error);
        }
        Assert.Equal(initialField, world.Field!.Id);
        Assert.True(world.Events.Frame > initialFrame + 100 * speed);
    }

    private static (int Width, int Height) PerformanceResolution()
    {
        string? resolution = Environment.GetEnvironmentVariable("MEMORIA_FF8_FIELD_PERFORMANCE_RESOLUTION");
        if (string.IsNullOrWhiteSpace(resolution))
        {
            return (960, 720);
        }
        string[] dimensions = resolution.Split('x', 'X');
        Assert.True(dimensions.Length == 2 && int.TryParse(dimensions[0], out int parsedWidth) && parsedWidth > 0 && int.TryParse(dimensions[1], out int parsedHeight) && parsedHeight > 0, "MEMORIA_FF8_FIELD_PERFORMANCE_RESOLUTION must be positive dimensions in WxH form.");
        return (int.Parse(dimensions[0]), int.Parse(dimensions[1]));
    }

    private static T Get<T>(RecreateGame game, string name)
    {
        return (T)typeof(RecreateGame).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(game)!;
    }

    private static void Set(RecreateGame game, string name, object value)
    {
        typeof(RecreateGame).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(game, value);
    }

    [Theory]
    [InlineData(132)]
    [InlineData(133)]
    [InlineData(134)]
    [InlineData(135)]
    [InlineData(264)]
    [InlineData(821)]
    public void Installed_background_submission_reports_draws_uploads_and_cpu_time(int fieldId)
    {
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_FIELD_PERFORMANCE_OUTPUT");
        Assert.SkipUnless(output is not null, "An optional diagnostic output directory is required.");
        Directory.CreateDirectory(output!);
        using GameAssets assets = new(TestResources.GameDirectory);
        FieldAssets field = assets.LoadField(fieldId);
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        FieldRenderer renderer = new(field, 960, 720) { TriangleRenderer = gpu };
        Dictionary<int, int> states = field.Background.Where(tile => tile.AnimationId != 255).GroupBy(tile => tile.AnimationId).ToDictionary(group => group.Key, group => (int)group.First().AnimationState);
        List<object> samples = [];
        for (int block = 0; block < 3; block++)
        {
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            long start = Stopwatch.GetTimestamp();
            int uploads = 0;
            int draws = 0;
            for (int frame = 0; frame < 30; frame++)
            {
                renderer.Render(states, [], TimeSpan.Zero, backgroundScrollOffsets: new Dictionary<int, System.Numerics.Vector2> { [1] = new(frame, 0) });
                uploads += gpu.FrameTextureUploads;
                draws += gpu.FrameDrawCalls;
                if (block > 0 || frame > 0)
                {
                    Assert.Equal(0, gpu.FrameTextureUploads);
                }
            }
            samples.Add(new { Block = block, RenderMilliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds / 30, AllocatedBytesPerFrame = (GC.GetAllocatedBytesForCurrentThread() - allocated) / 30, UploadsPerFrame = uploads / 30d, DrawsPerFrame = draws / 30d });
        }
        GpuTestDevice.Read(gpu.Target);
        File.WriteAllText(Path.Combine(output!, $"background-{fieldId}.json"), JsonSerializer.Serialize(new { Field = fieldId, Tiles = field.Background.Count, Samples = samples }, new JsonSerializerOptions { WriteIndented = true }));
    }

    [Fact]
    public void Named_standalone_profiles_scrolling_field_updates_and_gpu_submission()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_FIELD_PERFORMANCE_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_FIELD_PERFORMANCE_OUTPUT");
        Assert.SkipUnless(source is not null && File.Exists(source) && output is not null, "An optional field standalone and diagnostic output directory are required.");
        Directory.CreateDirectory(output!);
        GameSession session = new(GameSession.Load(source!));
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        world.SetRenderSize(960, 720);
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        long initialFrame = world.Events.Frame;
        List<object> samples = [];
        for (int block = 0; block < 4; block++)
        {
            double update = 0;
            double render = 0;
            int draws = 0;
            int uploads = 0;
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            for (int tick = 0; tick < 30; tick++)
            {
                long start = Stopwatch.GetTimestamp();
                world.Events.Step();
                update += Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                Assert.Null(world.Events.LastFault);
                start = Stopwatch.GetTimestamp();
                world.Render(TimeSpan.FromSeconds(1d / 30), triangleRenderer: gpu);
                render += Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                draws += gpu.FrameDrawCalls;
                uploads += gpu.FrameTextureUploads;
            }
            samples.Add(new { Block = block, UpdateMilliseconds = update / 30, RenderMilliseconds = render / 30, DrawsPerFrame = draws / 30d, UploadsPerFrame = uploads / 30d, AllocatedBytesPerFrame = (GC.GetAllocatedBytesForCurrentThread() - allocated) / 30 });
        }
        Microsoft.Xna.Framework.Color[] hardware = GpuTestDevice.Read(gpu.Target);
        string pixelHash = Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(hardware.AsSpan())));
        using (FileStream image = File.Create(Path.Combine(output!, "field.png")))
        {
            gpu.Target.SaveAsPng(image, 960, 720);
        }
        session.SetEventState(world.Events.Capture());
        session.Save(Path.Combine(output!, "after.json"));
        File.WriteAllText(Path.Combine(output!, "measurement.json"), JsonSerializer.Serialize(new { Field = world.Field!.Id, Tiles = world.Field.Background.Count, InitialFrame = initialFrame, FinalFrame = world.Events.Frame, PixelSha256 = pixelHash, Samples = samples }, new JsonSerializerOptions { WriteIndented = true }));
        using GameWorld restored = new(new GameSession(GameSession.Load(Path.Combine(output!, "after.json"))), TestResources.GameDirectory);
        restored.EnsureField();
        restored.SetRenderSize(960, 720);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        world.Events.Step();
        restored.Events.Step();
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
        Assert.Equal(initialFrame + 121, world.Events.Frame);
    }
}
