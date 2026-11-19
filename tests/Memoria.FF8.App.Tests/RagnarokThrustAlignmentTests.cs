using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;
using SkiaSharp;

namespace Memoria.FF8.App.Tests;

public sealed class RagnarokThrustAlignmentTests
{
    [Fact]
    public void Saved_world_publishes_native_ship_root_angles_and_visibility_without_render_advancement()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_THRUST_SAVE");
        Assert.SkipUnless(File.Exists(source), "Set MEMORIA_FF8_THRUST_SAVE to an optional world snapshot.");
        byte[] sourceHash = SHA256.HashData(File.ReadAllBytes(source!));
        string directory = TestResources.GameDirectory;
        using WorldAssets assets = new(directory);
        GameSession session = new(PlayerStateLoader.Load(source!, directory));
        using WorldMapScene scene = new(session, directory);
        long loadedFrame = scene.State.Frame;
        Assert.True(scene.Apply(new(PlayerActionKind.Wait, Frames: 1)).Accepted);
        Assert.Equal(loadedFrame + 1, scene.State.Frame);
        if (scene.State.NativeFootRuntime is null)
        {
            Assert.True(scene.Apply(new(PlayerActionKind.Wait, Frames: 23)).Accepted);
            Assert.True(scene.Apply(new(PlayerActionKind.WorldButtons, 0x80 | 0x2000 | 0x4000, Frames: 3)).Accepted);
            WorldState preceding = scene.State;
            Assert.True(scene.Apply(new(PlayerActionKind.WorldButtons, 0x80 | 0x2000 | 0x4000, Frames: 1)).Accepted);
            AssertCallbackPhase(preceding, scene.State, assets);
            NativeRagnarokWorldObject turning = scene.State.NativeRagnarokRuntime!.Objects[scene.State.NativeRagnarokRuntime.Allocation.RagnarokIndex];
            Assert.NotEqual((short)0, turning.Roll);
        }
        string before = JsonSerializer.Serialize(session.State);
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_THRUST_OUTPUT");
        if (!string.IsNullOrEmpty(output))
        {
            Directory.CreateDirectory(output);
        }
        GameSession restoredSession = new(JsonSerializer.Deserialize<GameState>(before)!);
        using WorldMapScene restoredScene = new(restoredSession, directory);
        foreach ((int width, int height) in new[] { (1018, 661), (960, 672) })
        {
            byte[] rgba = scene.Render(width, height);
            Assert.Equal(rgba, restoredScene.Render(width, height));
            if (!string.IsNullOrEmpty(output))
            {
                using SKBitmap bitmap = new(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
                Marshal.Copy(rgba, 0, bitmap.GetPixels(), rgba.Length);
                using SKImage image = SKImage.FromBitmap(bitmap);
                using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
                using FileStream stream = File.Create(Path.Combine(output, $"flight-{width}x{height}.png"));
                data.SaveTo(stream);
            }
        }
        NativeRagnarokWorldRuntimeState runtime = scene.State.NativeRagnarokRuntime!;
        NativeRagnarokWorldObject body = scene.State.NativeFootRuntime is null ? runtime.DrawBody! : runtime.Objects[runtime.Allocation.RagnarokIndex];
        WorldPresentation history = (WorldPresentation)typeof(WorldMapScene).GetField("presentation", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(scene)!;
        WorldRenderFrame frame = history.Current!;
        NativeWorldPathProjectionCertificate projection = NativeWorldPathProjectionCertificate.CreateRagnarok(runtime.ProjectionCamera!, runtime.ProjectionCurveThreshold!.Value);
        bool visible = projection.IsNear(body.Position) && projection.ProjectNode(body.Position)!.Visible;
        NativeWorldAnimatedModelDraw[] ships = frame.AnimatedModels!.Where(draw => draw.ObjectId == new WorldRenderObjectId(WorldRenderObjectKind.Allocation, runtime.Allocation.RagnarokIndex)).ToArray();
        Assert.Equal(visible ? 1 : 0, ships.Length);
        Assert.Equal(scene.State.NativeFootRuntime is not null && (scene.State.GroundType is null or > 5), frame.AvatarVisible);
        if (visible)
        {
            NativeWorldAnimatedModelDraw ship = Assert.Single(ships);
            Assert.Equal(projection.CanonicalModelAnchor(projection.ProjectNode(body.Position)!) + new Vector3(0, 146, 0), ship.Position);
            Assert.Equal(body.Pitch * MathF.Tau / 4096, ship.NativePitch);
            Assert.Equal(body.Yaw * MathF.Tau / 4096, ship.Heading);
            Assert.Equal(body.Roll * MathF.Tau / 4096, ship.NativeRoll);
        }
        Assert.Equal(before, JsonSerializer.Serialize(session.State));
        Assert.Equal(sourceHash, SHA256.HashData(File.ReadAllBytes(source!)));
        if (!string.IsNullOrEmpty(output))
        {
            File.WriteAllText(Path.Combine(output, "summary.json"), JsonSerializer.Serialize(new { LoadedFrame = loadedFrame, scene.State.Frame, Body = body, runtime.Motor, runtime.ProjectionCamera, scene.State.CameraTargetX, scene.State.CameraTargetZ, scene.State.CameraTargetNativeHeight, runtime.PreviousParticleNativeHeight, runtime.Forest, assets.ForestTables.RagnarokThrustOffsets, Effects = assets.ForestTables.Effects.Where(effect => effect.Type is 12 or 13), Presentation = new { frame.Position, frame.Heading, frame.AvatarVisible, Ships = ships.Select(ship => new { ship.Position, ship.NativePitch, ship.Heading, ship.NativeRoll }) } }, new JsonSerializerOptions { WriteIndented = true, IncludeFields = true }));
        }
        if (Environment.GetEnvironmentVariable("MEMORIA_FF8_THRUST_CHECKPOINT") is string checkpoint)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(checkpoint)!);
            session.Save(checkpoint, compact: true);
        }
        if (scene.State.NativeFootRuntime is null)
        {
            JsonObject historical = JsonNode.Parse(before)!.AsObject();
            JsonObject savedRuntime = historical["World"]!["NativeRagnarokRuntime"]!.AsObject();
            savedRuntime.Remove("DrawBody");
            savedRuntime.Remove("PreviousParticleMotion");
            GameSession historicalSession = new(JsonSerializer.Deserialize<GameState>(historical.ToJsonString())!);
            using WorldMapScene historicalScene = new(historicalSession, directory);
            Assert.True(JsonNode.DeepEquals(historical, JsonNode.Parse(JsonSerializer.Serialize(historicalSession.State))));
            Assert.True(historicalScene.Apply(new(PlayerActionKind.Wait)).Accepted);
            Assert.Equal(scene.State.Frame + 1, historicalScene.State.Frame);
            Assert.NotNull(historicalScene.State.NativeRagnarokRuntime!.DrawBody);
            Assert.NotNull(historicalScene.State.NativeRagnarokRuntime.PreviousParticleMotion);
        }
        Assert.Equal(scene.Apply(new(PlayerActionKind.Wait)), restoredScene.Apply(new(PlayerActionKind.Wait)));
        Assert.Equal(JsonSerializer.Serialize(session.State), JsonSerializer.Serialize(restoredSession.State));
    }

    private static void AssertCallbackPhase(WorldState preceding, WorldState current, WorldAssets assets)
    {
        NativeRagnarokWorldRuntimeState previous = preceding.NativeRagnarokRuntime!;
        NativeRagnarokWorldRuntimeState runtime = current.NativeRagnarokRuntime!;
        NativeRagnarokWorldObject oldBody = previous.Objects[previous.Allocation.RagnarokIndex];
        Assert.Equal(oldBody, runtime.DrawBody);
        Assert.NotEqual(oldBody, runtime.Objects[runtime.Allocation.RagnarokIndex]);
        Assert.NotEqual(previous.PreviousParticleMotion, runtime.PreviousParticleMotion);
        NativeWorldForestParticle existing = previous.Forest!.Particles.First(particle => particle.Type == 13 && particle.Age > 0 && particle.Age < particle.Lifetime);
        NativeWorldForestParticle aged = Assert.Single(runtime.Forest!.Particles, particle => particle.Slot == existing.Slot);
        NativeWorldPathPosition follow = previous.PreviousParticleMotion!.Value;
        Assert.Equal(existing.X + existing.VelocityX + 5 * (short)follow.X / 6, aged.X);
        Assert.Equal(existing.NativeY + existing.VelocityY + 5 * (preceding.NativeHeight - previous.PreviousParticleNativeHeight!.Value) / 6, aged.NativeY);
        Assert.Equal(existing.GraphicsZ + existing.VelocityZ + 5 * (short)follow.Z / 6, aged.GraphicsZ);
        Assert.Equal(existing.Age + 1, aged.Age);
        NativeWorldForestParticle[] born = runtime.Forest.Particles.Where(particle => particle.Type == 13 && particle.Age == 1).OrderBy(particle => particle.Slot).ToArray();
        Assert.Equal(2, born.Length);
        NativeWorldForestEffect effect = assets.ForestTables.Effects.Single(item => item.Type == 13);
        for (int side = 0; side < born.Length; side++)
        {
            NativeWorldPathPosition mount = assets.ForestTables.RagnarokThrustOffsets[side];
            (short x, short y, short z) = Rotate(oldBody, (short)mount.X, (short)mount.Y, (short)mount.Z);
            (short vx, short vy, short vz) = Rotate(oldBody, effect.VelocityX, effect.VelocityY, effect.VelocityZ);
            Assert.Equal(oldBody.Position.X + x + vx, born[side].X);
            Assert.Equal(oldBody.Position.Y - 146 + y + vy, born[side].NativeY);
            Assert.Equal(oldBody.Position.Z + z + vz, born[side].GraphicsZ);
            Assert.Equal(oldBody.Yaw, born[side].Yaw);
            Assert.Equal(-oldBody.Roll, born[side].Roll);
        }
    }

    private static (short X, short Y, short Z) Rotate(NativeRagnarokWorldObject body, short x, short y, short z)
    {
        MethodInfo rotate = typeof(NativeWorldForestState).GetMethod("RotateParticleVelocity", BindingFlags.Static | BindingFlags.NonPublic)!;
        return ((short X, short Y, short Z))rotate.Invoke(null, new object[] { body.Pitch, body.Yaw, unchecked((short)-body.Roll), x, y, z })!;
    }
}
