using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;
using Color = Microsoft.Xna.Framework.Color;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class WorldFramePerformanceTests
{
    private const int Width = 960;
    private const int Height = 720;
    private const int MaximumSweepFrames = 768;

    [Fact]
    public void Named_standalone_camera_sweep_compares_streamed_and_retained_gpu_terrain()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_WORLD_PERFORMANCE_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_WORLD_PERFORMANCE_OUTPUT");
        Assert.SkipUnless(source is not null && File.Exists(source) && output is not null, "An optional world standalone and diagnostic output directory are required.");
        Directory.CreateDirectory(output!);
        byte[] originalHash = SHA256.HashData(File.ReadAllBytes(source!));
        GameSession session = new(GameSession.Load(source!));
        Assert.Equal(SceneKind.WorldMap, session.State.Scene);
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        world.SetRenderSize(Width, Height);
        using GpuTestDevice device = new();
        using GpuTriangleRenderer baseline = new(device.Device);
        using GpuTriangleRenderer optimized = new(device.Device);
        ForwardingRenderer streamed = new(baseline);
        long initialFrame = session.State.World!.Frame;
        int initialYaw = session.State.World.CameraYaw;
        SweepResult warmup = Sweep(world, session, streamed, baseline, optimized, null);
        SweepResult measured = Sweep(world, session, streamed, baseline, optimized, output);
        Assert.Equal(originalHash, SHA256.HashData(File.ReadAllBytes(source!)));
        string finalSnapshot = Path.Combine(output!, "world-after.json");
        session.SetEventState(world.Events.Capture());
        session.Save(finalSnapshot, compact: true);
        GameState restored = GameSession.Load(finalSnapshot);
        Assert.Equal(session.State.World!.Frame, restored.World!.Frame);
        File.WriteAllText(Path.Combine(output!, "world-measurement.json"), JsonSerializer.Serialize(new
        {
            Adapter = device.Device.Adapter.Description,
            Width,
            Height,
            Timing = "CPU update and GPU submission; screenshot readback excluded",
            InitialFrame = initialFrame,
            InitialYaw = initialYaw,
            FinalFrame = session.State.World.Frame,
            FinalYaw = session.State.World.CameraYaw,
            Field = session.State.FieldId,
            Boundary = "World camera sweep complete; no translation input",
            FinalSnapshot = finalSnapshot,
            Warmup = warmup,
            Measured = measured
        }, new JsonSerializerOptions { WriteIndented = true }));
        Assert.Equal(8, measured.Pixels.Count);
        Assert.True(measured.Samples.All(sample => sample.Baseline.DrawCalls > 0 && sample.Optimized.DrawCalls > 0));
    }

    private static SweepResult Sweep(GameWorld world, GameSession session, ForwardingRenderer streamed, GpuTriangleRenderer baseline, GpuTriangleRenderer optimized, string? output)
    {
        List<FrameSample> samples = [];
        List<PixelComparison> pixels = [];
        int covered = 0;
        int previousYaw = session.State.World!.CameraYaw;
        int nextCapture = 0;
        for (int tick = 0; tick < MaximumSweepFrames && covered < 4096; tick++)
        {
            long updateStart = Stopwatch.GetTimestamp();
            PlayerActionResult result = world.Apply(new PlayerAction(PlayerActionKind.Wait, CameraHorizontal: 1));
            double updateMilliseconds = Stopwatch.GetElapsedTime(updateStart).TotalMilliseconds;
            Assert.True(result.Accepted, result.Detail);
            Assert.Equal(SceneKind.WorldMap, session.State.Scene);
            int yaw = session.State.World!.CameraYaw;
            int delta = (yaw - previousYaw + 2048) & 4095;
            delta -= 2048;
            Assert.InRange(delta, 0, 128);
            covered += delta;
            previousYaw = yaw;
            RenderSample streamedSample;
            RenderSample optimizedSample;
            // Alternate submission order so neither path always follows the other's work.
            if ((tick & 1) == 0)
            {
                streamedSample = Render(world, streamed, baseline);
                optimizedSample = Render(world, optimized, optimized);
            }
            else
            {
                optimizedSample = Render(world, optimized, optimized);
                streamedSample = Render(world, streamed, baseline);
            }
            samples.Add(new FrameSample(session.State.World.Frame, yaw, covered, updateMilliseconds, streamedSample, optimizedSample));
            if (output is not null && nextCapture < 8 && covered >= nextCapture * 512)
            {
                pixels.Add(CompareAndCapture(baseline, optimized, output, nextCapture, yaw));
                nextCapture++;
            }
        }
        Assert.True(covered >= 4096, $"Camera covered only {covered}/4096 units within {MaximumSweepFrames} callbacks.");
        return new SweepResult(covered, samples, pixels);
    }

    private static RenderSample Render(GameWorld world, ITriangleRenderer renderer, GpuTriangleRenderer gpu)
    {
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        long started = Stopwatch.GetTimestamp();
        byte[] software = world.Render(TimeSpan.Zero, triangleRenderer: renderer);
        double milliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        long bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Assert.Empty(software);
        return new RenderSample(milliseconds, bytes, gpu.FrameDrawCalls, gpu.FrameTextureUploads, gpu.FrameTerrainUploads);
    }

    private static PixelComparison CompareAndCapture(GpuTriangleRenderer baseline, GpuTriangleRenderer optimized, string output, int index, int yaw)
    {
        Color[] expected = GpuTestDevice.Read(baseline.Target);
        Color[] actual = GpuTestDevice.Read(optimized.Target);
        int matching = 0;
        int exact = 0;
        int maximumDifference = 0;
        for (int pixel = 0; pixel < actual.Length; pixel++)
        {
            Assert.Equal(255, actual[pixel].A);
            int difference = Math.Max(Math.Abs(expected[pixel].R - actual[pixel].R), Math.Max(Math.Abs(expected[pixel].G - actual[pixel].G), Math.Abs(expected[pixel].B - actual[pixel].B)));
            maximumDifference = Math.Max(maximumDifference, difference);
            if (difference == 0)
            {
                exact++;
            }
            if (difference <= 6)
            {
                matching++;
            }
        }
        using (FileStream image = File.Create(Path.Combine(output, $"world-{index:D2}-streamed.png")))
        {
            baseline.Target.SaveAsPng(image, Width, Height);
        }
        using (FileStream image = File.Create(Path.Combine(output, $"world-{index:D2}-retained.png")))
        {
            optimized.Target.SaveAsPng(image, Width, Height);
        }
        PixelComparison comparison = new(index, yaw, actual.Length, matching, exact, maximumDifference);
        File.WriteAllText(Path.Combine(output, $"world-{index:D2}-comparison.json"), JsonSerializer.Serialize(comparison));
        Assert.True(matching >= actual.Length * 0.95, $"Heading {yaw}: only {matching}/{actual.Length} RGB pixels match within six channel levels.");
        return comparison;
    }

    private sealed record RenderSample(double CpuMilliseconds, long AllocatedBytes, int DrawCalls, int TextureUploads, int TerrainUploads);
    private sealed record FrameSample(long Frame, int Yaw, int CoveredYaw, double UpdateMilliseconds, RenderSample Baseline, RenderSample Optimized);
    private sealed record PixelComparison(int Index, int Yaw, int Pixels, int MatchingWithinSix, int ExactMatches, int MaximumChannelDifference);
    private sealed record SweepResult(int CoveredYaw, List<FrameSample> Samples, List<PixelComparison> Pixels);

    // This deliberately exposes only the original primitive stream capability.
    private sealed class ForwardingRenderer(GpuTriangleRenderer gpu) : ITriangleRenderer
    {
        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
            gpu.Begin(width, height, clearColor, clearDepth);
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            gpu.Draw(a, b, c, material);
        }

        public void End()
        {
            gpu.End();
        }
    }
}
