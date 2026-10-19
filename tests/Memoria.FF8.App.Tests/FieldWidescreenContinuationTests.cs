using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;
using Color = Microsoft.Xna.Framework.Color;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class FieldWidescreenContinuationTests
{
    [Fact]
    public void StandaloneFieldPreservesNativeCenterAndRestoresItsNextOrdinaryMove()
    {
        string source = TestResources.FixturePath("field-widescreen-source");
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(File.Exists(source), "An optional standalone field fixture is required.");
        byte[] sourceHash = SHA256.HashData(File.ReadAllBytes(source));
        GameSession session = new(GameSession.Load(source));
        using GameWorld world = new(session, directory);
        world.EnsureField();
        Assert.Equal(SceneKind.Field, session.State.Scene);
        Assert.NotNull(world.Field);
        Assert.Null(world.Events.LastFault);
        Assert.True(world.Events.IsUserControlEnabled);
        int fieldId = world.Events.FieldId;
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        string outputRoot = Environment.GetEnvironmentVariable("MEMORIA_FF8_WIDESCREEN_OUTPUT") ?? TestResources.ArtifactDirectory;
        string output = Path.Combine(outputRoot, "field-widescreen-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        List<object> renders = [];
        Save(session, world, Path.Combine(output, "before-move.json"));
        RenderResult before = Render(session, world, gpu, output, "before-move");
        renders.Add(before.Summary);
        long beforeFrame = world.Events.Frame;
        var beforePosition = world.Events.Scene.NativePlayerPosition;
        PlayerAction move = new(PlayerActionKind.Move, Horizontal: 1, Frames: 1);
        Assert.True(world.Apply(move).Accepted);
        world.EnsureField();
        AssertBoundary(session, world, fieldId);
        Assert.Equal(beforeFrame + 1, world.Events.Frame);
        Assert.NotEqual(beforePosition, world.Events.Scene.NativePlayerPosition);
        long afterFrame = world.Events.Frame;
        string afterMove = Path.Combine(output, "after-move.json");
        Save(session, world, afterMove);
        RenderResult after = Render(session, world, gpu, output, "after-move");
        renders.Add(after.Summary);
        GameSession restoredSession = new(GameSession.Load(afterMove));
        using GameWorld restored = new(restoredSession, directory);
        restored.EnsureField();
        AssertBoundary(restoredSession, restored, fieldId);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        RenderResult cold = Render(restoredSession, restored, gpu, output, "restored-boundary");
        renders.Add(cold.Summary);
        Assert.Equal(after.WidePixels, cold.WidePixels);
        Assert.True(world.Apply(move).Accepted);
        Assert.True(restored.Apply(move).Accepted);
        world.EnsureField();
        restored.EnsureField();
        AssertBoundary(session, world, fieldId);
        AssertBoundary(restoredSession, restored, fieldId);
        Assert.Equal(afterFrame + 1, world.Events.Frame);
        Assert.Equal(world.Events.Frame, restored.Events.Frame);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restoredSession.State)));
        RenderResult continued = Render(session, world, gpu, output, "next-move");
        RenderResult restoredContinued = Render(restoredSession, restored, gpu, output, "restored-next-move");
        renders.Add(continued.Summary);
        renders.Add(restoredContinued.Summary);
        Assert.Equal(continued.WidePixels, restoredContinued.WidePixels);
        Save(session, world, Path.Combine(output, "next-move.json"));
        Save(restoredSession, restored, Path.Combine(output, "restored-next-move.json"));
        Assert.Equal(sourceHash, SHA256.HashData(File.ReadAllBytes(source)));
        File.WriteAllText(Path.Combine(output, "continuation.json"), JsonSerializer.Serialize(new
        {
            SourceFixture = "field-widescreen-source",
            SourceSha256 = Convert.ToHexString(sourceHash),
            Field = fieldId,
            BeforeFrame = beforeFrame,
            AfterFrame = afterFrame,
            FinalFrame = world.Events.Frame,
            Move = move,
            NativeCenterGpuMaximumChannelErrorAtMostOne = true,
            RenderingPreservedState = true,
            IndependentStateEqual = true,
            IndependentGpuPixelsEqual = true,
            SourcePreserved = true,
            Boundary = "Two ordinary one-frame MOVE actions from an externally supplied standalone field save.",
            Snapshot = "next-move.json",
            RestoredSnapshot = "restored-next-move.json",
            Renderings = renders
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void AssertBoundary(GameSession session, GameWorld world, int fieldId)
    {
        Assert.Equal(SceneKind.Field, session.State.Scene);
        Assert.Equal(fieldId, world.Field!.Id);
        Assert.Equal(fieldId, world.Events.FieldId);
        Assert.Null(world.Events.LastFault);
    }

    private static void Save(GameSession session, GameWorld world, string path)
    {
        session.SetEventState(world.Events.Capture());
        session.Save(path);
    }

    private static RenderResult Render(GameSession session, GameWorld world, GpuTriangleRenderer gpu, string output, string name)
    {
        JsonElement beforeEvents = world.Events.Capture();
        JsonElement beforeSession = JsonSerializer.SerializeToElement(session.State);
        world.SetRenderSize(640, 240);
        byte[] wideSoftware = world.Render(TimeSpan.Zero).ToArray();
        world.Render(TimeSpan.Zero, triangleRenderer: gpu);
        Color[] wideHardware = GpuTestDevice.Read(gpu.Target);
        SavePng(gpu, output, name + "-wide", 640, 240);
        world.SetRenderSize(320, 240);
        byte[] nativeSoftware = world.Render(TimeSpan.Zero).ToArray();
        world.Render(TimeSpan.Zero, triangleRenderer: gpu);
        Color[] nativeHardware = GpuTestDevice.Read(gpu.Target);
        SavePng(gpu, output, name + "-native", 320, 240);
        Assert.Equal(640 * 240, wideHardware.Length);
        Assert.Equal(320 * 240, nativeHardware.Length);
        int centerMaximumError = 0;
        int centerDifferingPixels = 0;
        for (int y = 0; y < 240; y++)
        {
            for (int x = 0; x < 320; x++)
            {
                Color native = nativeHardware[y * 320 + x];
                Color wide = wideHardware[y * 640 + x + 160];
                int error = Math.Max(Math.Abs(native.R - wide.R), Math.Max(Math.Abs(native.G - wide.G), Math.Abs(native.B - wide.B)));
                centerMaximumError = Math.Max(centerMaximumError, error);
                if (error != 0)
                {
                    centerDifferingPixels++;
                }
            }
        }
        // Translating projected model vertices into a different target can
        // change interpolated texture rounding by one channel unit.
        Assert.InRange(centerMaximumError, 0, 1);
        world.SetRenderSize(640, 240);
        Assert.True(JsonElement.DeepEquals(beforeEvents, world.Events.Capture()), $"Rendering changed field state for {name}.");
        Assert.True(JsonElement.DeepEquals(beforeSession, JsonSerializer.SerializeToElement(session.State)), $"Rendering changed session state for {name}.");
        int cameraIndex = world.Events.Scene.CameraIndex;
        FieldBounds? bounds = world.Field!.Information is { } information && (uint)cameraIndex < information.CameraBounds.Count ? information.CameraBounds[cameraIndex] : null;
        return new RenderResult(wideHardware, new
        {
            Name = name,
            Field = world.Events.FieldId,
            world.Events.Frame,
            CameraIndex = cameraIndex,
            SelectedCameraBounds = bounds,
            WideTarget = new PixelBounds(0, 0, 640, 240),
            NativeTarget = new PixelBounds(0, 0, 320, 240),
            NativeCenterInWide = new PixelBounds(160, 0, 320, 240),
            WideVisiblePixelBounds = VisibleBounds(wideHardware, 640, 240),
            NativeVisiblePixelBounds = VisibleBounds(nativeHardware, 320, 240),
            WideCpuGpu = Compare(wideSoftware, wideHardware),
            NativeCpuGpu = Compare(nativeSoftware, nativeHardware),
            NativeCenterGpuMaximumChannelError = centerMaximumError,
            NativeCenterGpuDifferingPixels = centerDifferingPixels,
            RenderingPreservedState = true
        });
    }

    private static void SavePng(GpuTriangleRenderer gpu, string output, string name, int width, int height)
    {
        using FileStream stream = new(Path.Combine(output, name + ".png"), FileMode.CreateNew);
        gpu.Target.SaveAsPng(stream, width, height);
    }

    private static PixelBounds VisibleBounds(Color[] pixels, int width, int height)
    {
        int left = width;
        int top = height;
        int right = 0;
        int bottom = 0;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color pixel = pixels[y * width + x];
                if (pixel.R == 0 && pixel.G == 0 && pixel.B == 0)
                {
                    continue;
                }
                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x + 1);
                bottom = Math.Max(bottom, y + 1);
            }
        }
        return right > left && bottom > top ? new PixelBounds(left, top, right - left, bottom - top) : new PixelBounds(0, 0, 0, 0);
    }

    private static RenderComparison Compare(byte[] software, Color[] hardware)
    {
        Assert.Equal(hardware.Length * 4, software.Length);
        long totalError = 0;
        int maximumError = 0;
        int differingPixels = 0;
        for (int index = 0; index < hardware.Length; index++)
        {
            Color pixel = hardware[index];
            int offset = index * 4;
            int difference = Math.Max(Math.Abs(pixel.R - software[offset]), Math.Max(Math.Abs(pixel.G - software[offset + 1]), Math.Abs(pixel.B - software[offset + 2])));
            totalError += difference;
            maximumError = Math.Max(maximumError, difference);
            if (difference != 0)
            {
                differingPixels++;
            }
        }
        return new RenderComparison(differingPixels, maximumError, totalError / (double)hardware.Length);
    }

    private sealed record RenderResult(Color[] WidePixels, object Summary);
    private sealed record RenderComparison(int DifferingPixels, int MaximumChannelError, double MeanMaximumChannelError);
}
