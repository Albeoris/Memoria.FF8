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
    public void Named_standalone_runs_inside_the_complete_field_graphics_host()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_FIELD_PERFORMANCE_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_FIELD_PERFORMANCE_OUTPUT");
        Assert.SkipUnless(Environment.GetEnvironmentVariable("MEMORIA_FF8_FIELD_HOST_PERFORMANCE") == "1" && source is not null && File.Exists(source) && output is not null, "An optional standalone, output directory and interactive graphics diagnostic opt-in are required.");
        Directory.CreateDirectory(output!);
        GameSession session = new(GameSession.Load(source!));
        GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        long initialFrame = world.Events.Frame;
        int initialField = world.Field!.Id;
        string directory = TestResources.ArtifactFile("field-host");
        Directory.CreateDirectory(directory);
        LaunchOptions options = new() { GameDirectory = TestResources.GameDirectory, InitialState = session.State, StatePath = source, SavePath = Path.Combine(directory, "unused.json"), MuteAudio = true, ExitAfterSeconds = 8, ResolutionSpecified = true, Width = 960, Height = 720 };
        using RecreateGame game = new(options, session);
        Set(game, "world", world);
        Set(game, "globalSettings", new GlobalSettingsPaths(directory));
        Set(game, "uiPreferences", new UiPreferences { WindowWidth = 960, WindowHeight = 720, VSync = false });
        game.Run();
        GameLoopSample? sample = Get<GameLoopMetrics>(game, "metrics").Latest;
        session.SetEventState(world.Events.Capture());
        session.Save(Path.Combine(output!, "host-after.json"));
        File.WriteAllText(Path.Combine(output!, "host-measurement.json"), JsonSerializer.Serialize(new { InitialFrame = initialFrame, FinalFrame = world.Events.Frame, Sample = sample }, new JsonSerializerOptions { WriteIndented = true }));
        Assert.NotNull(sample);
        Assert.False(Get<bool>(game, "fieldFaulted"), Get<string?>(game, "error"));
        Assert.Equal(initialField, world.Field!.Id);
        Assert.True(world.Events.Frame > initialFrame + 100);
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
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            for (int tick = 0; tick < 30; tick++)
            {
                long start = Stopwatch.GetTimestamp();
                Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
                update += Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                Assert.Null(world.Events.LastFault);
                start = Stopwatch.GetTimestamp();
                world.Render(TimeSpan.FromSeconds(1d / 30), triangleRenderer: gpu);
                render += Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            }
            samples.Add(new { Block = block, UpdateMilliseconds = update / 30, RenderMilliseconds = render / 30, AllocatedBytesPerFrame = (GC.GetAllocatedBytesForCurrentThread() - allocated) / 30 });
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
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.True(restored.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
        Assert.Equal(initialFrame + 121, world.Events.Frame);
    }
}
