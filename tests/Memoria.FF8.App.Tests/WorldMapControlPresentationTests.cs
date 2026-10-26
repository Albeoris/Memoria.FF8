using Bitmap = SkiaSharp.SKBitmap;
using Color = SkiaSharp.SKColor;

using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class WorldMapControlPresentationTests
{
    [Fact]
    public void ControllerBackAndRemappedSelectKeepTheirMappedButtonHeldAcrossSamples()
    {
        foreach (Buttons button in new[] { Buttons.Back, Buttons.LeftStick })
        {
            KeyboardInput keyboard = new();
            if (button != Buttons.Back)
            {
                keyboard.Profile = ControlsProfile.CreateDefault().WithBinding(ControlAction.Select, ControlDevice.Controller, new([], [button]));
            }
            keyboard.Read(default, new GamePadState(default, default, 0, 0, Buttons.None), true);
            InputFrame input = keyboard.Read(default, new GamePadState(default, default, 0, 0, button), true);
            PlayerAction first = ControllerGameplayInput.CreateWorldAction(input, new GameState { Scene = SceneKind.WorldMap }, keyboard.HeldControlButtons);
            input = keyboard.Read(default, new GamePadState(default, default, 0, 0, button), true);
            PlayerAction held = ControllerGameplayInput.CreateWorldAction(input, new GameState { Scene = SceneKind.WorldMap }, keyboard.HeldControlButtons);
            Assert.Equal((ushort)0x100, first.WorldButtons);
            Assert.Equal(first.WorldButtons, held.WorldButtons);
        }
    }

    [Fact]
    public void KeyboardAndRemappedSelectReachTheWorldMapButtonWithoutLosingCameraInput()
    {
        foreach (Keys key in new[] { Keys.J, Keys.NumPad7 })
        {
            KeyboardInput keyboard = new();
            if (key != Keys.J)
            {
                keyboard.Profile = ControlsProfile.CreateDefault().WithBinding(ControlAction.Select, ControlDevice.Keyboard, new([key], []));
            }
            InputFrame input = keyboard.Read(new KeyboardState(key), true);
            PlayerAction action = ControllerGameplayInput.CreateWorldAction(input, new GameState { Scene = SceneKind.WorldMap }, keyboard.HeldControlButtons);
            Assert.Equal((ushort)0x100, action.WorldButtons);
            Assert.Equal(PlayerActionKind.Wait, action.Kind);
            Assert.Equal(InputChordName(key), keyboard.BindingHint(ControlAction.Select));
        }
    }

    [Fact]
    public void FullMapRetainsAcceptedMotionWhileTheNextHeldMotorPacketIsZero()
    {
        GameSession session = new(GameSession.Load(TestResources.FixturePath("junction-world-map-source")));
        using WorldMapScene scene = new(session, TestResources.GameDirectory);
        int wait = checked((int)Math.Max(0, 24 - scene.State.Frame));
        if (wait > 0)
        {
            Assert.True(scene.Apply(new(PlayerActionKind.Wait, Frames: wait)).Accepted);
        }
        foreach (byte mode in new byte[] { 1, 2 })
        {
            Assert.True(scene.Apply(new(PlayerActionKind.Wait)).Accepted);
            Assert.True(scene.Apply(new(PlayerActionKind.Wait, WorldButtons: 0x100)).Accepted);
            Assert.Equal(mode, WorldMapScene.MapDisplayMode(scene.State));
        }
        Assert.True(scene.Apply(new(PlayerActionKind.Move, Horizontal: 1)).Accepted);
        int accepted = scene.State.NativeFootRuntime!.MovementMagnitude20409F4!.Value;
        Assert.True(accepted > 0);
        WorldState before = scene.State;
        Assert.True(scene.Apply(new(PlayerActionKind.Move, Horizontal: 1, WorldButtons: 0x100)).Accepted);
        Assert.Equal((byte)3, WorldMapScene.MapDisplayMode(scene.State));
        Assert.Equal((before.X, before.Z), (scene.State.X, scene.State.Z));
        Assert.Equal(accepted, scene.State.NativeFootRuntime!.MovementMagnitude20409F4);
        Assert.True(session.State.VibrationProcess!.GraphicsSpeed20409F0 > 0);
        Assert.True(scene.Apply(new(PlayerActionKind.Move, Horizontal: 1, WorldButtons: 0x100)).Accepted);
        Assert.Equal((before.X, before.Z), (scene.State.X, scene.State.Z));
        Assert.Equal(0, session.State.VibrationProcess!.GraphicsSpeed20409F0);
        Assert.Equal(accepted, scene.State.NativeFootRuntime!.MovementMagnitude20409F4);
    }

    [Fact]
    public void StandaloneWorldCyclesGlobeAndBothMapsWithoutTravelAndRestoresEachSnapshot()
    {
        string source = TestResources.FixturePath("junction-world-map-source");
        GameSession session = new(GameSession.Load(source));
        Assert.Equal(SceneKind.WorldMap, session.State.Scene);
        string game = TestResources.GameDirectory;
        string archive = TestResources.FixturePath("junction-world-textures");
        string mods = Directory.GetParent(Path.GetDirectoryName(archive)!)!.FullName;
        using GameResourceHost host = new(game, Path.Combine(mods, "test-application"), watch: false, modsDirectory: mods);
        using WorldMapScene scene = new(session, game, host.Resources);
        long wait = Math.Max(0, 24 - scene.State.Frame);
        if (wait > 0)
        {
            Assert.True(scene.Apply(new(PlayerActionKind.Wait, Frames: checked((int)wait))).Accepted);
        }
        Assert.Equal((byte)0, WorldMapScene.MapDisplayMode(scene.State));
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_JUNCTION_OUTPUT") ?? TestResources.ArtifactDirectory;
        Directory.CreateDirectory(output);
        foreach (byte expected in new byte[] { 1, 2, 3, 0 })
        {
            Assert.True(scene.Apply(new(PlayerActionKind.Wait)).Accepted);
            int x = scene.State.X;
            int z = scene.State.Z;
            NativeWorldForestState forest = scene.State.NativeFootRuntime!.Forest!;
            NativeWorldGroundCacheState ground = scene.State.NativeFootRuntime.GroundCache!;
            PlayerAction select = expected == 3 ? new(PlayerActionKind.Move, Horizontal: 1, WorldButtons: 0x100) : new(PlayerActionKind.Wait, WorldButtons: 0x100);
            Assert.True(scene.Apply(select).Accepted);
            Assert.Equal(expected, WorldMapScene.MapDisplayMode(scene.State));
            Assert.Equal((x, z), (scene.State.X, scene.State.Z));
            if (expected == 3)
            {
                Assert.Equal((ushort)0x8100, session.State.WorldProcess!.InputHistory!.CurrentButtons);
                Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(ground), JsonSerializer.SerializeToElement(scene.State.NativeFootRuntime!.GroundCache)));
                Assert.Equal(forest.SeedCounter, scene.State.NativeFootRuntime.Forest!.SeedCounter);
                Assert.Equal(forest.RandomCursor, scene.State.NativeFootRuntime.Forest.RandomCursor);
                Assert.Equal(forest.RandomOffset, scene.State.NativeFootRuntime.Forest.RandomOffset);
                Assert.Equal(forest.Particles, scene.State.NativeFootRuntime.Forest.Particles);
                Assert.True(session.State.VibrationProcess!.GraphicsSpeed20409F0 > 0);
            }
            Assert.True(scene.Apply(new(PlayerActionKind.Wait, WorldButtons: 0x100)).Accepted);
            Assert.Equal(expected, WorldMapScene.MapDisplayMode(scene.State));
            string snapshot = Path.Combine(output, $"world-map-control-mode{expected}.json");
            session.Save(snapshot);
            GameSession restored = new(GameSession.Load(snapshot));
            using WorldMapScene restoredScene = new(restored, game, host.Resources);
            Assert.Equal(expected, WorldMapScene.MapDisplayMode(restoredScene.State));
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restored.State)));
            byte[] pixels = scene.Render(640, 448);
            Assert.Equal(pixels, restoredScene.Render(640, 448));
            SaveFrame(Path.Combine(output, $"world-map-control-mode{expected}.png"), pixels, 640, 448);
            Console.WriteLine($"world-map-snapshot={snapshot} field={session.State.FieldId} frame={restored.State.World!.Frame} mode={expected} boundary=diagnostic-world-no-travel");
            if (expected == 3)
            {
                PlayerAction heldMovement = new(PlayerActionKind.Move, Horizontal: 1, WorldButtons: 0x100);
                Assert.True(scene.Apply(heldMovement).Accepted);
                Assert.True(restoredScene.Apply(heldMovement).Accepted);
                Assert.Equal((x, z), (scene.State.X, scene.State.Z));
                Assert.Equal((byte)3, WorldMapScene.MapDisplayMode(scene.State));
                Assert.Equal((ushort)0x8100, session.State.WorldProcess!.InputHistory!.CurrentButtons);
                Assert.Equal(0, session.State.VibrationProcess!.GraphicsSpeed20409F0);
                Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restored.State)));
            }
        }
    }

    private static string InputChordName(Keys key) => new InputChord([key], []).DisplayName;

    private static void SaveFrame(string path, byte[] rgba, int width, int height)
    {
        using Bitmap bitmap = new(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int pixel = (y * width + x) * 4;
                bitmap.SetPixel(x, y, Color.FromArgb(rgba[pixel + 3], rgba[pixel], rgba[pixel + 1], rgba[pixel + 2]));
            }
        }
        bitmap.SavePng(path);
    }
}
