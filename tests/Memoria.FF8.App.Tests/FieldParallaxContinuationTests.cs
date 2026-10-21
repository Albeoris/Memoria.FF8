using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;
using Color = Microsoft.Xna.Framework.Color;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class FieldParallaxContinuationTests
{
    [Fact]
    public void StandaloneParallaxKeepsItsEffectiveViewportAndRestoresTheNextMove()
    {
        string source = TestResources.FixturePath("field-parallax-source");
        Assert.SkipUnless(File.Exists(source), "An optional standalone parallax fixture is required.");
        byte[] sourceHash = SHA256.HashData(File.ReadAllBytes(source));
        GameSession session = new(GameSession.Load(source));
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        AssertBoundary(session, world);
        Assert.Empty(world.Events.Scene.BackgroundScrollPositions);
        Assert.Contains((short)64, world.Events.Scene.BackgroundScrollLayers.Values.Select(settings => settings.ScaleX));
        Assert.Contains((short)48, world.Events.Scene.BackgroundScrollLayers.Values.Select(settings => settings.ScaleX));
        Assert.Contains((short)32, world.Events.Scene.BackgroundScrollLayers.Values.Select(settings => settings.ScaleX));
        string outputRoot = Environment.GetEnvironmentVariable("MEMORIA_FF8_PARALLAX_OUTPUT") ?? TestResources.ArtifactDirectory;
        string output = Path.Combine(outputRoot, "field-parallax-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        Save(session, world, Path.Combine(output, "before-move.json"));
        long beforeFrame = world.Events.Frame;
        FieldFixedPosition? beforePosition = world.Events.Scene.NativePlayerPosition;
        List<object> renders = [];
        RenderResult before = Render(session, world, gpu, output, "before-move");
        renders.Add(before.Summary);
        PlayerAction move = new(PlayerActionKind.Move, Horizontal: 1, Frames: 1);
        for (int index = 0; index < 12; index++)
        {
            Assert.True(world.Apply(move).Accepted);
            world.EnsureField();
            AssertBoundary(session, world);
        }
        Assert.Equal(beforeFrame + 12, world.Events.Frame);
        Assert.NotEqual(beforePosition, world.Events.Scene.NativePlayerPosition);
        string moved = Path.Combine(output, "after-move.json");
        Save(session, world, moved);
        RenderResult after = Render(session, world, gpu, output, "after-move");
        renders.Add(after.Summary);
        Assert.NotEqual(before.Center, after.Center);
        Assert.Equal(before.BackgroundPixels, after.BackgroundPixels);
        Assert.True(before.OldDifferingPixels > 0 || after.OldDifferingPixels > 0);
        GameSession restoredSession = new(GameSession.Load(moved));
        using GameWorld restored = new(restoredSession, TestResources.GameDirectory);
        restored.EnsureField();
        AssertBoundary(restoredSession, restored);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        RenderResult cold = Render(restoredSession, restored, gpu, output, "restored-boundary");
        renders.Add(cold.Summary);
        Assert.Equal(after.WorldPixels, cold.WorldPixels);
        Assert.Equal(after.BackgroundPixels, cold.BackgroundPixels);
        Assert.True(world.Apply(move).Accepted);
        Assert.True(restored.Apply(move).Accepted);
        world.EnsureField();
        restored.EnsureField();
        AssertBoundary(session, world);
        AssertBoundary(restoredSession, restored);
        Assert.Equal(beforeFrame + 13, world.Events.Frame);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restoredSession.State)));
        RenderResult next = Render(session, world, gpu, output, "next-move");
        RenderResult restoredNext = Render(restoredSession, restored, gpu, output, "restored-next-move");
        renders.Add(next.Summary);
        renders.Add(restoredNext.Summary);
        Assert.Equal(next.WorldPixels, restoredNext.WorldPixels);
        Assert.Equal(next.BackgroundPixels, restoredNext.BackgroundPixels);
        Save(session, world, Path.Combine(output, "next-move.json"));
        Save(restoredSession, restored, Path.Combine(output, "restored-next-move.json"));
        Assert.Equal(sourceHash, SHA256.HashData(File.ReadAllBytes(source)));
        File.WriteAllText(Path.Combine(output, "continuation.json"), JsonSerializer.Serialize(new
        {
            SourceFixture = "field-parallax-source",
            SourceSha256 = Convert.ToHexString(sourceHash),
            Field = world.Field!.Id,
            BeforeFrame = beforeFrame,
            FinalFrame = world.Events.Frame,
            Move = move,
            MoveCallbacksBeforeRestore = 12,
            EffectiveCenter = new { X = 0, Y = 0 },
            FixedBackgroundEqualsIndependentZeroCenterReference = true,
            BackgroundPixelsStableAcrossMovement = true,
            OldBehaviorDiffers = true,
            IndependentStateEqual = true,
            IndependentGpuPixelsEqual = true,
            RenderingPreservedState = true,
            SourcePreserved = true,
            Snapshot = "next-move.json",
            RestoredSnapshot = "restored-next-move.json",
            Boundary = "Thirteen ordinary one-frame MOVE callbacks within the source field, with cold restore after callback twelve.",
            Renderings = renders
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static RenderResult Render(GameSession session, GameWorld world, GpuTriangleRenderer gpu, string output, string name)
    {
        JsonElement beforeEvents = world.Events.Capture();
        JsonElement beforeSession = JsonSerializer.SerializeToElement(session.State);
        world.SetRenderSize(640, 240);
        world.Render(TimeSpan.Zero, triangleRenderer: gpu);
        Color[] worldPixels = GpuTestDevice.Read(gpu.Target);
        SavePng(gpu, output, name + "-world", 640);
        if (name is "before-move" or "after-move")
        {
            world.SetRenderSize(2048, 796);
            world.Render(TimeSpan.Zero, triangleRenderer: gpu);
            SavePng(gpu, output, name + "-world-wide", 2048, 796);
        }
        FieldAssets field = world.Field!;
        int camera = world.Events.Scene.CameraIndex;
        BackgroundScrollFrame frame = NormalCameraCertificate.Prepare(field, world.Events.Scene, world.Events.Actors)!;
        Assert.Null(frame.NativeWrap);
        Assert.Equal(0, frame.ActorOffset.Y);
        FieldBounds bounds = field.Information!.CameraBounds[camera];
        Assert.Equal(-bounds.Left, bounds.Right);
        Assert.InRange(bounds.Right - bounds.Left, 320, 640);
        Dictionary<int, short> scales = field.Background.Select(tile => tile.Layer).Distinct().ToDictionary(layer => layer, layer => layer > camera ? Settings(layer).ScaleX : (short)256);
        Dictionary<int, Vector2> referenceOffsets = field.Background.Select(tile => tile.Layer).Distinct().ToDictionary(layer => layer, layer => layer > camera ? new Vector2(Settings(layer).OffsetX, Settings(layer).OffsetY) + field.GetCamera(camera).Pan : Vector2.Zero);
        Color[] fixedPixels = Background(640, frame.LayerOffsets, frame.ActorOffset, scales, name + "-fixed-background");
        Color[] oldPixels = Background(640, frame.LayerOffsets, frame.ActorOffset, null, name + "-old-background");
        Color[] reference = Background(640, referenceOffsets, Vector2.Zero, scales, name + "-reference-background");
        Assert.Equal(reference, fixedPixels);
        Color[] nativeReference = Background(320, referenceOffsets, Vector2.Zero, scales, name + "-native-reference-background");
        for (int y = 0; y < 240; y++)
        {
            for (int x = 0; x < 320; x++)
            {
                Assert.Equal(nativeReference[y * 320 + x], reference[y * 640 + x + 160]);
            }
        }
        int oldDifferingPixels = oldPixels.Zip(reference).Count(pair => pair.First != pair.Second);
        Assert.True(JsonElement.DeepEquals(beforeEvents, world.Events.Capture()));
        Assert.True(JsonElement.DeepEquals(beforeSession, JsonSerializer.SerializeToElement(session.State)));
        Vector2 center = -frame.ActorOffset;
        return new RenderResult(worldPixels, fixedPixels, center, oldDifferingPixels, new
        {
            Name = name,
            world.Events.Frame,
            Center = new { center.X, center.Y },
            CameraBounds = bounds,
            ScaleX = scales,
            LayerSettings = world.Events.Scene.BackgroundScrollLayers,
            NativeOffsets = frame.LayerOffsets.ToDictionary(pair => pair.Key, pair => new { pair.Value.X, pair.Value.Y }),
            ReferenceOffsets = referenceOffsets.ToDictionary(pair => pair.Key, pair => new { pair.Value.X, pair.Value.Y }),
            FixedReferenceDifferingPixels = 0,
            OldReferenceDifferingPixels = oldDifferingPixels,
            NativeReferenceMatchesWideCenter = true,
            RenderingPreservedState = true
        });

        BackgroundScrollSettings Settings(int layer)
        {
            return world.Events.Scene.BackgroundScrollLayers.GetValueOrDefault(layer) ?? new BackgroundScrollSettings(0, 0, 256, 256);
        }

        Color[] Background(int width, IReadOnlyDictionary<int, Vector2> offsets, Vector2 actorOffset, IReadOnlyDictionary<int, short>? layerScales, string imageName)
        {
            FieldRenderer renderer = new(field, width, 240) { TriangleRenderer = gpu };
            renderer.Render(world.Events.BackgroundStates, [], TimeSpan.Zero, cameraIndex: camera, backgroundModuleShades: world.Events.GetBackgroundModuleShades(), backgroundScrollOffsets: offsets, actorScrollOffset: actorOffset, backgroundLayerScales: layerScales);
            Color[] pixels = GpuTestDevice.Read(gpu.Target);
            SavePng(gpu, output, imageName, width);
            return pixels;
        }
    }

    private static void SavePng(GpuTriangleRenderer gpu, string output, string name, int width, int height = 240)
    {
        using FileStream stream = new(Path.Combine(output, name + ".png"), FileMode.CreateNew);
        gpu.Target.SaveAsPng(stream, width, height);
    }

    private static void Save(GameSession session, GameWorld world, string path)
    {
        session.SetEventState(world.Events.Capture());
        session.Save(path, compact: true);
    }

    private static void AssertBoundary(GameSession session, GameWorld world)
    {
        Assert.Equal(SceneKind.Field, session.State.Scene);
        Assert.Equal(230, world.Field!.Id);
        Assert.Equal(230, world.Events.FieldId);
        Assert.Equal(0, world.Field.Information!.BackgroundWrapMask);
        Assert.Null(world.Events.LastFault);
        Assert.True(world.Events.IsUserControlEnabled);
    }

    private sealed record RenderResult(Color[] WorldPixels, Color[] BackgroundPixels, Vector2 Center, int OldDifferingPixels, object Summary);
}
