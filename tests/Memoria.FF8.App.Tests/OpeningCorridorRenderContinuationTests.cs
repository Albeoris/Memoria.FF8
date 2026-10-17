using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;
using Color = Microsoft.Xna.Framework.Color;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class OpeningCorridorRenderContinuationTests
{
    [Fact]
    public void Standalone_corridor_move_renders_and_restores_its_next_ordinary_move()
    {
        string source = TestResources.FixturePath("opening-corridor-render-source");
        string directory = TestResources.GameDirectory;
        byte[] sourceHash = SHA256.HashData(File.ReadAllBytes(source));
        GameSession session = new(GameSession.Load(source));
        using GameWorld world = new(session, directory);
        world.EnsureField();
        AssertCorridor(world);
        Assert.True(world.Events.IsUserControlEnabled);
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        string outputRoot = Environment.GetEnvironmentVariable("MEMORIA_FF8_CORRIDOR_RENDER_OUTPUT") ?? TestResources.ArtifactDirectory;
        string output = Path.Combine(outputRoot, "corridor-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        world.SetRenderSize(640, 480);
        Save(session, world, Path.Combine(output, "before-move.json"));
        Render(world, gpu, output, "before-move");
        long beforeFrame = world.Events.Frame;
        var beforePosition = world.Events.Scene.NativePlayerPosition;
        PlayerAction move = new(PlayerActionKind.Move, Horizontal: -1);
        Assert.True(world.Apply(move).Accepted);
        world.EnsureField();
        AssertCorridor(world);
        Assert.True(world.Events.Frame > beforeFrame);
        Assert.NotEqual(beforePosition, world.Events.Scene.NativePlayerPosition);
        string afterMove = Path.Combine(output, "after-move.json");
        Save(session, world, afterMove);
        Render(world, gpu, output, "after-move");
        GameSession restoredSession = new(GameSession.Load(afterMove));
        using GameWorld restored = new(restoredSession, directory);
        restored.EnsureField();
        restored.SetRenderSize(640, 480);
        AssertCorridor(restored);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Color[] expected = Render(world, gpu, output, "original-restored-boundary");
        Assert.Equal(expected, Render(restored, gpu, output, "restored-boundary"));
        Assert.True(world.Apply(move).Accepted);
        Assert.True(restored.Apply(move).Accepted);
        world.EnsureField();
        restored.EnsureField();
        AssertCorridor(world);
        AssertCorridor(restored);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restoredSession.State)));
        Assert.Equal(Render(world, gpu, output, "next-move"), Render(restored, gpu, output, "restored-next-move"));
        Save(session, world, Path.Combine(output, "next-move.json"));
        Save(restoredSession, restored, Path.Combine(output, "restored-next-move.json"));
        Assert.Equal(sourceHash, SHA256.HashData(File.ReadAllBytes(source)));
        File.WriteAllText(Path.Combine(output, "continuation.json"), JsonSerializer.Serialize(new
        {
            Field = world.Events.FieldId,
            BeforeFrame = beforeFrame,
            FinalFrame = world.Events.Frame,
            Move = move,
            IndependentStateEqual = true,
            IndependentGpuPixelsEqual = true,
            SourcePreserved = true,
            Boundary = "Two ordinary one-frame MOVE actions from an externally supplied standalone field229 save.",
            Snapshot = "next-move.json",
            RestoredSnapshot = "restored-next-move.json",
            Width = world.RenderWidth,
            Height = world.RenderHeight
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void AssertCorridor(GameWorld world)
    {
        Assert.Equal(229, world.Field!.Id);
        Assert.Equal(229, world.Events.FieldId);
        Assert.Null(world.Events.LastFault);
    }

    private static void Save(GameSession session, GameWorld world, string path)
    {
        session.SetEventState(world.Events.Capture());
        session.Save(path);
    }

    private static Color[] Render(GameWorld world, GpuTriangleRenderer gpu, string output, string name)
    {
        byte[] software = world.Render(TimeSpan.Zero);
        world.Render(TimeSpan.Zero, triangleRenderer: gpu);
        Color[] hardware = GpuTestDevice.Read(gpu.Target);
        long totalError = 0;
        int maximumError = 0;
        int differingPixels = 0;
        for (int index = 0; index < hardware.Length; index++)
        {
            int difference = Math.Max(Math.Abs(hardware[index].R - software[index * 4]), Math.Max(Math.Abs(hardware[index].G - software[index * 4 + 1]), Math.Abs(hardware[index].B - software[index * 4 + 2])));
            totalError += difference;
            maximumError = Math.Max(maximumError, difference);
            if (difference != 0)
            {
                differingPixels++;
            }
        }
        using (FileStream stream = new(Path.Combine(output, name + ".png"), FileMode.CreateNew))
        {
            gpu.Target.SaveAsPng(stream, world.RenderWidth, world.RenderHeight);
        }
        File.WriteAllText(Path.Combine(output, name + "-render.json"), JsonSerializer.Serialize(new
        {
            Field = world.Events.FieldId,
            world.Events.Frame,
            Width = world.RenderWidth,
            Height = world.RenderHeight,
            MeanMaximumChannelError = totalError / (double)hardware.Length,
            MaximumChannelError = maximumError,
            DifferingPixels = differingPixels,
            Pixels = hardware.Length,
            gpu.FrameDrawCalls,
            gpu.FrameTextureUploads
        }, new JsonSerializerOptions { WriteIndented = true }));
        return hardware;
    }
}
